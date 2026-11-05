:ON ERROR EXIT
-- Ejecutar en SSMS con Consulta > Modo SQLCMD activado.
USE [master];
GO
IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
    THROW 50001, 'Se requiere la instancia SQLEXPRESS del mismo equipo que SSMS.', 1;
GO
USE [PortalMercado_ET01_Test];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')) <> 56
    THROW 50003, 'Se requieren las 56 tablas antes de instalar los procedimientos.', 1;
IF EXISTS (SELECT 1 FROM sys.procedures WHERE is_ms_shipped = 0)
    THROW 50004, 'Ya existen procedimientos. Revisar antes de continuar.', 1;
GO
/****** Object:  StoredProcedure [dbo].[pm_AprobarLiquidacion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_AprobarLiquidacion] @IdLiquidacion BIGINT,@IdAprobador BIGINT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE IdUsuario=@IdAprobador AND Activo=1) THROW 51300,'Aprobador inválido.',1;
 UPDATE dbo.Liquidacion SET Estado='APROBADA',AprobadaEn=SYSUTCDATETIME(),IdAprobador=@IdAprobador
 WHERE IdLiquidacion=@IdLiquidacion AND Estado IN ('PENDIENTE','CONCILIADA') AND Total>0;
 IF @@ROWCOUNT<>1 THROW 51301,'Liquidación no aprobable.',1;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_CancelarPedidoSinCobro]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_CancelarPedidoSinCobro] @IdPedido BIGINT,@Motivo NVARCHAR(500)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @res INT;
 BEGIN TRY BEGIN TRAN;
 --EXEC @res=sys.sp_getapplock @Resource=CONCAT('pm:cancelar:',@IdPedido),@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 DECLARE @RecursoLock NVARCHAR(255);

SET @RecursoLock = CONCAT('pm:cancelar:', @IdPedido);

EXEC @res = sys.sp_getapplock
    @Resource = @RecursoLock,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;

 IF @res<0 THROW 51500,'Bloqueo no disponible.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Pedido WITH(UPDLOCK,HOLDLOCK) WHERE IdPedido=@IdPedido AND Estado='REGISTRADO' AND EstadoPago='PENDIENTE')
 THROW 51501,'Pedido no cancelable sin devolución.',1;
 IF EXISTS(SELECT 1 FROM dbo.TransaccionPago tp JOIN dbo.OrdenPago op ON op.IdOrdenPago=tp.IdOrdenPago WHERE op.IdPedido=@IdPedido AND tp.Tipo IN ('COBRO','CAPTURA') AND tp.Estado='CONFIRMADO')
 THROW 51502,'Existe cobro; utilizar flujo de reembolso.',1;
 IF EXISTS(SELECT 1 FROM dbo.PedidoDetalle d JOIN dbo.PedidoNegocio pn ON pn.IdPedidoNegocio=d.IdPedidoNegocio
 JOIN dbo.Producto p ON p.IdProducto=d.IdProducto LEFT JOIN dbo.Inventario i WITH(UPDLOCK,HOLDLOCK) ON i.IdProducto=p.IdProducto
 WHERE pn.IdPedido=@IdPedido AND p.ControlaStock=1 AND (i.IdProducto IS NULL OR i.Reservado<d.Cantidad))
 THROW 51503,'Reserva de inventario inconsistente.',1;
 UPDATE i SET Reservado=Reservado-d.Cantidad,ActualizadoEn=SYSUTCDATETIME()
 FROM dbo.Inventario i JOIN dbo.Producto p ON p.IdProducto=i.IdProducto
 JOIN dbo.PedidoDetalle d ON d.IdProducto=p.IdProducto
 JOIN dbo.PedidoNegocio pn ON pn.IdPedidoNegocio=d.IdPedidoNegocio
 WHERE pn.IdPedido=@IdPedido AND p.ControlaStock=1 AND i.Reservado>=d.Cantidad;
 UPDATE dbo.PedidoNegocio SET Estado='CANCELADO' WHERE IdPedido=@IdPedido;
 UPDATE dbo.Pedido SET Estado='CANCELADO' WHERE IdPedido=@IdPedido;
 UPDATE dbo.OrdenPago SET Estado='ANULADO' WHERE IdPedido=@IdPedido AND Estado='PENDIENTE';
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_ConciliarCobro]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_ConciliarCobro]
 @IdTransaccion BIGINT,@MontoReportado DECIMAL(18,2),@IdUsuario BIGINT,@IdConciliacion BIGINT OUTPUT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @Monto DECIMAL(18,2),@Proveedor VARCHAR(70),@Fecha DATE;
 BEGIN TRY BEGIN TRAN;
 SELECT @Monto=Monto,@Proveedor=Proveedor,@Fecha=CONVERT(DATE,Fecha) FROM dbo.TransaccionPago WITH(UPDLOCK,HOLDLOCK)
 WHERE IdTransaccion=@IdTransaccion AND Tipo='COBRO' AND Estado='CONFIRMADO';
 IF @Monto IS NULL THROW 51600,'Cobro no confirmado.',1;
 IF @Monto<>@MontoReportado THROW 51601,'Diferencia de conciliación.',1;
 IF EXISTS(SELECT 1 FROM dbo.ConciliacionDetalle WHERE IdTransaccion=@IdTransaccion) THROW 51602,'Cobro ya conciliado.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE IdUsuario=@IdUsuario AND Activo=1) THROW 51603,'Usuario inválido.',1;
 INSERT dbo.ConciliacionPago(Proveedor,FechaCorte,MontoReportado,MontoSistema,Estado,IdUsuario)
 VALUES(@Proveedor,@Fecha,@MontoReportado,@Monto,'CONCILIADA',@IdUsuario);
 SET @IdConciliacion=SCOPE_IDENTITY();
 INSERT dbo.ConciliacionDetalle(IdConciliacion,IdTransaccion,MontoConciliado) VALUES(@IdConciliacion,@IdTransaccion,@Monto);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_ConfirmarCobro]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_ConfirmarCobro]
 @IdOrdenPago BIGINT,@Proveedor VARCHAR(70),@ReferenciaExterna VARCHAR(160),@Monto DECIMAL(18,2),@Moneda CHAR(3)='PEN'
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @IdPedido BIGINT,@MontoEsperado DECIMAL(18,2),@Estado VARCHAR(20),@res INT;
 IF NULLIF(@ReferenciaExterna,'') IS NULL OR NULLIF(@Proveedor,'') IS NULL THROW 51100,'Referencia y proveedor obligatorios.',1;
 BEGIN TRY BEGIN TRAN;
 --EXEC @res=sys.sp_getapplock @Resource=CONCAT('pm:cobro:',@IdOrdenPago),@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 DECLARE @RecursoLock NVARCHAR(255);

SET @RecursoLock = CONCAT('pm:cobro:', @IdOrdenPago);

EXEC @res = sys.sp_getapplock
    @Resource = @RecursoLock,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;

 IF @res<0 THROW 51101,'No se pudo bloquear el cobro.',1;
 SELECT @IdPedido=IdPedido,@MontoEsperado=Monto,@Estado=Estado FROM dbo.OrdenPago WITH(UPDLOCK,HOLDLOCK) WHERE IdOrdenPago=@IdOrdenPago;
 IF @IdPedido IS NULL THROW 51102,'Orden no encontrada.',1;
 IF @Monto<>@MontoEsperado OR @Moneda<>'PEN' THROW 51103,'Monto o moneda no coinciden.',1;
 IF EXISTS(SELECT 1 FROM dbo.TransaccionPago WHERE Proveedor=@Proveedor AND ReferenciaExterna=@ReferenciaExterna AND Tipo='COBRO' AND IdOrdenPago<>@IdOrdenPago)
 THROW 51104,'Referencia externa utilizada en otra orden.',1;
 IF @Estado='PAGADO'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.TransaccionPago WHERE IdOrdenPago=@IdOrdenPago AND Proveedor=@Proveedor AND ReferenciaExterna=@ReferenciaExterna AND Tipo='COBRO' AND Estado='CONFIRMADO')
    THROW 51105,'Orden pagada con otra transacción.',1;
  COMMIT;RETURN;
 END;
 IF @Estado NOT IN ('PENDIENTE','AUTORIZADO') THROW 51106,'Estado no permite confirmación.',1;
 INSERT dbo.TransaccionPago(IdOrdenPago,Proveedor,ReferenciaExterna,Tipo,Estado,Monto,Moneda)
 VALUES(@IdOrdenPago,@Proveedor,@ReferenciaExterna,'COBRO','CONFIRMADO',@Monto,@Moneda);
 UPDATE dbo.OrdenPago SET Estado='PAGADO' WHERE IdOrdenPago=@IdOrdenPago;
 UPDATE dbo.Pedido SET EstadoPago='CONFIRMADO',Estado='CONFIRMADO' WHERE IdPedido=@IdPedido AND Estado='REGISTRADO';
 UPDATE dbo.PedidoNegocio SET Estado='CONFIRMADO',FechaConfirmacion=SYSUTCDATETIME() WHERE IdPedido=@IdPedido AND Estado='PENDIENTE';
 INSERT dbo.MovimientoCuenta(IdNegocio,IdPedidoNegocio,Tipo,Importe,Referencia,Descripcion)
 SELECT IdNegocio,IdPedidoNegocio,'VENTA',Total,CONCAT('VENTA:',IdPedidoNegocio),N'Cobro centralizado confirmado'
 FROM dbo.PedidoNegocio WHERE IdPedido=@IdPedido;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_ConfirmarTransferencia]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_ConfirmarTransferencia]
 @IdLiquidacion BIGINT,@ReferenciaBanco VARCHAR(160),@IdUsuario BIGINT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @Monto DECIMAL(18,2),@res INT;
 IF NULLIF(@ReferenciaBanco,'') IS NULL THROW 51400,'Referencia bancaria obligatoria.',1;
 BEGIN TRY BEGIN TRAN;
 --EXEC @res=sys.sp_getapplock @Resource=CONCAT('pm:transferencia:',@IdLiquidacion),@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
DECLARE @RecursoLock NVARCHAR(255);

SET @RecursoLock = CONCAT('pm:transferencia:', @IdLiquidacion);

EXEC @res = sys.sp_getapplock
    @Resource = @RecursoLock,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;

 IF @res<0 THROW 51401,'Bloqueo no disponible.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE IdUsuario=@IdUsuario AND Activo=1) THROW 51402,'Usuario inválido.',1;
 SELECT @Monto=Total FROM dbo.Liquidacion WITH(UPDLOCK,HOLDLOCK) WHERE IdLiquidacion=@IdLiquidacion AND Estado='APROBADA';
 IF @Monto IS NULL THROW 51403,'Liquidación no aprobada.',1;
 IF EXISTS(SELECT 1 FROM dbo.Transferencia WHERE ReferenciaBanco=@ReferenciaBanco AND IdLiquidacion<>@IdLiquidacion) THROW 51404,'Referencia bancaria duplicada.',1;
 IF EXISTS(SELECT 1 FROM dbo.Transferencia WHERE IdLiquidacion=@IdLiquidacion AND Estado='CONFIRMADA') THROW 51405,'Transferencia ya confirmada.',1;
 IF EXISTS(SELECT 1 FROM dbo.Transferencia WHERE IdLiquidacion=@IdLiquidacion AND Estado NOT IN ('PENDIENTE','ENVIADA')) THROW 51406,'Estado de transferencia inválido.',1;
 IF EXISTS(SELECT 1 FROM dbo.Transferencia WHERE IdLiquidacion=@IdLiquidacion)
  UPDATE dbo.Transferencia SET ReferenciaBanco=@ReferenciaBanco,Monto=@Monto,Estado='CONFIRMADA',Fecha=SYSUTCDATETIME(),IdUsuario=@IdUsuario WHERE IdLiquidacion=@IdLiquidacion;
 ELSE INSERT dbo.Transferencia(IdLiquidacion,ReferenciaBanco,Monto,Estado,Fecha,IdUsuario)
  VALUES(@IdLiquidacion,@ReferenciaBanco,@Monto,'CONFIRMADA',SYSUTCDATETIME(),@IdUsuario);
 UPDATE dbo.Liquidacion SET Estado='TRANSFERIDA' WHERE IdLiquidacion=@IdLiquidacion;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_ConsultarPedido]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_ConsultarPedido] @IdPedido BIGINT
AS
BEGIN
 SET NOCOUNT ON;
 SELECT p.*,op.IdOrdenPago,op.Estado AS EstadoOrdenPago,op.Monto AS MontoOrdenPago
 FROM dbo.Pedido p LEFT JOIN dbo.OrdenPago op ON op.IdPedido=p.IdPedido WHERE p.IdPedido=@IdPedido;
 SELECT pn.*,n.NombreComercial FROM dbo.PedidoNegocio pn JOIN dbo.Negocio n ON n.IdNegocio=pn.IdNegocio WHERE pn.IdPedido=@IdPedido;
 SELECT d.* FROM dbo.PedidoDetalle d JOIN dbo.PedidoNegocio pn ON pn.IdPedidoNegocio=d.IdPedidoNegocio WHERE pn.IdPedido=@IdPedido;
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_ConsultarSaldos]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_ConsultarSaldos] @IdNegocio BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON;
 SELECT n.IdNegocio,n.NombreComercial,
 COALESCE(SUM(mc.Importe),0) AS SaldoContable,
 COALESCE(SUM(CASE WHEN ld.IdMovimientoCuenta IS NULL THEN mc.Importe ELSE 0 END),0) AS PendienteLiquidacion,
 COALESCE(SUM(CASE WHEN l.Estado IN ('PENDIENTE','CONCILIADA','APROBADA') THEN ld.Importe ELSE 0 END),0) AS EnLiquidacionesAbiertas
 FROM dbo.Negocio n LEFT JOIN dbo.MovimientoCuenta mc ON mc.IdNegocio=n.IdNegocio
 LEFT JOIN dbo.LiquidacionDetalle ld ON ld.IdMovimientoCuenta=mc.IdMovimientoCuenta
 LEFT JOIN dbo.Liquidacion l ON l.IdLiquidacion=ld.IdLiquidacion
 WHERE @IdNegocio IS NULL OR n.IdNegocio=@IdNegocio
 GROUP BY n.IdNegocio,n.NombreComercial ORDER BY n.IdNegocio;
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_GenerarLiquidacion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_GenerarLiquidacion]
 @IdNegocio BIGINT,@Desde DATETIME2(3),@Hasta DATETIME2(3),@IdLiquidacion BIGINT OUTPUT
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @IdCuenta BIGINT,@Total DECIMAL(18,2),@res INT;
 IF @Desde>=@Hasta THROW 51200,'Período inválido.',1;
 BEGIN TRY BEGIN TRAN;
 --EXEC @res=sys.sp_getapplock @Resource=CONCAT('pm:liquidacion:',@IdNegocio),@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 DECLARE @RecursoLock NVARCHAR(255);

SET @RecursoLock = CONCAT('pm:liquidacion:', @IdNegocio);

EXEC @res = sys.sp_getapplock
    @Resource = @RecursoLock,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;

 IF @res<0 THROW 51201,'No se pudo bloquear la liquidación.',1;
 SELECT @IdCuenta=IdCuenta FROM dbo.CuentaComerciante WITH(UPDLOCK,HOLDLOCK) WHERE IdNegocio=@IdNegocio AND Verificada=1 AND Activa=1;
 IF @IdCuenta IS NULL THROW 51202,'Cuenta bancaria no verificada.',1;
 /* No liquidar pedidos pendientes de conciliación: se exige conciliación del cobro. */
 SELECT mc.IdMovimientoCuenta,mc.Importe INTO #Pendientes
 FROM dbo.MovimientoCuenta mc WITH(UPDLOCK,HOLDLOCK)
 LEFT JOIN dbo.LiquidacionDetalle ld ON ld.IdMovimientoCuenta=mc.IdMovimientoCuenta
 WHERE mc.IdNegocio=@IdNegocio AND mc.Fecha>=@Desde AND mc.Fecha<@Hasta AND ld.IdMovimientoCuenta IS NULL
 AND (mc.Tipo<>'VENTA' OR EXISTS(
   SELECT 1 FROM dbo.PedidoNegocio pn JOIN dbo.OrdenPago op ON op.IdPedido=pn.IdPedido
   JOIN dbo.TransaccionPago tp ON tp.IdOrdenPago=op.IdOrdenPago AND tp.Tipo='COBRO' AND tp.Estado='CONFIRMADO'
   JOIN dbo.ConciliacionDetalle cd ON cd.IdTransaccion=tp.IdTransaccion
   JOIN dbo.ConciliacionPago cp ON cp.IdConciliacion=cd.IdConciliacion AND cp.Estado='CONCILIADA'
   WHERE pn.IdPedidoNegocio=mc.IdPedidoNegocio));
 SELECT @Total=SUM(Importe) FROM #Pendientes;
 IF @Total IS NULL OR @Total<=0 THROW 51203,'No hay saldo positivo conciliado para liquidar.',1;
 INSERT dbo.Liquidacion(IdNegocio,IdCuenta,PeriodoDesde,PeriodoHasta,Total,Estado)
 VALUES(@IdNegocio,@IdCuenta,@Desde,@Hasta,@Total,'PENDIENTE');
 SET @IdLiquidacion=SCOPE_IDENTITY();
 INSERT dbo.LiquidacionDetalle(IdLiquidacion,IdMovimientoCuenta,Importe)
 SELECT @IdLiquidacion,IdMovimientoCuenta,Importe FROM #Pendientes;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO
/****** Object:  StoredProcedure [dbo].[pm_RegistrarPedido]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE   PROCEDURE [dbo].[pm_RegistrarPedido]
 @IdCarrito BIGINT, @IdMercado BIGINT, @ModalidadEntrega VARCHAR(15),
 @IdDireccion BIGINT=NULL, @IdZona BIGINT=NULL,
 @ClaveOperacion VARCHAR(120), @Observaciones NVARCHAR(500)=NULL,
 @IdPedido BIGINT OUTPUT
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @IdCliente BIGINT,@Subtotal DECIMAL(18,2),@Delivery DECIMAL(18,2)=0,@ahora DATETIME2(3)=SYSUTCDATETIME(),@lockResult INT;
 IF NULLIF(LTRIM(RTRIM(@ClaveOperacion)),'') IS NULL THROW 51000,'Clave de idempotencia requerida.',1;
 IF @ModalidadEntrega NOT IN ('RECOJO','DOMICILIO') THROW 51001,'Modalidad no permitida.',1;
 BEGIN TRY
 BEGIN TRAN;
 --EXEC @lockResult=sys.sp_getapplock @Resource=CONCAT('pm:pedido:',@ClaveOperacion),@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 DECLARE @RecursoLock NVARCHAR(255);

SET @RecursoLock = CONCAT('pm:pedido:', @ClaveOperacion);

EXEC @lockResult = sys.sp_getapplock
    @Resource = @RecursoLock,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 10000;


 IF @lockResult<0 THROW 51002,'No se pudo bloquear la operación.',1;
 SELECT @IdPedido=IdPedido FROM dbo.OrdenPago WITH(UPDLOCK,HOLDLOCK) WHERE IdempotencyKey=@ClaveOperacion;
 IF @IdPedido IS NOT NULL BEGIN
  SELECT @IdPedido=IdPedido FROM dbo.OrdenPago WHERE IdempotencyKey=@ClaveOperacion;
  COMMIT; RETURN;
 END;
 SELECT @IdCliente=IdCliente FROM dbo.Carrito WITH(UPDLOCK,HOLDLOCK) WHERE IdCarrito=@IdCarrito AND Estado='ABIERTO';
 IF @IdCliente IS NULL THROW 51003,'Carrito no disponible.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Mercado WHERE IdMercado=@IdMercado AND Activo=1) THROW 51004,'Mercado no disponible.',1;
 IF @ModalidadEntrega='DOMICILIO'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.ClienteDireccion WHERE IdDireccion=@IdDireccion AND IdCliente=@IdCliente AND Activa=1) THROW 51005,'Dirección inválida.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.ZonaEntrega WHERE IdZona=@IdZona AND IdMercado=@IdMercado AND Activa=1) THROW 51006,'Zona inválida.',1;
  SELECT TOP(1) @Delivery=Monto FROM dbo.TarifaEntrega WHERE IdZona=@IdZona AND Desde<=@ahora AND (Hasta IS NULL OR Hasta>@ahora) ORDER BY Desde DESC,IdTarifa DESC;
  IF @Delivery IS NULL THROW 51007,'No hay tarifa vigente.',1;
 END
 ELSE BEGIN SET @IdDireccion=NULL;SET @IdZona=NULL;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.CarritoDetalle WHERE IdCarrito=@IdCarrito) THROW 51008,'Carrito vacío.',1;
 IF EXISTS(SELECT 1 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto JOIN dbo.Negocio n ON n.IdNegocio=p.IdNegocio
           WHERE cd.IdCarrito=@IdCarrito AND (p.Estado<>'PUBLICADO' OR n.Estado<>'ACTIVO' OR n.IdMercado<>@IdMercado OR p.Precio<=0))
 THROW 51009,'Existen productos o negocios no disponibles.',1;
 IF EXISTS(SELECT 1 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto JOIN dbo.UnidadMedida u ON u.IdUnidad=p.IdUnidad
           WHERE cd.IdCarrito=@IdCarrito AND u.PermiteFraccion=0 AND cd.Cantidad<>FLOOR(cd.Cantidad))
 THROW 51010,'Cantidad fraccionaria no permitida.',1;
 /* Bloquear y validar todas las existencias antes de reservar. */
 IF EXISTS(SELECT 1 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto
  LEFT JOIN dbo.Inventario i WITH(UPDLOCK,HOLDLOCK) ON i.IdProducto=p.IdProducto
  WHERE cd.IdCarrito=@IdCarrito AND p.ControlaStock=1
  AND (i.IdProducto IS NULL OR i.Existencia-i.Reservado<cd.Cantidad))
 THROW 51011,'Stock insuficiente.',1;
 UPDATE i SET Reservado=Reservado+cd.Cantidad,ActualizadoEn=@ahora
 FROM dbo.Inventario i JOIN dbo.CarritoDetalle cd ON cd.IdProducto=i.IdProducto
 JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto
 WHERE cd.IdCarrito=@IdCarrito AND p.ControlaStock=1 AND i.Existencia-i.Reservado>=cd.Cantidad;
 IF @@ROWCOUNT<>(SELECT COUNT(*) FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto WHERE cd.IdCarrito=@IdCarrito AND p.ControlaStock=1)
 THROW 51012,'No se reservaron todas las existencias.',1;
 SELECT @Subtotal=SUM(CONVERT(DECIMAL(18,2),ROUND(cd.Cantidad*p.Precio,2)))
 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto WHERE cd.IdCarrito=@IdCarrito;
 IF @Subtotal IS NULL OR @Subtotal<=0 THROW 51013,'Total inválido.',1;
 INSERT dbo.Pedido(Codigo,IdCliente,IdMercado,IdDireccion,IdZona,ModalidadEntrega,Subtotal,Descuento,CostoEntrega,Total,Observaciones)
 VALUES(CONCAT('TMP-',CONVERT(VARCHAR(36),NEWID())),@IdCliente,@IdMercado,@IdDireccion,@IdZona,@ModalidadEntrega,@Subtotal,0,@Delivery,@Subtotal+@Delivery,@Observaciones);
 SET @IdPedido=SCOPE_IDENTITY();
 UPDATE dbo.Pedido SET Codigo=CONCAT('PM-',CONVERT(VARCHAR(20),@IdPedido)) WHERE IdPedido=@IdPedido;
 INSERT dbo.PedidoNegocio(IdPedido,IdNegocio,Subtotal,Descuento,Total)
 SELECT @IdPedido,p.IdNegocio,SUM(CONVERT(DECIMAL(18,2),ROUND(cd.Cantidad*p.Precio,2))),0,SUM(CONVERT(DECIMAL(18,2),ROUND(cd.Cantidad*p.Precio,2)))
 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto WHERE cd.IdCarrito=@IdCarrito GROUP BY p.IdNegocio;
 INSERT dbo.PedidoDetalle(IdPedidoNegocio,IdProducto,ProductoNombre,UnidadCodigo,Cantidad,PrecioUnitario,Descuento,Total)
 SELECT pn.IdPedidoNegocio,p.IdProducto,p.Nombre,u.Codigo,cd.Cantidad,p.Precio,0,CONVERT(DECIMAL(18,2),ROUND(cd.Cantidad*p.Precio,2))
 FROM dbo.CarritoDetalle cd JOIN dbo.Producto p ON p.IdProducto=cd.IdProducto JOIN dbo.UnidadMedida u ON u.IdUnidad=p.IdUnidad
 JOIN dbo.PedidoNegocio pn ON pn.IdPedido=@IdPedido AND pn.IdNegocio=p.IdNegocio WHERE cd.IdCarrito=@IdCarrito;
 INSERT dbo.OrdenPago(IdPedido,Moneda,Monto,IdempotencyKey) VALUES(@IdPedido,'PEN',@Subtotal+@Delivery,@ClaveOperacion);
 UPDATE dbo.Carrito SET Estado='CONVERTIDO' WHERE IdCarrito=@IdCarrito;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;

GO

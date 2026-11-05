# Matriz inicial SQL - C#

Fuente: `database/original/Portal.sql`, esquema `dbo`. Alcance: cinco entidades piloto y nueve SP inventariados.

Los nombres de propiedades coinciden con columnas; bigint -> long, bit -> bool, datetime2(3) -> DateTime, texto -> string. `?` indica nulabilidad en C#.

## dbo.Mercado -> Domain.Entities.Mercado

| Columna | Tipo SQL | NULL | Propiedad C# | Clave / relación |
|---|---|---|---|---|
| IdMercado | bigint | No | long IdMercado | PK, IDENTITY(1,1) |
| Nombre | nvarchar(180) | No | string Nombre | - |
| RUC | varchar(11) | Sí | string? RUC | - |
| Direccion | nvarchar(250) | Sí | string? Direccion | - |
| Telefono | varchar(30) | Sí | string? Telefono | - |
| Correo | varchar(254) | Sí | string? Correo | - |
| LogoUrl | nvarchar(500) | Sí | string? LogoUrl | - |
| Activo | bit | No | bool Activo | - |
| CreadoEn | datetime2(3) | No | DateTime CreadoEn | - |

Uso: lectura de `dbo.Mercado` por PortalMercadoDbContext y prueba de materialización con datos ficticios.

## dbo.Sector -> Domain.Entities.Sector

| Columna | Tipo SQL | NULL | Propiedad C# | Clave / relación |
|---|---|---|---|---|
| IdSector | bigint | No | long IdSector | PK, IDENTITY(1,1) |
| IdMercado | bigint | No | long IdMercado | FK -> Mercado.IdMercado |
| Codigo | varchar(30) | No | string Codigo | - |
| Nombre | nvarchar(120) | No | string Nombre | - |
| Activo | bit | No | bool Activo | - |

Uso: lectura de `dbo.Sector` por PortalMercadoDbContext y prueba de materialización con datos ficticios.

## dbo.Puesto -> Domain.Entities.Puesto

| Columna | Tipo SQL | NULL | Propiedad C# | Clave / relación |
|---|---|---|---|---|
| IdPuesto | bigint | No | long IdPuesto | PK, IDENTITY(1,1) |
| IdSector | bigint | No | long IdSector | FK -> Sector.IdSector |
| Codigo | varchar(30) | No | string Codigo | - |
| Ubicacion | nvarchar(250) | Sí | string? Ubicacion | - |
| Estado | varchar(20) | No | string Estado | - |

Uso: lectura de `dbo.Puesto` por PortalMercadoDbContext y prueba de materialización con datos ficticios.

## dbo.Negocio -> Domain.Entities.Negocio

| Columna | Tipo SQL | NULL | Propiedad C# | Clave / relación |
|---|---|---|---|---|
| IdNegocio | bigint | No | long IdNegocio | PK, IDENTITY(1,1) |
| IdComerciante | bigint | No | long IdComerciante | FK -> Comerciante.IdComerciante |
| IdMercado | bigint | No | long IdMercado | FK -> Mercado.IdMercado |
| NombreComercial | nvarchar(180) | No | string NombreComercial | - |
| Slug | varchar(160) | No | string Slug | - |
| RUC | varchar(11) | Sí | string? RUC | - |
| RazonSocial | nvarchar(220) | Sí | string? RazonSocial | - |
| DireccionFiscal | nvarchar(250) | Sí | string? DireccionFiscal | - |
| Descripcion | nvarchar(max) | Sí | string? Descripcion | - |
| LogoUrl | nvarchar(500) | Sí | string? LogoUrl | - |
| Telefono | varchar(30) | Sí | string? Telefono | - |
| Correo | varchar(254) | Sí | string? Correo | - |
| Horario | nvarchar(500) | Sí | string? Horario | - |
| Estado | varchar(20) | No | string Estado | - |
| CreadoEn | datetime2(3) | No | DateTime CreadoEn | - |

Uso: lectura de `dbo.Negocio` por PortalMercadoDbContext y prueba de materialización con datos ficticios.

## dbo.Usuario -> Domain.Entities.Usuario

| Columna | Tipo SQL | NULL | Propiedad C# | Clave / relación |
|---|---|---|---|---|
| IdUsuario | bigint | No | long IdUsuario | PK, IDENTITY(1,1) |
| Correo | varchar(254) | No | string Correo | - |
| PasswordHash | nvarchar(500) | No | string PasswordHash | - |
| Nombre | nvarchar(150) | No | string Nombre | - |
| Telefono | varchar(30) | Sí | string? Telefono | - |
| Activo | bit | No | bool Activo | - |
| EmailConfirmado | bit | No | bool EmailConfirmado | - |
| DosFactoresHabilitado | bit | No | bool DosFactoresHabilitado | - |
| CreadoEn | datetime2(3) | No | DateTime CreadoEn | - |
| UltimoAcceso | datetime2(3) | Sí | DateTime? UltimoAcceso | - |

Uso: lectura de `dbo.Usuario` por PortalMercadoDbContext y prueba de materialización con datos ficticios.

## Procedimientos

| Objeto dbo | Parámetros (firma exportada) | Uso en Etapa 01 |
|---|---|---|
| pm_AprobarLiquidacion | `@IdLiquidacion BIGINT,@IdAprobador BIGINT` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_CancelarPedidoSinCobro | `@IdPedido BIGINT,@Motivo NVARCHAR(500)=NULL` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_ConciliarCobro | `@IdTransaccion BIGINT,@MontoReportado DECIMAL(18,2),@IdUsuario BIGINT,@IdConciliacion BIGINT OUTPUT` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_ConfirmarCobro | `@IdOrdenPago BIGINT,@Proveedor VARCHAR(70),@ReferenciaExterna VARCHAR(160),@Monto DECIMAL(18,2),@Moneda CHAR(3)='PEN'` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_ConfirmarTransferencia | `@IdLiquidacion BIGINT,@ReferenciaBanco VARCHAR(160),@IdUsuario BIGINT` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_ConsultarPedido | `@IdPedido BIGINT` | Inventariado; lectura, no implementado en C# |
| pm_ConsultarSaldos | `@IdNegocio BIGINT=NULL` | IStoredProcedureExecutor -> StoredProcedureExecutor; solo lectura; SaldoNegocioDto |
| pm_GenerarLiquidacion | `@IdNegocio BIGINT,@Desde DATETIME2(3),@Hasta DATETIME2(3),@IdLiquidacion BIGINT OUTPUT` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |
| pm_RegistrarPedido | `@IdCarrito BIGINT, @IdMercado BIGINT, @ModalidadEntrega VARCHAR(15), @IdDireccion BIGINT=NULL, @IdZona BIGINT=NULL, @ClaveOperacion VARCHAR(120), @Observaciones NVARCHAR(500)=NULL, @IdPedido BIGINT OUTPUT` | Inventariado; NO ejecutar operaciones de negocio en esta etapa |

## Resultado pm_ConsultarSaldos -> SaldoNegocioDto

| Columna | C# | Observación |
|---|---|---|
| IdNegocio | long | PK de Negocio |
| NombreComercial | string | nvarchar(180), obligatorio |
| SaldoContable | decimal | SUM de decimal(18,2), SQL puede promover a decimal(38,2) |
| PendienteLiquidacion | decimal | Agregado con COALESCE a cero |
| EnLiquidacionesAbiertas | decimal | Agregado con COALESCE a cero |

La escala y precisión de las expresiones agregadas debe contemplarse antes de manejar importes extremos; la prueba usa solo ceros ficticios. El DTO no se publica en endpoints de salud.

-- Adaptación LOCAL autorizada por el usuario que trabaja individualmente.
-- Instalar manualmente en PortalMercado_ET01_Test. No forma parte del contrato central DBA.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'PortalMercado_ET01_Test'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
 THROW 51100, 'Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT<>0 THROW 51100,'Ejecuta sin transacciones abiertas.',1;
IF OBJECT_ID('dbo.Negocio','U') IS NULL OR OBJECT_ID('dbo.Auditoria','U') IS NULL
 OR OBJECT_ID('dbo.UsuarioNegocio','U') IS NULL OR OBJECT_ID('dbo.UsuarioMercado','U') IS NULL
 OR OBJECT_ID('dbo.ProductoImagen','U') IS NULL OR OBJECT_ID('dbo.RolPermiso','U') IS NULL
 THROW 51100,'Prepara primero los esquemas locales de etapas anteriores.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID('dbo.TiendaPublicacionLocal','U') IS NULL
 EXEC sys.sp_executesql N'CREATE TABLE dbo.TiendaPublicacionLocal(
 IdNegocio bigint NOT NULL CONSTRAINT PK_TiendaPublicacionLocal PRIMARY KEY
 CONSTRAINT FK_TiendaPublicacionLocal_Negocio REFERENCES dbo.Negocio(IdNegocio) ON DELETE CASCADE,
 Visible bit NOT NULL CONSTRAINT DF_TiendaLocal_Visible DEFAULT(0),
 TelefonoPublico bit NOT NULL CONSTRAINT DF_TiendaLocal_Telefono DEFAULT(0),
 CorreoPublico bit NOT NULL CONSTRAINT DF_TiendaLocal_Correo DEFAULT(0),
 Version uniqueidentifier NOT NULL CONSTRAINT DF_TiendaLocal_Version DEFAULT(NEWID()),
 UltimoActor bigint NULL,UltimoCambioUtc datetime2(3) NULL,UltimaCorrelacion varchar(100) NULL);';
 EXEC sys.sp_executesql N'INSERT dbo.TiendaPublicacionLocal(IdNegocio)
 SELECT n.IdNegocio FROM dbo.Negocio n WHERE NOT EXISTS(SELECT 1 FROM dbo.TiendaPublicacionLocal t WHERE t.IdNegocio=n.IdNegocio);';
EXEC sys.sp_executesql N'CREATE OR ALTER FUNCTION dbo.pm_TiendaLocal_PuedeEditar(@Actor bigint, @Negocio bigint)
RETURNS bit AS
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE IdUsuario=@Actor AND Activo=1) RETURN 0;
 IF NOT EXISTS(SELECT 1 FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
               WHERE n.IdNegocio=@Negocio AND n.Estado=''ACTIVO'' AND m.Activo=1) RETURN 0;
 IF (SELECT COUNT(DISTINCT p.Codigo) FROM dbo.UsuarioRol ur
     JOIN dbo.RolPermiso rp ON rp.IdRol=ur.IdRol JOIN dbo.Permiso p ON p.IdPermiso=rp.IdPermiso
     WHERE ur.IdUsuario=@Actor AND p.Codigo IN (''PRODUCTO_GESTIONAR'',''NEGOCIO_CONTEXTO'')) <> 2 RETURN 0;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioRol ur JOIN dbo.Rol r ON r.IdRol=ur.IdRol
           WHERE ur.IdUsuario=@Actor AND r.Codigo=''ADMIN_GENERAL'') RETURN 1;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioRol ur JOIN dbo.Rol r ON r.IdRol=ur.IdRol
           WHERE ur.IdUsuario=@Actor AND r.Codigo=''ADMIN_MERCADO'')
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.UsuarioMercado um JOIN dbo.Negocio n ON n.IdMercado=um.IdMercado
            WHERE um.IdUsuario=@Actor AND n.IdNegocio=@Negocio) RETURN 1;
  RETURN 0;
 END;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioNegocio WHERE IdUsuario=@Actor AND IdNegocio=@Negocio AND Activo=1) RETURN 1;
 RETURN 0;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER TRIGGER dbo.tr_TiendaLocal_Version ON dbo.Negocio AFTER INSERT, UPDATE AS
BEGIN
 SET NOCOUNT ON;
 UPDATE t SET Version=NEWID() FROM dbo.TiendaPublicacionLocal t JOIN inserted i ON i.IdNegocio=t.IdNegocio;
 INSERT dbo.TiendaPublicacionLocal(IdNegocio)
 SELECT i.IdNegocio FROM inserted i WHERE NOT EXISTS(SELECT 1 FROM dbo.TiendaPublicacionLocal t WITH(UPDLOCK,HOLDLOCK) WHERE t.IdNegocio=i.IdNegocio);
END';

EXEC sys.sp_executesql N'CREATE OR ALTER VIEW dbo.pm_TiendaLocal_Datos AS
SELECT n.IdNegocio,n.Slug,n.NombreComercial,n.IdMercado,m.Nombre AS Mercado,n.Estado,
 m.Activo AS MercadoActivo,COALESCE(t.Visible,CONVERT(bit,0)) AS VisibilidadAprobada,
 n.Descripcion,n.LogoUrl,n.Horario,n.Telefono,COALESCE(t.TelefonoPublico,CONVERT(bit,0)) AS TelefonoPublico,
 n.Correo,COALESCE(t.CorreoPublico,CONVERT(bit,0)) AS CorreoPublico,
 COALESCE((SELECT STRING_AGG(CONVERT(varchar(max),x.IdCategoria),'','') FROM
   (SELECT DISTINCT p.IdCategoria FROM dbo.Producto p JOIN dbo.Categoria c ON c.IdCategoria=p.IdCategoria
    WHERE p.IdNegocio=n.IdNegocio AND p.Estado=''PUBLICADO'' AND c.Activa=1) x),'''') AS Categorias
FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
LEFT JOIN dbo.TiendaPublicacionLocal t ON t.IdNegocio=n.IdNegocio';

EXEC sys.sp_executesql N'CREATE OR ALTER VIEW dbo.pm_TiendaLocal_Visibles AS
SELECT * FROM dbo.pm_TiendaLocal_Datos d
WHERE d.Estado=''ACTIVO'' AND d.MercadoActivo=1 AND d.VisibilidadAprobada=1
 AND (SELECT COUNT_BIG(*) FROM dbo.Negocio n WHERE n.Slug COLLATE Latin1_General_100_CI_AS=d.Slug COLLATE Latin1_General_100_CI_AS)=1';

EXEC sys.sp_executesql N'CREATE OR ALTER VIEW dbo.pm_TiendaLocal_Productos AS
SELECT p.IdNegocio,p.Estado,p.IdProducto,p.IdCategoria,c.Nombre AS Categoria,p.Nombre,p.Descripcion,
 COALESCE(precio.Precio,p.Precio) AS Precio,u.Codigo AS Unidad,p.ControlaStock,
 CASE WHEN p.ControlaStock=1 THEN COALESCE(i.Existencia,0)-COALESCE(i.Reservado,0) END AS Disponible,
 imagen.Url AS ImagenPrincipal
FROM dbo.Producto p JOIN dbo.Categoria c ON c.IdCategoria=p.IdCategoria AND c.Activa=1
JOIN dbo.UnidadMedida u ON u.IdUnidad=p.IdUnidad
LEFT JOIN dbo.Inventario i ON i.IdProducto=p.IdProducto
OUTER APPLY(SELECT TOP(1) pp.Precio FROM dbo.ProductoPrecio pp
 WHERE pp.IdProducto=p.IdProducto AND pp.VigenteDesde<=SYSUTCDATETIME()
 AND (pp.VigenteHasta IS NULL OR pp.VigenteHasta>SYSUTCDATETIME())
 ORDER BY pp.VigenteDesde DESC,pp.IdProductoPrecio DESC) precio
OUTER APPLY(SELECT TOP(1) pi.Url FROM dbo.ProductoImagen pi WHERE pi.IdProducto=p.IdProducto AND pi.Principal=1
 ORDER BY pi.Orden,pi.IdImagen) imagen
WHERE p.Estado=''PUBLICADO''';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Opciones
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 SELECT DISTINCT IdMercado AS Id,Mercado AS Nombre FROM dbo.pm_TiendaLocal_Visibles ORDER BY Nombre,Id;
 SELECT DISTINCT p.IdCategoria AS Id,p.Categoria AS Nombre FROM dbo.pm_TiendaLocal_Productos p
 JOIN dbo.pm_TiendaLocal_Visibles t ON t.IdNegocio=p.IdNegocio ORDER BY Nombre,Id;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Listar @MercadoId bigint=NULL,@CategoriaId bigint=NULL,@Buscar nvarchar(180)=NULL,@Pagina int=1,@Tamano int=12
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 IF @Pagina<1 OR @Pagina>100000 OR @Tamano<1 OR @Tamano>100 THROW 51104,''Paginacion invalida.'',1;
 SELECT * INTO #Tiendas FROM dbo.pm_TiendaLocal_Visibles t
 WHERE (@MercadoId IS NULL OR t.IdMercado=@MercadoId)
 AND (@Buscar IS NULL OR CHARINDEX(@Buscar,t.NombreComercial)>0)
 AND (@CategoriaId IS NULL OR EXISTS(SELECT 1 FROM dbo.pm_TiendaLocal_Productos p WHERE p.IdNegocio=t.IdNegocio AND p.IdCategoria=@CategoriaId));
 SELECT COUNT(*) AS Total FROM #Tiendas;
 SELECT * FROM #Tiendas ORDER BY NombreComercial,IdNegocio OFFSET (@Pagina-1)*@Tamano ROWS FETCH NEXT @Tamano ROWS ONLY;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Buscar @Slug varchar(160)
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 SELECT TOP(2) * FROM dbo.pm_TiendaLocal_Datos WHERE Slug COLLATE Latin1_General_100_CI_AS=@Slug COLLATE Latin1_General_100_CI_AS ORDER BY IdNegocio;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Ubicaciones @IdNegocio bigint,@Fecha date
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 IF NOT EXISTS(SELECT 1 FROM dbo.pm_TiendaLocal_Visibles WHERE IdNegocio=@IdNegocio) RETURN;
 SELECT np.IdNegocio,s.IdMercado,p.IdPuesto,m.Nombre AS Mercado,s.Nombre AS Sector,p.Codigo AS Puesto,
 p.Ubicacion AS Referencia,m.Activo AS MercadoActivo,s.Activo AS SectorActivo,
 CONVERT(bit,CASE WHEN p.Estado IN (''LIBRE'',''OCUPADO'',''DISPONIBLE'') THEN 1 ELSE 0 END) AS PuestoHabilitado,
 np.Desde,np.Hasta
 FROM dbo.NegocioPuesto np JOIN dbo.Puesto p ON p.IdPuesto=np.IdPuesto
 JOIN dbo.Sector s ON s.IdSector=p.IdSector JOIN dbo.Mercado m ON m.IdMercado=s.IdMercado
 WHERE np.IdNegocio=@IdNegocio AND np.Desde<=@Fecha AND (np.Hasta IS NULL OR np.Hasta>=@Fecha)
 ORDER BY s.Nombre,p.Codigo,p.IdPuesto;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Catalogo @IdNegocio bigint,@CategoriaId bigint=NULL,@Buscar nvarchar(200)=NULL,@Orden varchar(20)=''nombre'',@Pagina int=1,@Tamano int=12
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 IF @Pagina<1 OR @Pagina>100000 OR @Tamano<1 OR @Tamano>100 OR @Orden NOT IN(''nombre'',''precio-asc'',''precio-desc'')
 THROW 51104,''Filtros invalidos.'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.pm_TiendaLocal_Visibles WHERE IdNegocio=@IdNegocio) THROW 51144,''Tienda no disponible.'',1;
 SELECT * INTO #Productos FROM dbo.pm_TiendaLocal_Productos WHERE IdNegocio=@IdNegocio;
 SELECT COUNT(*) AS Total FROM #Productos WHERE (@CategoriaId IS NULL OR IdCategoria=@CategoriaId) AND (@Buscar IS NULL OR CHARINDEX(@Buscar,Nombre)>0);
 SELECT * FROM #Productos WHERE (@CategoriaId IS NULL OR IdCategoria=@CategoriaId) AND (@Buscar IS NULL OR CHARINDEX(@Buscar,Nombre)>0)
 ORDER BY CASE WHEN @Orden=''nombre'' THEN Nombre END,
 CASE WHEN @Orden=''precio-asc'' THEN Precio END ASC, CASE WHEN @Orden=''precio-desc'' THEN Precio END DESC,IdProducto
 OFFSET (@Pagina-1)*@Tamano ROWS FETCH NEXT @Tamano ROWS ONLY;
 SELECT DISTINCT IdCategoria AS Id,Categoria AS Nombre FROM #Productos ORDER BY Nombre,Id;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Perfil @IdNegocio bigint
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 SELECT n.IdNegocio,n.NombreComercial,n.Slug,t.TelefonoPublico,t.CorreoPublico,t.Version,
 n.Descripcion,n.LogoUrl,n.Horario,n.Telefono,n.Correo,t.UltimoActor,t.UltimoCambioUtc,t.UltimaCorrelacion
 FROM dbo.Negocio n JOIN dbo.TiendaPublicacionLocal t ON t.IdNegocio=n.IdNegocio
 WHERE n.IdNegocio=@IdNegocio;
END';

EXEC sys.sp_executesql N'CREATE OR ALTER PROCEDURE dbo.pm_TiendaLocal_Guardar @IdNegocio bigint,@IdActor bigint,@Version uniqueidentifier,
 @Descripcion nvarchar(max)=NULL,@LogoUrl nvarchar(max)=NULL,@Horario nvarchar(max)=NULL,
 @Telefono nvarchar(max)=NULL,@Correo nvarchar(max)=NULL,@Correlacion varchar(100)
AS
BEGIN
 SET NOCOUNT ON;
IF DB_NAME() <> N''PortalMercado_ET01_Test''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''InstanceName'')), N'''') <> N''SQLEXPRESS''
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY(''MachineName'')), N'''') <> ISNULL(HOST_NAME(), N'''')
 THROW 51100, ''Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.'', 1;
 SET XACT_ABORT ON;
 IF @Version IS NULL OR @Version=''00000000-0000-0000-0000-000000000000''
 OR DATALENGTH(@Descripcion)>8000 OR DATALENGTH(@LogoUrl)>1000 OR DATALENGTH(@Horario)>1000
 OR DATALENGTH(@Telefono)>60 OR DATALENGTH(@Correo)>508
 OR DATALENGTH(CONVERT(varchar(max),@Telefono))>30 OR DATALENGTH(CONVERT(varchar(max),@Correo))>254 OR NULLIF(TRIM(@Correlacion),'''') IS NULL
 OR (@LogoUrl IS NOT NULL AND @LogoUrl NOT LIKE ''https://%'')
 OR (CONVERT(nvarchar(max),CONVERT(varchar(max),@Telefono)) COLLATE Latin1_General_100_BIN2 <> @Telefono COLLATE Latin1_General_100_BIN2)
 OR (CONVERT(nvarchar(max),CONVERT(varchar(max),@Correo)) COLLATE Latin1_General_100_BIN2 <> @Correo COLLATE Latin1_General_100_BIN2)
 THROW 51104,''Datos de perfil invalidos.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  -- Bloquear primero Negocio: mismo orden que el trigger para evitar inversiones de bloqueo.
  IF NOT EXISTS(SELECT 1 FROM dbo.Negocio WITH(UPDLOCK,HOLDLOCK) WHERE IdNegocio=@IdNegocio)
   THROW 51144,''Perfil inexistente.'',1;
  IF dbo.pm_TiendaLocal_PuedeEditar(@IdActor,@IdNegocio)<>1 THROW 51143,''Acceso no autorizado.'',1;
  DECLARE @Actual uniqueidentifier;
  SELECT @Actual=Version FROM dbo.TiendaPublicacionLocal WITH(UPDLOCK,HOLDLOCK) WHERE IdNegocio=@IdNegocio;
  IF @Actual IS NULL THROW 51144,''Perfil inexistente.'',1;
  IF @Actual<>@Version THROW 51149,''Version anterior.'',1;
  UPDATE dbo.Negocio SET Descripcion=@Descripcion,LogoUrl=@LogoUrl,Horario=@Horario,
   Telefono=CONVERT(varchar(30),@Telefono),Correo=CONVERT(varchar(254),@Correo) WHERE IdNegocio=@IdNegocio;
  -- El trigger renueva Version también si se edita Negocio desde otro escritor.
  DECLARE @Ahora datetime2(3)=SYSUTCDATETIME();
  UPDATE dbo.TiendaPublicacionLocal SET UltimoActor=@IdActor,UltimoCambioUtc=@Ahora,UltimaCorrelacion=@Correlacion WHERE IdNegocio=@IdNegocio;
  INSERT dbo.Auditoria(IdUsuario,IdNegocio,Entidad,IdRegistro,Accion,Detalle,Fecha)
  VALUES(@IdActor,@IdNegocio,''Negocio'',CONVERT(varchar(100),@IdNegocio),''TIENDA_PERFIL_ACTUALIZADO'',CONCAT(''Correlacion='',@Correlacion),@Ahora);
  COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
END';

 COMMIT;
 PRINT 'Tiendas locales instaladas. Todas las tiendas nuevas permanecen ocultas hasta aprobación explícita.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

-- Solo datos ficticios de la base local. No publica otros negocios.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'PortalMercado_ET01_Test'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
 THROW 51100, 'Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT<>0 THROW 51100,'Ejecuta sin transacciones abiertas.',1;
IF OBJECT_ID('dbo.TiendaPublicacionLocal','U') IS NULL THROW 51100,'Ejecuta primero 01_Instalar_Tiendas_Local.sql.',1;
DECLARE @Negocio bigint,@Categoria bigint,@Unidad int,@Producto bigint;
IF (SELECT COUNT(*) FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
 WHERE n.Slug='negocio-prueba-et01' AND m.Nombre=N'Mercado de prueba ET01' AND n.Estado='ACTIVO' AND m.Activo=1)<>1
 THROW 51100,'No se encontro el negocio ficticio unico del ciclo 1.',1;
SELECT @Negocio=n.IdNegocio FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
 WHERE n.Slug='negocio-prueba-et01' AND m.Nombre=N'Mercado de prueba ET01';
SELECT @Categoria=IdCategoria FROM dbo.Categoria WHERE Slug='frutas-prueba' AND Activa=1;
SELECT @Unidad=IdUnidad FROM dbo.UnidadMedida WHERE Codigo='KG';
IF @Categoria IS NULL OR @Unidad IS NULL THROW 51100,'Prepara la categoria frutas-prueba y la unidad KG del ciclo 1.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 -- Aprobación explícita solo para este negocio ficticio. Contactos se mantienen privados.
 UPDATE dbo.TiendaPublicacionLocal SET Visible=1 WHERE IdNegocio=@Negocio;
 UPDATE dbo.Negocio SET Descripcion=COALESCE(Descripcion,N'Tienda local de prueba conectada a SQL Server.'),
 Horario=COALESCE(Horario,N'Lunes a sabado de 08:00 a 17:00') WHERE IdNegocio=@Negocio;
 SELECT @Producto=IdProducto FROM dbo.Producto WHERE IdNegocio=@Negocio AND SKU='TDA-LOCAL-01';
 IF @Producto IS NULL
 BEGIN
  INSERT dbo.Producto(IdNegocio,IdCategoria,IdUnidad,SKU,Nombre,Descripcion,Precio,ControlaStock,Estado,CreadoEn)
  VALUES(@Negocio,@Categoria,@Unidad,'TDA-LOCAL-01',N'Mango de tienda local',N'Producto ficticio para la tienda publica.',8.90,0,'PUBLICADO',SYSUTCDATETIME());
  SET @Producto=SCOPE_IDENTITY();
  INSERT dbo.ProductoPrecio(IdProducto,Precio,VigenteDesde) VALUES(@Producto,8.90,SYSUTCDATETIME());
  INSERT dbo.ProductoImagen(IdProducto,Url,Orden,Principal)
  VALUES(@Producto,N'https://upload.wikimedia.org/wikipedia/commons/e/ee/Mango_and_cross_section_edit.jpg',1,1);
 END;
 -- Vincular únicamente el puesto ficticio existente si no tiene una asignación vigente.
 DECLARE @Puesto bigint,@Hoy date=CONVERT(date,DATEADD(hour,-5,SYSUTCDATETIME()));
 SELECT @Puesto=p.IdPuesto FROM dbo.Puesto p JOIN dbo.Sector s ON s.IdSector=p.IdSector
 JOIN dbo.Negocio n ON n.IdMercado=s.IdMercado WHERE n.IdNegocio=@Negocio AND p.Codigo='P-ET01';
 IF @Puesto IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.NegocioPuesto WITH(UPDLOCK,HOLDLOCK)
  WHERE IdPuesto=@Puesto AND Desde<=@Hoy AND (Hasta IS NULL OR Hasta>=@Hoy))
  INSERT dbo.NegocioPuesto(IdNegocio,IdPuesto,Desde) VALUES(@Negocio,@Puesto,@Hoy);
 COMMIT;
 SELECT @Negocio AS IdNegocio,Slug,NombreComercial,CONCAT('/tiendas/',Slug) AS RutaPublica FROM dbo.Negocio WHERE IdNegocio=@Negocio;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

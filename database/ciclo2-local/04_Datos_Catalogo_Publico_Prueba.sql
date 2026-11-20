-- Datos ficticios para demostrar filtros y paginación pública en SQL local.
-- No cambia productos CAT-PAG/CAT-LOCAL ni perfiles/contactos existentes.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'PortalMercado_ET01_Test'
 OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'') <> N'SQLEXPRESS'
 OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),N'') <> ISNULL(HOST_NAME(),N'')
 THROW 51100,'Solo se permite la copia local PortalMercado_ET01_Test de este equipo.',1;

IF @@TRANCOUNT<>0 THROW 51100,'Ejecuta sin transacciones abiertas.',1;
IF OBJECT_ID('dbo.pm_TiendaLocal_Catalogo','P') IS NULL
 THROW 51100,'Instala primero los scripts locales 01 y 02.',1;
DECLARE @Negocio bigint,@Unidad int;
IF (SELECT COUNT(*) FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
 WHERE n.Slug='negocio-prueba-et01' AND n.Estado='ACTIVO' AND m.Nombre=N'Mercado de prueba ET01' AND m.Activo=1)<>1
 THROW 51100,'No se encontro el negocio ficticio unico.',1;
SELECT @Negocio=n.IdNegocio FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
 WHERE n.Slug='negocio-prueba-et01' AND m.Nombre=N'Mercado de prueba ET01';
IF NOT EXISTS(SELECT 1 FROM dbo.TiendaPublicacionLocal WHERE IdNegocio=@Negocio AND Visible=1)
 THROW 51100,'Habilita primero la tienda ficticia con el script 02.',1;
SELECT @Unidad=IdUnidad FROM dbo.UnidadMedida WHERE Codigo='KG';
IF @Unidad IS NULL THROW 51100,'Falta la unidad KG de la copia local.',1;
DECLARE @Datos TABLE(Codigo varchar(20) PRIMARY KEY,Nombre nvarchar(200),CategoriaSlug varchar(160),
 Precio decimal(18,2),ControlaStock bit,Existencia decimal(18,3));
INSERT @Datos VALUES
('TDA-DEMO-01',N'Palta Hass','c2-tienda-frutas',9.50,1,10),
('TDA-DEMO-02',N'Tomate italiano','c2-tienda-verduras',4.80,1,0),
('TDA-DEMO-03',N'Zanahoria','c2-tienda-verduras',3.50,1,18),
('TDA-DEMO-04',N'Manzana roja','c2-tienda-frutas',7.90,1,8),
('TDA-DEMO-05',N'Platano de seda','c2-tienda-frutas',5.20,1,15),
('TDA-DEMO-06',N'Mango del catalogo publico','c2-tienda-frutas',6.00,1,12),
('TDA-DEMO-07',N'Mandarina','c2-tienda-frutas',5.00,1,20),
('TDA-DEMO-08',N'Naranja','c2-tienda-frutas',5.00,1,25),
('TDA-DEMO-09',N'Papaya','c2-tienda-frutas',4.50,1,6),
('TDA-DEMO-10',N'Pina','c2-tienda-frutas',6.50,1,7),
('TDA-DEMO-11',N'Uva','c2-tienda-frutas',12.00,1,5),
('TDA-DEMO-12',N'Lechuga por peso','c2-tienda-verduras',2.50,0,0),
('TDA-DEMO-13',N'Pepino','c2-tienda-verduras',3.00,1,11),
('TDA-DEMO-14',N'Cebolla roja','c2-tienda-verduras',3.80,1,14),
('TDA-DEMO-15',N'Sandia','c2-tienda-frutas',2.00,1,35);
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT dbo.Categoria(Nombre,Slug,Activa)
 SELECT v.Nombre,v.Slug,1 FROM (VALUES
  (N'Frutas de tienda (prueba)','c2-tienda-frutas'),(N'Verduras de tienda (prueba)','c2-tienda-verduras')) v(Nombre,Slug)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Categoria c WITH(UPDLOCK,HOLDLOCK) WHERE c.Slug=v.Slug);
 IF EXISTS(SELECT 1 FROM dbo.Categoria WHERE Slug IN('c2-tienda-frutas','c2-tienda-verduras') AND Activa=0)
  THROW 51100,'Las categorias de prueba estan inactivas; no se modificaran automaticamente.',1;
 DECLARE @Creados TABLE(IdProducto bigint PRIMARY KEY);
 INSERT dbo.Producto(IdNegocio,IdCategoria,IdUnidad,SKU,Nombre,Descripcion,Precio,ControlaStock,Estado,CreadoEn)
 OUTPUT inserted.IdProducto INTO @Creados
 SELECT @Negocio,c.IdCategoria,@Unidad,d.Codigo,d.Nombre,N'Producto ficticio para demostrar la tienda local.',
  d.Precio,d.ControlaStock,'PUBLICADO',SYSUTCDATETIME()
 FROM @Datos d JOIN dbo.Categoria c ON c.Slug=d.CategoriaSlug
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Producto p WITH(UPDLOCK,HOLDLOCK) WHERE p.IdNegocio=@Negocio AND p.SKU=d.Codigo);
 INSERT dbo.ProductoPrecio(IdProducto,Precio,VigenteDesde)
 SELECT p.IdProducto,p.Precio,SYSUTCDATETIME() FROM dbo.Producto p JOIN @Creados n ON n.IdProducto=p.IdProducto;
 INSERT dbo.Inventario(IdProducto,Existencia,Reservado,ActualizadoEn)
 SELECT p.IdProducto,d.Existencia,0,SYSUTCDATETIME() FROM dbo.Producto p JOIN @Creados n ON n.IdProducto=p.IdProducto
 JOIN @Datos d ON d.Codigo=p.SKU WHERE p.ControlaStock=1;
 INSERT dbo.ProductoImagen(IdProducto,Url,Orden,Principal)
 SELECT p.IdProducto,N'https://upload.wikimedia.org/wikipedia/commons/e/ee/Mango_and_cross_section_edit.jpg',1,1
 FROM dbo.Producto p JOIN @Creados n ON n.IdProducto=p.IdProducto WHERE p.SKU='TDA-DEMO-06';
 COMMIT;
 SELECT @Negocio AS IdNegocio,(SELECT COUNT(*) FROM dbo.pm_TiendaLocal_Productos WHERE IdNegocio=@Negocio) AS ProductosPublicados,
  '/tiendas/negocio-prueba-et01' AS RutaPublica;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;

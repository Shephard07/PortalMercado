-- Ejecutar completo en SSMS sobre la copia local, después del script 08.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta sin transacciones abiertas.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Negocio bigint, @Unidad int;
    SELECT @Negocio = n.IdNegocio
    FROM dbo.Negocio AS n
    JOIN dbo.Mercado AS m ON m.IdMercado = n.IdMercado AND m.Activo = 1
    WHERE m.Nombre = N'Mercado de prueba ET01'
      AND n.Slug = 'negocio-prueba-et01' AND n.Estado = 'ACTIVO';
    SELECT @Unidad = IdUnidad FROM dbo.UnidadMedida WHERE Codigo = 'UND';

    IF @Negocio IS NULL OR @Unidad IS NULL OR NOT EXISTS (
        SELECT 1 FROM dbo.Producto WHERE IdNegocio = @Negocio AND SKU = 'CAT-LOCAL-STOCK'
    ) OR OBJECT_ID(N'dbo.pm_Catalogo_Productos_Listar', N'P') IS NULL
        THROW 51061, 'Instala los bloques previos, incluido el script 08 de datos locales.', 1;

    INSERT dbo.Categoria (Nombre, Slug, Activa)
    SELECT f.Nombre, f.Slug, 1
    FROM (VALUES
        (N'Paginación local A', 'cat-local-paginacion-a'),
        (N'Paginación local B', 'cat-local-paginacion-b')
    ) AS f(Nombre, Slug)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Categoria WITH (UPDLOCK, HOLDLOCK) WHERE Slug = f.Slug);

    DECLARE @CategoriaA bigint = (SELECT IdCategoria FROM dbo.Categoria WHERE Slug = 'cat-local-paginacion-a');
    DECLARE @CategoriaB bigint = (SELECT IdCategoria FROM dbo.Categoria WHERE Slug = 'cat-local-paginacion-b');

    DECLARE @Numeros TABLE (Numero int PRIMARY KEY);
    INSERT @Numeros VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),
        (11),(12),(13),(14),(15),(16),(17),(18),(19),(20),(21),(22),(23),(24),(25);

    DECLARE @Nuevos TABLE (IdProducto bigint, Precio decimal(18,2), CreadoEn datetime2(3));
    INSERT dbo.Producto (IdNegocio, IdCategoria, IdUnidad, SKU, Nombre, Descripcion,
        Precio, ControlaStock, Estado, CreadoEn)
    OUTPUT inserted.IdProducto, inserted.Precio, inserted.CreadoEn INTO @Nuevos
    SELECT @Negocio, CASE WHEN n.Numero <= 15 THEN @CategoriaA ELSE @CategoriaB END,
        @Unidad, CONCAT('CAT-PAG-', RIGHT(CONCAT('0', n.Numero), 2)),
        CONCAT(N'Producto de paginación local ', RIGHT(CONCAT('0', n.Numero), 2)),
        N'Dato ficticio de validación del catálogo.', 10.00 + n.Numero, 0,
        CASE WHEN n.Numero % 2 = 1 THEN 'BORRADOR' ELSE 'SUSPENDIDO' END,
        SYSUTCDATETIME()
    FROM @Numeros AS n
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Producto AS p WITH (UPDLOCK, HOLDLOCK)
        WHERE p.IdNegocio = @Negocio AND p.SKU = CONCAT('CAT-PAG-', RIGHT(CONCAT('0', n.Numero), 2))
    );

    INSERT dbo.ProductoPrecio (IdProducto, Precio, VigenteDesde)
    SELECT IdProducto, Precio, CreadoEn FROM @Nuevos;
    COMMIT;

    SELECT DB_NAME() AS BaseActual, @Negocio AS IdNegocio,
           @CategoriaA AS CategoriaA, @CategoriaB AS CategoriaB;
    SELECT p.Estado, p.IdCategoria, COUNT(*) AS Productos
    FROM dbo.Producto AS p
    WHERE p.IdNegocio = @Negocio AND p.SKU LIKE 'CAT-PAG-%'
    GROUP BY p.Estado, p.IdCategoria
    ORDER BY p.IdCategoria, p.Estado;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

-- Ejecutar completo en SSMS con PortalMercado_ET01_Test seleccionada.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta el script sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.pm_Catalogo_Categorias_Listar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.pm_Catalogo_Unidades_Listar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.Producto', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Inventario', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProductoImagen', N'U') IS NULL
    THROW 51061, 'Falta el esquema o los procedimientos de catalogo.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Productos_Listar
    @IdNegocio bigint,
    @IdCategoria bigint = NULL,
    @Estado varchar(20) = NULL,
    @Buscar nvarchar(200) = NULL,
    @Pagina int = 1,
    @Tamano int = 20,
    @Total int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdNegocio IS NULL OR @IdNegocio < 1 OR @IdCategoria < 1
       OR @Pagina IS NULL OR @Pagina NOT BETWEEN 1 AND 100000
       OR @Tamano IS NULL OR @Tamano NOT BETWEEN 1 AND 100
       OR @Estado NOT IN (''BORRADOR'', ''PUBLICADO'', ''AGOTADO'', ''SUSPENDIDO'')
        THROW 51062, ''Filtros de producto invalidos.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Total = COUNT(*)
        FROM dbo.Producto AS p WITH (HOLDLOCK)
        WHERE p.IdNegocio = @IdNegocio
          AND (@IdCategoria IS NULL OR p.IdCategoria = @IdCategoria)
          AND (@Estado IS NULL OR p.Estado = @Estado)
          AND (@Buscar IS NULL OR CHARINDEX(@Buscar, p.Nombre) > 0 OR CHARINDEX(@Buscar, p.SKU) > 0);

        ;WITH Pagina AS
        (
            SELECT p.IdProducto, p.IdCategoria, p.IdUnidad, p.SKU, p.Nombre, p.Precio, p.Estado, p.ControlaStock
            FROM dbo.Producto AS p WITH (HOLDLOCK)
            WHERE p.IdNegocio = @IdNegocio
              AND (@IdCategoria IS NULL OR p.IdCategoria = @IdCategoria)
              AND (@Estado IS NULL OR p.Estado = @Estado)
              AND (@Buscar IS NULL OR CHARINDEX(@Buscar, p.Nombre) > 0 OR CHARINDEX(@Buscar, p.SKU) > 0)
            ORDER BY p.Nombre, p.IdProducto
            OFFSET ((@Pagina - 1) * @Tamano) ROWS FETCH NEXT @Tamano ROWS ONLY
        )
        SELECT p.IdProducto, p.IdCategoria, c.Nombre AS Categoria, p.SKU, p.Nombre, p.Precio,
               u.Codigo AS Unidad, p.Estado, p.ControlaStock,
               CASE WHEN p.ControlaStock = 1 THEN i.Existencia - i.Reservado END AS Disponible,
               imagen.Url AS ImagenPrincipal
        FROM Pagina AS p
        JOIN dbo.Categoria AS c ON c.IdCategoria = p.IdCategoria
        JOIN dbo.UnidadMedida AS u ON u.IdUnidad = p.IdUnidad
        LEFT JOIN dbo.Inventario AS i ON i.IdProducto = p.IdProducto
        OUTER APPLY (
            SELECT TOP (1) pi.Url
            FROM dbo.ProductoImagen AS pi
            WHERE pi.IdProducto = p.IdProducto AND pi.Principal = 1
            ORDER BY pi.Orden, pi.IdImagen
        ) AS imagen
        ORDER BY p.Nombre, p.IdProducto;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;';

    DECLARE @IdRol int = (SELECT IdRol FROM dbo.Rol WHERE Codigo = 'ADMIN_GENERAL');
    IF @IdRol IS NULL
        THROW 51061, 'Falta el rol ADMIN_GENERAL de Etapa 02.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Permiso WITH (UPDLOCK, HOLDLOCK) WHERE Codigo = 'PRODUCTO_VER')
        INSERT dbo.Permiso (Codigo, Descripcion) VALUES ('PRODUCTO_VER', N'Consultar productos por negocio');

    DECLARE @IdPermiso int = (SELECT IdPermiso FROM dbo.Permiso WHERE Codigo = 'PRODUCTO_VER');
    IF NOT EXISTS (
        SELECT 1 FROM dbo.RolPermiso WITH (UPDLOCK, HOLDLOCK)
        WHERE IdRol = @IdRol AND IdPermiso = @IdPermiso
    )
        INSERT dbo.RolPermiso (IdRol, IdPermiso) VALUES (@IdRol, @IdPermiso);

    INSERT dbo.UnidadMedida (Codigo, Nombre, PermiteFraccion)
    SELECT u.Codigo, u.Nombre, u.PermiteFraccion
    FROM (VALUES ('KG', N'Kilogramo', 1), ('UND', N'Unidad', 0)) AS u(Codigo, Nombre, PermiteFraccion)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.UnidadMedida AS actual WITH (UPDLOCK, HOLDLOCK) WHERE actual.Codigo = u.Codigo
    );

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, OBJECT_NAME(OBJECT_ID(N'dbo.pm_Catalogo_Productos_Listar', N'P')) AS Procedimiento,
       'ADMIN_GENERAL' AS Rol, 'PRODUCTO_VER' AS Permiso;
SELECT Codigo, Nombre, PermiteFraccion FROM dbo.UnidadMedida WHERE Codigo IN ('KG', 'UND') ORDER BY Codigo;

-- Ejecutar completo en SSMS con PortalMercado_ET01_Test seleccionada.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta el script sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.pm_Catalogo_Productos_Obtener', N'P') IS NULL
   OR OBJECT_ID(N'dbo.ProductoPrecio', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Inventario', N'U') IS NULL
    THROW 51061, 'Instala primero el Bloque 05 en la copia local.', 1;

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Productos_Detalle
    @IdNegocio bigint,
    @IdProducto bigint,
    @Pagina int = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdNegocio IS NULL OR @IdNegocio < 1 OR @IdProducto IS NULL OR @IdProducto < 1
       OR @Pagina IS NULL OR @Pagina NOT BETWEEN 1 AND 100000
        THROW 51075, ''Producto o pagina invalidos.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT p.Nombre, p.SKU, c.Nombre AS Categoria, u.Codigo AS Unidad, p.Estado, p.Precio, p.ControlaStock,
               CASE WHEN p.ControlaStock = 1 THEN i.Existencia END AS Existencia,
               CASE WHEN p.ControlaStock = 1 THEN i.Reservado END AS Reservado
        FROM dbo.Producto AS p WITH (HOLDLOCK)
        JOIN dbo.Categoria AS c ON c.IdCategoria = p.IdCategoria
        JOIN dbo.UnidadMedida AS u ON u.IdUnidad = p.IdUnidad
        LEFT JOIN dbo.Inventario AS i ON i.IdProducto = p.IdProducto AND p.ControlaStock = 1
        WHERE p.IdProducto = @IdProducto AND p.IdNegocio = @IdNegocio;

        SELECT h.Precio, h.VigenteDesde, h.VigenteHasta
        FROM dbo.ProductoPrecio AS h
        JOIN dbo.Producto AS p ON p.IdProducto = h.IdProducto
        WHERE p.IdProducto = @IdProducto AND p.IdNegocio = @IdNegocio
        ORDER BY h.VigenteDesde DESC, h.IdProductoPrecio DESC
        OFFSET ((@Pagina - 1) * 20) ROWS FETCH NEXT 21 ROWS ONLY;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;';

SELECT DB_NAME() AS BaseActual,
       OBJECT_NAME(OBJECT_ID(N'dbo.pm_Catalogo_Productos_Detalle', N'P')) AS Procedimiento;

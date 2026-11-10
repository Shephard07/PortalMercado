-- Ejecutar completo en SSMS con PortalMercado_ET01_Test seleccionada.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta el script sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.pm_Catalogo_Productos_Detalle', N'P') IS NULL
   OR OBJECT_ID(N'dbo.ProductoImagen', N'U') IS NULL
   OR COL_LENGTH(N'dbo.AuditoriaAcceso', N'IdObjetivo') IS NULL
    THROW 51061, 'Instala primero el Bloque 06 en la copia local.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Imagenes_Listar
    @IdNegocio bigint,
    @IdProducto bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.Nombre, i.IdImagen, i.Url, i.Orden, i.Principal
    FROM dbo.Producto AS p
    LEFT JOIN dbo.ProductoImagen AS i ON i.IdProducto = p.IdProducto
    WHERE p.IdProducto = @IdProducto AND p.IdNegocio = @IdNegocio
    ORDER BY i.Orden, i.IdImagen;
END;';

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Imagenes_Guardar
    @IdNegocio bigint,
    @IdProducto bigint,
    @IdImagen bigint,
    @Url nvarchar(500),
    @Orden int,
    @Principal bit,
    @IdActor bigint,
    @Correlacion varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdNegocio IS NULL OR @IdNegocio < 1 OR @IdProducto IS NULL OR @IdProducto < 1 OR @IdImagen < 1
       OR @Url IS NULL OR @Url NOT LIKE N''https://_%''
       OR @Orden IS NULL OR @Orden < 0 OR @Principal IS NULL
       OR NULLIF(TRIM(@Correlacion), '''') IS NULL
        THROW 51076, ''Datos de imagen invalidos.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (HOLDLOCK) WHERE IdUsuario = @IdActor AND Activo = 1)
           OR NOT EXISTS (SELECT 1 FROM dbo.Negocio WITH (HOLDLOCK) WHERE IdNegocio = @IdNegocio AND Estado = ''ACTIVO'')
            THROW 51078, ''Actor o negocio deshabilitado.'', 1;

        -- La fila del producto serializa los guardados de sus imagenes.
        IF NOT EXISTS (
            SELECT 1 FROM dbo.Producto WITH (UPDLOCK, HOLDLOCK)
            WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio
        )
            THROW 51077, ''Producto inexistente en el negocio.'', 1;

        IF @IdImagen IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM dbo.ProductoImagen WITH (UPDLOCK, HOLDLOCK)
            WHERE IdImagen = @IdImagen AND IdProducto = @IdProducto
        )
            THROW 51077, ''Imagen inexistente en el producto.'', 1;

        IF @Principal = 1
            UPDATE dbo.ProductoImagen SET Principal = 0
            WHERE IdProducto = @IdProducto AND Principal = 1 AND (@IdImagen IS NULL OR IdImagen <> @IdImagen);
        ELSE IF (
            SELECT COUNT(*) FROM dbo.ProductoImagen
            WHERE IdProducto = @IdProducto AND Principal = 1 AND (@IdImagen IS NULL OR IdImagen <> @IdImagen)
        ) > 1
            THROW 51079, ''Hay mas de una imagen principal.'', 1;

        DECLARE @Evento varchar(60);
        IF @IdImagen IS NULL
        BEGIN
            INSERT dbo.ProductoImagen (IdProducto, Url, Orden, Principal)
            VALUES (@IdProducto, @Url, @Orden, @Principal);
            SET @IdImagen = CONVERT(bigint, SCOPE_IDENTITY());
            SET @Evento = ''PRODUCTO_IMAGEN_CREAR'';
        END
        ELSE
        BEGIN
            UPDATE dbo.ProductoImagen SET Url = @Url, Orden = @Orden, Principal = @Principal
            WHERE IdImagen = @IdImagen AND IdProducto = @IdProducto;
            SET @Evento = ''PRODUCTO_IMAGEN_EDITAR'';
        END;

        INSERT dbo.AuditoriaAcceso (IdActor, IdObjetivo, Evento, FechaUtc, CorrelationId)
        VALUES (@IdActor, @IdProducto, @Evento, SYSUTCDATETIME(), @Correlacion);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;

    SELECT @IdImagen AS IdImagen;
END;';

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, name AS Procedimiento
FROM sys.procedures
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND name IN (N'pm_Catalogo_Imagenes_Listar', N'pm_Catalogo_Imagenes_Guardar')
ORDER BY name;

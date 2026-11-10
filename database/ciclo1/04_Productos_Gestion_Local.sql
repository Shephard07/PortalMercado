-- Ejecutar completo en SSMS con PortalMercado_ET01_Test seleccionada.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta el script sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.pm_Catalogo_Productos_Listar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.ProductoPrecio', N'U') IS NULL
   OR COL_LENGTH(N'dbo.AuditoriaAcceso', N'IdObjetivo') IS NULL
    THROW 51061, 'Instala primero el Bloque 04 en la copia local.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Productos_Obtener
    @IdNegocio bigint,
    @IdProducto bigint
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdCategoria, IdUnidad, SKU, Nombre, Descripcion, Precio, ControlaStock, Estado
    FROM dbo.Producto
    WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio;
END;';

    DECLARE @GuardarSql nvarchar(max) = CAST(N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Productos_Guardar
    @IdNegocio bigint,
    @IdProducto bigint,
    @IdCategoria bigint,
    @IdUnidad int,
    @Sku varchar(70),
    @Nombre nvarchar(200),
    @Descripcion nvarchar(max),
    @Precio decimal(18,2),
    @ControlaStock bit,
    @IdActor bigint,
    @Correlacion varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Nombre = NULLIF(TRIM(@Nombre), N'''');
    SET @Sku = NULLIF(TRIM(@Sku), '''');

    IF @IdNegocio IS NULL OR @IdNegocio < 1 OR @IdProducto < 1 OR @Nombre IS NULL
       OR @Precio IS NULL OR @Precio < 0 OR @ControlaStock IS NULL
       OR NULLIF(TRIM(@Correlacion), '''') IS NULL
        THROW 51070, ''Datos de producto invalidos.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (HOLDLOCK) WHERE IdUsuario = @IdActor AND Activo = 1)
           OR NOT EXISTS (SELECT 1 FROM dbo.Negocio WITH (HOLDLOCK) WHERE IdNegocio = @IdNegocio AND Estado = ''ACTIVO'')
            THROW 51074, ''Actor o negocio deshabilitado.'', 1;

        DECLARE @Anterior decimal(18,2), @CategoriaAnterior bigint;
        IF @IdProducto IS NOT NULL
        BEGIN
            SELECT @Anterior = Precio, @CategoriaAnterior = IdCategoria
            FROM dbo.Producto WITH (UPDLOCK, HOLDLOCK)
            WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio;
            IF @@ROWCOUNT = 0
                THROW 51071, ''Producto inexistente en el negocio.'', 1;
        END;

        IF NOT EXISTS (
            SELECT 1 FROM dbo.Categoria WITH (HOLDLOCK)
            WHERE IdCategoria = @IdCategoria AND (Activa = 1 OR IdCategoria = @CategoriaAnterior)
        ) OR NOT EXISTS (SELECT 1 FROM dbo.UnidadMedida WITH (HOLDLOCK) WHERE IdUnidad = @IdUnidad)
            THROW 51070, ''Categoria o unidad invalida.'', 1;

        DECLARE @Evento varchar(60), @Ahora datetime2(3) = SYSUTCDATETIME();
        IF @IdProducto IS NULL
        BEGIN
            INSERT dbo.Producto (IdNegocio, IdCategoria, IdUnidad, SKU, Nombre, Descripcion, Precio, ControlaStock, Estado, CreadoEn)
            VALUES (@IdNegocio, @IdCategoria, @IdUnidad, @Sku, @Nombre, @Descripcion, @Precio, @ControlaStock, ''BORRADOR'', @Ahora);
            SET @IdProducto = CONVERT(bigint, SCOPE_IDENTITY());
            SET @Evento = ''PRODUCTO_CREAR'';
        END
        ELSE
        BEGIN
            UPDATE dbo.Producto SET IdCategoria = @IdCategoria, IdUnidad = @IdUnidad, SKU = @Sku,
                Nombre = @Nombre, Descripcion = @Descripcion, Precio = @Precio, ControlaStock = @ControlaStock
            WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio;
            SET @Evento = ''PRODUCTO_EDITAR'';
        END;

        IF @Anterior IS NULL OR @Anterior <> @Precio
        BEGIN
            IF EXISTS (
                SELECT 1 FROM dbo.ProductoPrecio WITH (UPDLOCK, HOLDLOCK)
                WHERE IdProducto = @IdProducto AND (VigenteDesde >= @Ahora OR VigenteHasta > @Ahora)
            ) OR (SELECT COUNT(*) FROM dbo.ProductoPrecio WITH (UPDLOCK, HOLDLOCK)
                  WHERE IdProducto = @IdProducto AND VigenteHasta IS NULL) > 1
                THROW 51073, ''Historial de precios incompatible.'', 1;

            UPDATE dbo.ProductoPrecio SET VigenteHasta = @Ahora' AS nvarchar(max)) + N'
            WHERE IdProducto = @IdProducto AND VigenteHasta IS NULL;

            INSERT dbo.ProductoPrecio (IdProducto, Precio, VigenteDesde, VigenteHasta)
            VALUES (@IdProducto, @Precio, @Ahora, NULL);
        END;

        INSERT dbo.AuditoriaAcceso (IdActor, IdObjetivo, Evento, FechaUtc, CorrelationId)
        VALUES (@IdActor, @IdProducto, @Evento, @Ahora, @Correlacion);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 51072, ''El SKU ya existe en el negocio.'', 1;
        THROW;
    END CATCH;

    SELECT @IdProducto AS IdProducto;
END;';
    EXEC sys.sp_executesql @GuardarSql;

    DECLARE @IdRol int = (SELECT IdRol FROM dbo.Rol WHERE Codigo = 'ADMIN_GENERAL');
    IF @IdRol IS NULL
        THROW 51061, 'Falta el rol ADMIN_GENERAL de Etapa 02.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Permiso WITH (UPDLOCK, HOLDLOCK) WHERE Codigo = 'PRODUCTO_GESTIONAR')
        INSERT dbo.Permiso (Codigo, Descripcion) VALUES ('PRODUCTO_GESTIONAR', N'Registrar y editar productos por negocio');

    DECLARE @IdPermiso int = (SELECT IdPermiso FROM dbo.Permiso WHERE Codigo = 'PRODUCTO_GESTIONAR');
    IF NOT EXISTS (
        SELECT 1 FROM dbo.RolPermiso WITH (UPDLOCK, HOLDLOCK)
        WHERE IdRol = @IdRol AND IdPermiso = @IdPermiso
    )
        INSERT dbo.RolPermiso (IdRol, IdPermiso) VALUES (@IdRol, @IdPermiso);


    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, name AS Procedimiento
FROM sys.procedures
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND name IN (N'pm_Catalogo_Productos_Obtener', N'pm_Catalogo_Productos_Guardar');
SELECT r.Codigo AS Rol, p.Codigo AS Permiso
FROM dbo.RolPermiso AS rp
JOIN dbo.Rol AS r ON r.IdRol = rp.IdRol
JOIN dbo.Permiso AS p ON p.IdPermiso = rp.IdPermiso
WHERE r.Codigo = 'ADMIN_GENERAL' AND p.Codigo = 'PRODUCTO_GESTIONAR';

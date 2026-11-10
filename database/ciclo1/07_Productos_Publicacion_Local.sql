-- Ejecutar completo en SSMS con PortalMercado_ET01_Test seleccionada.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta el script sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.pm_Catalogo_Imagenes_Guardar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.Inventario', N'U') IS NULL
   OR COL_LENGTH(N'dbo.AuditoriaAcceso', N'IdObjetivo') IS NULL
    THROW 51061, 'Instala primero el Bloque 07 en la copia local.', 1;

-- Reglas provisionales para la prueba local de RF-B09.
DECLARE @EstadoSql nvarchar(max) = CAST(N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Productos_CambiarEstado
    @IdNegocio bigint,
    @IdProducto bigint,
    @EstadoActual varchar(20),
    @NuevoEstado varchar(20),
    @IdActor bigint,
    @Correlacion varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdNegocio IS NULL OR @IdNegocio < 1 OR @IdProducto IS NULL OR @IdProducto < 1
       OR @EstadoActual IS NULL OR @EstadoActual NOT IN (''BORRADOR'', ''PUBLICADO'', ''AGOTADO'', ''SUSPENDIDO'')
       OR @NuevoEstado IS NULL OR @NuevoEstado NOT IN (''BORRADOR'', ''PUBLICADO'', ''AGOTADO'', ''SUSPENDIDO'')
       OR NULLIF(TRIM(@Correlacion), '''') IS NULL
        THROW 51080, ''Datos de cambio de estado invalidos.'', 1;

    IF NOT (
        (@EstadoActual = ''BORRADOR'' AND @NuevoEstado = ''PUBLICADO'')
        OR (@EstadoActual = ''PUBLICADO'' AND @NuevoEstado IN (''AGOTADO'', ''SUSPENDIDO''))
        OR (@EstadoActual = ''AGOTADO'' AND @NuevoEstado IN (''PUBLICADO'', ''SUSPENDIDO''))
        OR (@EstadoActual = ''SUSPENDIDO'' AND @NuevoEstado = ''BORRADOR'')
    )
        THROW 51083, ''Transicion no permitida.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (HOLDLOCK) WHERE IdUsuario = @IdActor AND Activo = 1)
           OR NOT EXISTS (SELECT 1 FROM dbo.Negocio WITH (HOLDLOCK) WHERE IdNegocio = @IdNegocio AND Estado = ''ACTIVO'')
            THROW 51087, ''Actor o negocio deshabilitado.'', 1;

        DECLARE @Estado varchar(20), @IdCategoria bigint, @IdUnidad int, @Nombre nvarchar(200),
                @Precio decimal(18,2), @ControlaStock bit;
        SELECT @Estado = Estado, @IdCategoria = IdCategoria, @IdUnidad = IdUnidad,
               @Nombre = Nombre, @Precio = Precio, @ControlaStock = ControlaStock
        FROM dbo.Producto WITH (UPDLOCK, HOLDLOCK)
        WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio;

        IF @@ROWCOUNT = 0
            THROW 51081, ''Producto inexistente en el negocio.'', 1;

        IF @Estado = @NuevoEstado AND @EstadoActual <> @NuevoEstado
        BEGIN
            COMMIT;
            RETURN;
        END;

        IF @Estado <> @EstadoActual
            THROW 51082, ''El estado del producto cambio.'', 1;

        IF @NuevoEstado = ''PUBLICADO''
        BEGIN
            IF NULLIF(TRIM(@Nombre), N'''') IS NULL OR @Precio < 0
               OR NOT EXISTS (SELECT 1 FROM dbo.Categoria WITH (HOLDLOCK) WHERE IdCategoria = @IdCategoria AND Activa = 1)
               OR NOT EXISTS (SELECT 1 FROM dbo.UnidadMedida WITH (HOLDLOCK) WHERE IdUnidad = @IdUnidad)
                THROW 51084, ''Faltan datos minimos para publicar.'', 1;

            IF NOT EXISTS (
                SELECT 1 FROM dbo.ProductoImagen WITH (HOLDLOCK)
                WHERE IdProducto = @IdProducto AND Principal = 1 AND Url LIKE N''https://_%''
            )
                THROW 51085, ''Se requiere una imagen principal HTTPS.'', 1;

            IF @ControlaStock = 1 AND NOT EXISTS (
                SELECT 1 FROM dbo.Inventario WITH (HOLDLOCK)
                WHERE IdProducto = @IdProducto AND Existencia > Reservado
            )
                THROW 51086, ''El producto no tiene stock disponible.'', 1;
        END;
' AS nvarchar(max)) + N'
        UPDATE dbo.Producto SET Estado = @NuevoEstado WHERE IdProducto = @IdProducto AND IdNegocio = @IdNegocio;

        INSERT dbo.AuditoriaAcceso (IdActor, IdObjetivo, Evento, FechaUtc, CorrelationId)
        VALUES (@IdActor, @IdProducto, CONCAT(''PRODUCTO_ESTADO_'', @Estado, ''_'', @NuevoEstado), SYSUTCDATETIME(), @Correlacion);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;';
EXEC sys.sp_executesql @EstadoSql;

SELECT DB_NAME() AS BaseActual,
       OBJECT_NAME(OBJECT_ID(N'dbo.pm_Catalogo_Productos_CambiarEstado', N'P')) AS Procedimiento;

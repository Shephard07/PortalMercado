-- Ejecutar completo en una ventana de SSMS conectada a la copia local.
-- Seleccionar PortalMercado_ET01_Test en el desplegable de la ventana.
-- Los procedimientos se instalan en una transacción y no se modifican filas durante la instalación.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0
    THROW 51061, 'Ejecuta la instalacion en una ventana sin transacciones abiertas.', 1;

IF OBJECT_ID(N'dbo.Categoria', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UnidadMedida', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
   OR COL_LENGTH(N'dbo.AuditoriaAcceso', N'IdObjetivo') IS NULL
   OR OBJECT_ID(N'dbo.SeguridadVersion', N'U') IS NULL
    THROW 51061, 'Falta el esquema base o la auditoria de Etapa 02.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo = 'ET02-004')
    THROW 51061, 'Aplica ET02-004 en la copia local antes de instalar el catalogo.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Categorias_Listar
    @Buscar nvarchar(4000) = NULL,
    @Activa bit = NULL,
    @Pagina int = 1,
    @Tamano int = 20,
    @Total int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Buscar = NULLIF(TRIM(@Buscar), N'''');
    IF DATALENGTH(@Buscar) > 320
       OR @Pagina IS NULL OR @Pagina NOT BETWEEN 1 AND 100000
       OR @Tamano IS NULL OR @Tamano NOT BETWEEN 1 AND 100
        THROW 51062, ''Busqueda o paginacion invalida.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @Total = COUNT(*)
        FROM dbo.Categoria AS c WITH (HOLDLOCK)
        WHERE (@Activa IS NULL OR c.Activa = @Activa)
          AND (@Buscar IS NULL OR CHARINDEX(@Buscar, c.Nombre) > 0 OR CHARINDEX(@Buscar, c.Slug) > 0);

        SELECT c.IdCategoria, c.IdPadre, p.Nombre AS NombrePadre, c.Nombre, c.Slug, c.Activa
        FROM dbo.Categoria AS c WITH (HOLDLOCK)
        LEFT JOIN dbo.Categoria AS p ON p.IdCategoria = c.IdPadre
        WHERE (@Activa IS NULL OR c.Activa = @Activa)
          AND (@Buscar IS NULL OR CHARINDEX(@Buscar, c.Nombre) > 0 OR CHARINDEX(@Buscar, c.Slug) > 0)
        ORDER BY c.Nombre, c.IdCategoria
        OFFSET ((@Pagina - 1) * @Tamano) ROWS FETCH NEXT @Tamano ROWS ONLY;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;';

    DECLARE @GuardarSql nvarchar(max) = CAST(N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Categorias_Guardar
    @IdCategoria bigint,
    @Nombre nvarchar(4000),
    @Slug varchar(8000),
    @IdPadre bigint,
    @IdActor bigint,
    @Correlacion varchar(8000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Nombre = NULLIF(TRIM(@Nombre), N'''');
    SET @Slug = NULLIF(LOWER(TRIM(@Slug) COLLATE Latin1_General_100_BIN2), '''');
    IF @IdCategoria < 1 OR @IdPadre < 1
       OR @Nombre IS NULL OR DATALENGTH(@Nombre) > 260
       OR @Slug IS NULL OR DATALENGTH(@Slug) > 160
       OR @Slug COLLATE Latin1_General_100_BIN2 LIKE ''%[^a-z0-9-]%''
       OR @Slug LIKE ''-%'' OR @Slug LIKE ''%-'' OR @Slug LIKE ''%--%''
       OR @IdActor IS NULL OR @IdActor < 1
       OR NULLIF(TRIM(@Correlacion), '''') IS NULL OR DATALENGTH(@Correlacion) > 64
        THROW 51062, ''Datos de categoria invalidos.'', 1;

    DECLARE @Evento varchar(60) = CASE WHEN @IdCategoria IS NULL THEN ''CATEGORIA_CREAR'' ELSE ''CATEGORIA_EDITAR'' END;
    DECLARE @Resultado TABLE (
        IdCategoria bigint NOT NULL, IdPadre bigint NULL, NombrePadre nvarchar(130) NULL,
        Nombre nvarchar(130) NOT NULL, Slug varchar(160) NOT NULL, Activa bit NOT NULL);

    BEGIN TRY
        BEGIN TRANSACTION;
        -- Serializa cambios de jerarquia entre llamadas del catalogo.
        DECLARE @Bloqueo int;
        EXEC @Bloqueo = sys.sp_getapplock @Resource = N''PortalMercado.Catalogo.Categorias'',
            @LockMode = ''Exclusive'', @LockOwner = ''Transaction'', @LockTimeout = 5000;
        IF @Bloqueo < 0
            THROW 51067, ''Catalogo ocupado. Reintenta la operacion.'', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (HOLDLOCK) WHERE IdUsuario = @IdActor AND Activo = 1)
            THROW 51068, ''El actor no esta activo.'', 1;

        IF @IdCategoria IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM dbo.Categoria WITH (UPDLOCK, HOLDLOCK) WHERE IdCategoria = @IdCategoria)
            THROW 51063, ''Categoria inexistente.'', 1;

        DECLARE @Visitados TABLE (Id bigint PRIMARY KEY);
        DECLARE @Actual bigint = @IdPadre, @Siguiente bigint;
        WHILE @Actual IS NOT NULL
        BEGIN
            IF @Actual = @IdCategoria OR EXISTS (SELECT 1 FROM @Visitados WHERE Id = @Actual)
                THROW 51065, ''La jerarquia de categorias contiene un ciclo.'', 1;

            INSERT @Visitados (Id) VALUES (@Actual);
            SET @Siguiente = NULL;
            SELECT @Siguiente = IdPadre
            FROM dbo.Categoria WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCategoria = @Actual;
            IF @@ROWCOUNT = 0
                THROW 51064, ''La categoria padre no existe.'', 1;

            SET @Actual = @Siguiente;
        END;

        IF EXISTS (
            SELECT 1 FROM dbo.Categoria WITH (UPDLOCK, HOLDLOCK)
            WHERE Slug = @Slug AND (@IdCategoria IS NULL OR IdCategoria <> @IdCategoria))
            THROW 51066, ''El slug ya esta en uso.'', 1;

        IF @IdCategoria IS NULL
        BEGIN
            INSERT dbo.Categoria (IdPadre, Nombre, Slug, Activa) VALUES (@IdPadre, @Nombre, @Slug, 1);
            SET @IdCategoria = CONVERT(bigint, SCOPE_IDENTITY());
        END
        ELSE
            UPDATE dbo.Categoria SET IdPadre = @IdPadre, Nombre = @Nombre, Slug = @Slug
            WHERE IdCategoria = @IdCategoria;

        INSERT dbo.AuditoriaAcceso (IdActor, IdObjetivo, Evento, FechaUtc, CorrelationId)' AS nvarchar(max)) + N'
        VALUES (@IdActor, @IdCategoria, @Evento, SYSUTCDATETIME(), @Correlacion);

        INSERT @Resultado (IdCategoria, IdPadre, NombrePadre, Nombre, Slug, Activa)
        SELECT c.IdCategoria, c.IdPadre, p.Nombre, c.Nombre, c.Slug, c.Activa
        FROM dbo.Categoria AS c
        LEFT JOIN dbo.Categoria AS p ON p.IdCategoria = c.IdPadre
        WHERE c.IdCategoria = @IdCategoria;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 51066, ''El slug ya esta en uso.'', 1;
        THROW;
    END CATCH;

    SELECT IdCategoria, IdPadre, NombrePadre, Nombre, Slug, Activa FROM @Resultado;
END;';
    EXEC sys.sp_executesql @GuardarSql;

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Categorias_CambiarEstado
    @IdCategoria bigint,
    @Activa bit,
    @IdActor bigint,
    @Correlacion varchar(8000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdCategoria IS NULL OR @IdCategoria < 1 OR @Activa IS NULL
       OR @IdActor IS NULL OR @IdActor < 1
       OR NULLIF(TRIM(@Correlacion), '''') IS NULL OR DATALENGTH(@Correlacion) > 64
        THROW 51062, ''Datos de categoria invalidos.'', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WITH (HOLDLOCK) WHERE IdUsuario = @IdActor AND Activo = 1)
            THROW 51068, ''El actor no esta activo.'', 1;

        DECLARE @Anterior bit;
        SELECT @Anterior = Activa FROM dbo.Categoria WITH (UPDLOCK, HOLDLOCK) WHERE IdCategoria = @IdCategoria;
        IF @Anterior IS NULL
            THROW 51063, ''Categoria inexistente.'', 1;

        IF @Anterior <> @Activa
        BEGIN
            UPDATE dbo.Categoria SET Activa = @Activa WHERE IdCategoria = @IdCategoria;

            INSERT dbo.AuditoriaAcceso (IdActor, IdObjetivo, Evento, FechaUtc, CorrelationId)
            VALUES (@IdActor, @IdCategoria, CASE WHEN @Activa = 1 THEN ''CATEGORIA_ACTIVAR'' ELSE ''CATEGORIA_DESACTIVAR'' END,
                    SYSUTCDATETIME(), @Correlacion);
        END;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;';

    EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.pm_Catalogo_Unidades_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdUnidad, Codigo, Nombre, PermiteFraccion
    FROM dbo.UnidadMedida
    ORDER BY Nombre, IdUnidad;
END;';

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, p.name AS Procedimiento
FROM sys.procedures AS p
WHERE p.schema_id = SCHEMA_ID(N'dbo')
  AND p.name IN (N'pm_Catalogo_Categorias_Listar', N'pm_Catalogo_Categorias_Guardar',
                 N'pm_Catalogo_Categorias_CambiarEstado', N'pm_Catalogo_Unidades_Listar')
ORDER BY p.name;

-- Prueba de lectura opcional, en la misma base. Descomenta estas cuatro lineas:
-- DECLARE @Total int;
-- EXEC dbo.pm_Catalogo_Categorias_Listar @Total = @Total OUTPUT;
-- SELECT @Total AS TotalCategorias;
-- EXEC dbo.pm_Catalogo_Unidades_Listar;

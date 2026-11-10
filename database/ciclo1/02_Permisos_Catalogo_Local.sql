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
   OR OBJECT_ID(N'dbo.pm_Catalogo_Categorias_Guardar', N'P') IS NULL
   OR OBJECT_ID(N'dbo.pm_Catalogo_Categorias_CambiarEstado', N'P') IS NULL
   OR OBJECT_ID(N'dbo.pm_Catalogo_Unidades_Listar', N'P') IS NULL
    THROW 51061, 'Instala primero los cuatro procedimientos locales del catalogo.', 1;

DECLARE @Catalogo TABLE (Codigo varchar(50) PRIMARY KEY, Descripcion nvarchar(250));
INSERT @Catalogo (Codigo, Descripcion) VALUES
    ('CATEGORIA_VER', N'Consultar categorías'),
    ('CATEGORIA_GESTIONAR', N'Crear, editar, activar y desactivar categorías'),
    ('UNIDAD_MEDIDA_VER', N'Consultar unidades de medida');

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @IdRol int = (SELECT IdRol FROM dbo.Rol WHERE Codigo = 'ADMIN_GENERAL');
    IF @IdRol IS NULL
        THROW 51061, 'Falta el rol ADMIN_GENERAL de Etapa 02.', 1;

    INSERT dbo.Permiso (Codigo, Descripcion)
    SELECT c.Codigo, c.Descripcion FROM @Catalogo AS c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Permiso AS p WITH (UPDLOCK, HOLDLOCK) WHERE p.Codigo = c.Codigo);

    INSERT dbo.RolPermiso (IdRol, IdPermiso)
    SELECT @IdRol, p.IdPermiso
    FROM dbo.Permiso AS p
    JOIN @Catalogo AS c ON c.Codigo = p.Codigo
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.RolPermiso AS rp WITH (UPDLOCK, HOLDLOCK)
        WHERE rp.IdRol = @IdRol AND rp.IdPermiso = p.IdPermiso
    );

    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, r.Codigo AS Rol, p.Codigo AS Permiso
FROM dbo.Rol AS r
JOIN dbo.RolPermiso AS rp ON rp.IdRol = r.IdRol
JOIN dbo.Permiso AS p ON p.IdPermiso = rp.IdPermiso
JOIN @Catalogo AS c ON c.Codigo = p.Codigo
WHERE r.Codigo = 'ADMIN_GENERAL'
ORDER BY p.Codigo;

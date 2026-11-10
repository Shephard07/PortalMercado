-- Datos ficticios para CA-06 y CA-07. Ejecutar completo en la copia local.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PortalMercado_ET01_Test'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
    THROW 51060, 'Selecciona PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

IF @@TRANCOUNT <> 0 OR OBJECT_ID(N'dbo.pm_Catalogo_Productos_CambiarEstado', N'P') IS NULL
    THROW 51061, 'Instala el Bloque 08 y ejecuta sin transacciones abiertas.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Usuario bigint, @NegocioPropio bigint, @NegocioAjeno bigint, @Mercado bigint, @Comerciante bigint;
    SELECT @Usuario = IdUsuario FROM dbo.Usuario
    WHERE Correo = 'vendedor.et02@example.invalid' AND Activo = 1;

    SELECT @NegocioPropio = n.IdNegocio, @Mercado = n.IdMercado, @Comerciante = n.IdComerciante
    FROM dbo.Negocio AS n
    JOIN dbo.Mercado AS m ON m.IdMercado = n.IdMercado AND m.Activo = 1
    WHERE m.Nombre = N'Mercado de prueba ET01' AND n.Slug = 'negocio-prueba-et01' AND n.Estado = 'ACTIVO';

    IF @Usuario IS NULL OR @NegocioPropio IS NULL
        THROW 51061, 'Falta el vendedor ficticio activo o el negocio de prueba ET01 activo.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.UsuarioNegocio WITH (UPDLOCK, HOLDLOCK)
        WHERE IdUsuario = @Usuario AND IdNegocio = @NegocioPropio AND Activo = 1
    ) OR EXISTS (
        SELECT 1 FROM dbo.UsuarioNegocio WITH (UPDLOCK, HOLDLOCK)
        WHERE IdUsuario = @Usuario AND IdNegocio <> @NegocioPropio AND Activo = 1
    ) OR EXISTS (
        SELECT 1 FROM dbo.UsuarioRol AS ur JOIN dbo.Rol AS r ON r.IdRol = ur.IdRol
        WHERE ur.IdUsuario = @Usuario AND r.Codigo IN ('ADMIN_GENERAL', 'ADMIN_MERCADO')
    )
        THROW 51061, 'El vendedor de prueba debe tener acceso solo al negocio ET01 y carecer de roles administrativos.', 1;

    DECLARE @Categoria bigint = (SELECT MIN(IdCategoria) FROM dbo.Categoria WHERE Activa = 1);
    DECLARE @Unidad int = (SELECT IdUnidad FROM dbo.UnidadMedida WHERE Codigo = 'KG');
    IF @Categoria IS NULL OR @Unidad IS NULL
        THROW 51061, 'Se requiere una categoria activa y la unidad KG del catalogo local.', 1;

    SELECT @NegocioAjeno = IdNegocio FROM dbo.Negocio WITH (UPDLOCK, HOLDLOCK)
    WHERE IdMercado = @Mercado AND Slug = 'negocio-prueba-catalogo-ajeno';
    IF @NegocioAjeno IS NULL
    BEGIN
        INSERT dbo.Negocio (IdComerciante, IdMercado, NombreComercial, Slug, Estado)
        VALUES (@Comerciante, @Mercado, N'Negocio ajeno de prueba catálogo', 'negocio-prueba-catalogo-ajeno', 'ACTIVO');
        SET @NegocioAjeno = CONVERT(bigint, SCOPE_IDENTITY());
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.Negocio WHERE IdNegocio = @NegocioAjeno AND Estado = 'ACTIVO')
        THROW 51061, 'El negocio ajeno de prueba debe estar activo.', 1;

    IF (SELECT COUNT(*) FROM dbo.Permiso
        WHERE Codigo IN ('PRODUCTO_VER', 'PRODUCTO_GESTIONAR', 'NEGOCIO_CONTEXTO')) <> 3
        THROW 51061, 'Faltan permisos de catálogo o de contexto de negocio.', 1;

    DECLARE @Rol int;
    SELECT @Rol = IdRol FROM dbo.Rol WITH (UPDLOCK, HOLDLOCK) WHERE Codigo = 'CATALOGO_PRUEBA_LOCAL';
    IF @Rol IS NULL
    BEGIN
        INSERT dbo.Rol (Codigo, Nombre) VALUES ('CATALOGO_PRUEBA_LOCAL', N'Catálogo de prueba local');
        SET @Rol = CONVERT(int, SCOPE_IDENTITY());
    END;

    INSERT dbo.RolPermiso (IdRol, IdPermiso)
    SELECT @Rol, p.IdPermiso FROM dbo.Permiso AS p
    WHERE p.Codigo IN ('PRODUCTO_VER', 'PRODUCTO_GESTIONAR', 'NEGOCIO_CONTEXTO')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolPermiso WHERE IdRol = @Rol AND IdPermiso = p.IdPermiso);

    IF NOT EXISTS (SELECT 1 FROM dbo.UsuarioRol WHERE IdUsuario = @Usuario AND IdRol = @Rol)
        INSERT dbo.UsuarioRol (IdUsuario, IdRol) VALUES (@Usuario, @Rol);

    DECLARE @Fixtures TABLE (IdNegocio bigint, Sku varchar(70), Nombre nvarchar(200), ControlaStock bit);
    INSERT @Fixtures VALUES
        (@NegocioPropio, 'CAT-LOCAL-STOCK', N'Producto con stock de prueba', 1),
        (@NegocioAjeno, 'CAT-LOCAL-AJENO', N'Producto ajeno de prueba', 0);

    DECLARE @Nuevos TABLE (IdProducto bigint, Precio decimal(18,2), CreadoEn datetime2(3));
    INSERT dbo.Producto (IdNegocio, IdCategoria, IdUnidad, SKU, Nombre, Precio, ControlaStock, Estado, CreadoEn)
    OUTPUT inserted.IdProducto, inserted.Precio, inserted.CreadoEn INTO @Nuevos
    SELECT f.IdNegocio, @Categoria, @Unidad, f.Sku, f.Nombre, 7.50, f.ControlaStock, 'BORRADOR', SYSUTCDATETIME()
    FROM @Fixtures AS f
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Producto AS p WITH (UPDLOCK, HOLDLOCK)
        WHERE p.IdNegocio = f.IdNegocio AND p.SKU = f.Sku
    );

    INSERT dbo.ProductoPrecio (IdProducto, Precio, VigenteDesde)
    SELECT IdProducto, Precio, CreadoEn FROM @Nuevos;

    INSERT dbo.Inventario (IdProducto, Existencia, Reservado)
    SELECT p.IdProducto, 12.500, 2.250
    FROM dbo.Producto AS p
    JOIN @Nuevos AS n ON n.IdProducto = p.IdProducto
    WHERE p.ControlaStock = 1;

    COMMIT;

    SELECT p.IdNegocio, p.IdProducto, p.SKU, i.Existencia, i.Reservado,
           i.Existencia - i.Reservado AS Disponible
    FROM @Fixtures AS f
    JOIN dbo.Producto AS p ON p.IdNegocio = f.IdNegocio AND p.SKU = f.Sku
    LEFT JOIN dbo.Inventario AS i ON i.IdProducto = p.IdProducto
    ORDER BY f.ControlaStock DESC;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    THROW;
END CATCH;

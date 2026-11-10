using PortalMercado.Application.Administration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Catalogo;
using PortalMercado.Infrastructure.Catalogo;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Tests.Catalogo;

[Trait("Category", "SqlIntegration")]
public sealed class CatalogoSqlTests
{
    [CatalogoSqlFact]
    public async Task Categorias_aplica_filtro_y_devuelve_total_aunque_no_haya_filas()
    {
        await using var contexto = CrearContexto();
        IRepositorioCategorias repositorio = new SqlCatalogo(contexto);

        var pagina = await repositorio.ListarAsync(new CategoriaFiltro(Activa: true, Tamano: 1), default);
        var vacia = await repositorio.ListarAsync(new CategoriaFiltro(Buscar: Guid.NewGuid().ToString("N")), default);

        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(1, pagina.Tamano);
        Assert.Equal(Math.Min(1, pagina.Total), pagina.Items.Length);
        Assert.All(pagina.Items, categoria => Assert.True(categoria.Activa));
        Assert.Equal(0, vacia.Total);
        Assert.Empty(vacia.Items);
    }

    [CatalogoSqlFact]
    public async Task Unidades_devuelve_los_datos_del_catalogo_local()
    {
        await using var contexto = CrearContexto();
        IRepositorioUnidadesMedida repositorio = new SqlCatalogo(contexto);

        var unidades = await repositorio.ListarAsync(default);

        Assert.Equal(unidades.Length, unidades.Select(unidad => unidad.Id).Distinct().Count());
        Assert.All(unidades, unidad =>
        {
            Assert.True(unidad.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(unidad.Codigo));
            Assert.False(string.IsNullOrWhiteSpace(unidad.Nombre));
        });
    }

    [ProductosSqlFact]
    public async Task Productos_devuelve_una_pagina_vacia_para_un_negocio_sin_coincidencias()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);
        var filtro = new ProductoFiltro(Estado: "BORRADOR", Buscar: Guid.NewGuid().ToString("N"), Tamano: 1);

        var pagina = await repositorio.ListarAsync(long.MaxValue, filtro, default);

        Assert.Equal(0, pagina.Total);
        Assert.Empty(pagina.Items);
        Assert.Equal(1, pagina.Tamano);
    }

    [ProductosSqlFact]
    public async Task Productos_detalle_no_devuelve_datos_para_un_negocio_inexistente()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);

        Assert.Null(await repositorio.DetalleAsync(long.MaxValue, 1, 1, default));
    }

    [ProductosSqlFact]
    public async Task Imagenes_no_devuelve_datos_para_un_negocio_inexistente()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);

        Assert.Null(await repositorio.ImagenesAsync(long.MaxValue, 1, default));
    }

    [ProductosSqlFact]
    public async Task Sql_rechaza_la_transicion_de_suspendido_a_publicado_antes_de_escribir()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            repositorio.CambiarEstadoAsync(long.MaxValue, 1, new("SUSPENDIDO", "PUBLICADO"), 1, "prueba", default));

        Assert.Equal(409, error.Status);
        Assert.Equal("La transición solicitada no está permitida.", error.Message);
    }

    [CatalogoDatosLocalesFact]
    public async Task Inventario_con_reservas_se_consulta_sin_cambiar_su_version()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);
        var propio = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");

        var pagina = await repositorio.ListarAsync(propio.Negocio, new ProductoFiltro(Buscar: "CAT-LOCAL-STOCK"), default);
        var producto = Assert.Single(pagina.Items);
        Assert.Equal(propio.Producto, producto.Id);
        Assert.Equal<decimal?>(10.250m, producto.Disponible);

        var detalle = await repositorio.DetalleAsync(propio.Negocio, propio.Producto, 1, default);
        Assert.NotNull(detalle);
        var inventario = Assert.IsType<InventarioProducto>(detalle.Inventario);
        Assert.Equal(12.500m, inventario.Existencia);
        Assert.Equal(2.250m, inventario.Reservado);
        Assert.Equal(10.250m, inventario.Disponible);

        var despues = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");
        Assert.NotNull(propio.VersionInventario);
        Assert.Equal(propio.VersionInventario, despues.VersionInventario);
    }

    [CatalogoDatosLocalesFact]
    public async Task Sql_no_lee_ni_edita_un_producto_real_usando_el_negocio_equivocado()
    {
        await using var contexto = CrearContexto();
        var repositorio = new SqlProductos(contexto);
        var propio = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");
        var ajeno = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-catalogo-ajeno", "CAT-LOCAL-AJENO");
        Assert.NotEqual(propio.Negocio, ajeno.Negocio);

        var original = await repositorio.ObtenerAsync(ajeno.Negocio, ajeno.Producto, default);
        Assert.NotNull(original);
        Assert.Null(await repositorio.ObtenerAsync(propio.Negocio, ajeno.Producto, default));
        Assert.Null(await repositorio.DetalleAsync(propio.Negocio, ajeno.Producto, 1, default));
        Assert.Null(await repositorio.ImagenesAsync(propio.Negocio, ajeno.Producto, default));

        var datos = original.Datos;
        var nombreOriginal = datos.Nombre;
        datos.Nombre = "Cambio cruzado que debe rechazarse";
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            repositorio.GuardarAsync(propio.Negocio, ajeno.Producto, datos, propio.Vendedor,
                Guid.NewGuid().ToString("N"), default));
        Assert.Equal(404, error.Status);

        var despues = await repositorio.ObtenerAsync(ajeno.Negocio, ajeno.Producto, default);
        Assert.NotNull(despues);
        Assert.Equal(nombreOriginal, despues.Datos.Nombre);
    }

    [CatalogoPaginacionFact]
    public async Task Productos_pagina_25_registros_sin_repeticiones_y_con_total_en_pagina_vacia()
    {
        await using var contexto = CrearContexto();
        var propio = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");
        var repositorio = new SqlProductos(contexto);
        var filtro = new ProductoFiltro(Buscar: "CAT-PAG-", Tamano: 20);

        var primera = await repositorio.ListarAsync(propio.Negocio, filtro, default);
        var segunda = await repositorio.ListarAsync(propio.Negocio, filtro with { Pagina = 2 }, default);
        var vacia = await repositorio.ListarAsync(propio.Negocio, filtro with { Pagina = 3 }, default);

        Assert.Equal(25, primera.Total);
        Assert.Equal(25, segunda.Total);
        Assert.Equal(25, vacia.Total);
        Assert.Equal(20, primera.Items.Length);
        Assert.Equal(5, segunda.Items.Length);
        Assert.Empty(vacia.Items);
        var productos = primera.Items.Concat(segunda.Items).ToArray();
        Assert.Equal(25, productos.Select(p => p.Id).Distinct().Count());
        var skusEsperados = Enumerable.Range(1, 25).Select(n => $"CAT-PAG-{n:00}").ToArray();
        Assert.Equal(skusEsperados, productos.Select(p => p.Sku).ToArray());
    }

    [CatalogoPaginacionFact]
    public async Task Productos_combina_categoria_estado_y_busqueda_antes_de_paginar()
    {
        await using var contexto = CrearContexto();
        var propio = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");
        var repositorio = new SqlProductos(contexto);
        var referencia = await repositorio.ListarAsync(propio.Negocio, new ProductoFiltro(Buscar: "CAT-PAG-01"), default);
        var categoria = Assert.Single(referencia.Items).IdCategoria;
        var filtro = new ProductoFiltro(IdCategoria: categoria, Estado: "BORRADOR", Buscar: "CAT-PAG-", Tamano: 5);

        var primera = await repositorio.ListarAsync(propio.Negocio, filtro, default);
        var segunda = await repositorio.ListarAsync(propio.Negocio, filtro with { Pagina = 2 }, default);

        Assert.Equal(8, primera.Total);
        Assert.Equal(8, segunda.Total);
        Assert.Equal(5, primera.Items.Length);
        Assert.Equal(3, segunda.Items.Length);
        var productos = primera.Items.Concat(segunda.Items).ToArray();
        Assert.All(productos, p =>
        {
            Assert.Equal(categoria, p.IdCategoria);
            Assert.Equal("BORRADOR", p.Estado);
        });
        Assert.Equal(Enumerable.Range(0, 8).Select(n => $"CAT-PAG-{n * 2 + 1:00}").ToArray(),
            productos.Select(p => p.Sku).ToArray());
    }

    [CatalogoPaginacionFact]
    public async Task Productos_busca_por_nombre_y_sku_y_trata_comodines_como_texto()
    {
        await using var contexto = CrearContexto();
        var propio = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-et01", "CAT-LOCAL-STOCK");
        var repositorio = new SqlProductos(contexto);

        var porNombre = await repositorio.ListarAsync(propio.Negocio,
            new ProductoFiltro(Buscar: "Producto de paginación local 25"), default);
        var porSku = await repositorio.ListarAsync(propio.Negocio, new ProductoFiltro(Buscar: "CAT-PAG-25"), default);
        Assert.Equal(1, porNombre.Total);
        Assert.Equal(1, porSku.Total);
        Assert.Equal(Assert.Single(porNombre.Items).Id, Assert.Single(porSku.Items).Id);

        var literal = await repositorio.ListarAsync(propio.Negocio, new ProductoFiltro(Buscar: "CAT-PAG-%"), default);
        Assert.Equal(0, literal.Total);
        Assert.Empty(literal.Items);
        var ajeno = await LeerProductoDePruebaAsync(contexto, "negocio-prueba-catalogo-ajeno", "CAT-LOCAL-AJENO");
        var otroNegocio = await repositorio.ListarAsync(ajeno.Negocio, new ProductoFiltro(Buscar: "CAT-PAG-"), default);
        Assert.Equal(0, otroNegocio.Total);
        Assert.Empty(otroNegocio.Items);
    }

    private static async Task<(long Negocio, long Producto, long Vendedor, byte[]? VersionInventario)>
        LeerProductoDePruebaAsync(PortalMercadoDbContext contexto, string negocio, string sku)
    {
        await using var connection = new SqlConnection(contexto.Database.GetConnectionString());
        await using var command = new SqlCommand("""
            SELECT p.IdNegocio, p.IdProducto, u.IdUsuario, i.RowVer
            FROM dbo.Producto AS p
            JOIN dbo.Negocio AS n ON n.IdNegocio = p.IdNegocio
            JOIN dbo.Mercado AS m ON m.IdMercado = n.IdMercado
            JOIN dbo.Usuario AS u ON u.Correo = 'vendedor.et02@example.invalid' AND u.Activo = 1
            LEFT JOIN dbo.Inventario AS i ON i.IdProducto = p.IdProducto
            WHERE m.Nombre = N'Mercado de prueba ET01' AND n.Slug = @negocio AND p.SKU = @sku;
            """, connection);
        command.Parameters.Add("@negocio", System.Data.SqlDbType.VarChar, 160).Value = negocio;
        command.Parameters.Add("@sku", System.Data.SqlDbType.VarChar, 70).Value = sku;
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Ejecuta el script 08 de datos de prueba del catálogo.");
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<byte[]>(3));
    }

    private static PortalMercadoDbContext CrearContexto()
    {
        const string connectionString = @"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;" +
            "Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;";
        return new PortalMercadoDbContext(new DbContextOptionsBuilder<PortalMercadoDbContext>()
            .UseSqlServer(connectionString)
            .Options);
    }
}

public sealed class CatalogoSqlFactAttribute : FactAttribute
{
    public CatalogoSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_RUN_CATALOGO_SQL_TESTS") != "1")
            Skip = "Requiere habilitar PM_RUN_CATALOGO_SQL_TESTS=1 e instalar los procedimientos en SQL local.";
    }
}

public sealed class ProductosSqlFactAttribute : FactAttribute
{
    public ProductosSqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_RUN_PRODUCTOS_SQL_TESTS") != "1")
            Skip = "Requiere habilitar PM_RUN_PRODUCTOS_SQL_TESTS=1 e instalar el procedimiento local de productos.";
    }
}

public sealed class CatalogoDatosLocalesFactAttribute : FactAttribute
{
    public CatalogoDatosLocalesFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_RUN_CATALOGO_FIXTURE_TESTS") != "1")
            Skip = "Requiere PM_RUN_CATALOGO_FIXTURE_TESTS=1 y el script 08 de datos locales.";
    }
}

public sealed class CatalogoPaginacionFactAttribute : FactAttribute
{
    public CatalogoPaginacionFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_RUN_CATALOGO_PAGINACION_TESTS") != "1")
            Skip = "Requiere PM_RUN_CATALOGO_PAGINACION_TESTS=1 y los scripts 08 y 09 de datos locales.";
    }
}

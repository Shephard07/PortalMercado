using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class CatalogoTiendaServicioTests
{
    [Fact]
    public async Task Cada_tienda_devuelve_solo_sus_publicados_y_categorias()
    {
        var servicio = CrearServicio();
        var frutas = await servicio.ConsultarAsync("fruteria-el-sol", new(Tamano: 100), default);
        Assert.Equal(15, frutas.Productos.Total);
        Assert.All(frutas.Productos.Items, p => Assert.InRange(p.Id, 100101L, 100115L));
        Assert.Equal(new long[] { 1, 3 }, frutas.Categorias.Select(c => c.Id).ToArray());

        var abarrotes = await servicio.ConsultarAsync("abarrotes-lima", new(), default);
        Assert.Equal(4, abarrotes.Productos.Total);
        Assert.All(abarrotes.Productos.Items, p => Assert.InRange(p.Id, 200101L, 200104L));
        Assert.Equal(2, Assert.Single(abarrotes.Categorias).Id);
        Assert.Empty(frutas.Productos.Items.Select(p => p.Id).Intersect(abarrotes.Productos.Items.Select(p => p.Id)));
    }

    [Fact]
    public async Task Pagina_15_productos_sin_duplicar_y_conserva_total_en_pagina_vacia()
    {
        var servicio = CrearServicio();
        var primera = await servicio.ConsultarAsync("fruteria-el-sol", new(), default);
        var segunda = await servicio.ConsultarAsync("fruteria-el-sol", new(Pagina: 2), default);
        var vacia = await servicio.ConsultarAsync("fruteria-el-sol", new(Pagina: 3), default);
        Assert.Equal(12, primera.Productos.Items.Length);
        Assert.Equal(3, segunda.Productos.Items.Length);
        Assert.Empty(vacia.Productos.Items);
        Assert.Equal(15, vacia.Productos.Total);
        Assert.Equal("Cebolla roja", primera.Productos.Items[0].Nombre);
        var ids = primera.Productos.Items.Concat(segunda.Productos.Items).Select(p => p.Id).ToArray();
        Assert.Equal(15, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("precio-asc", "Sandía")]
    [InlineData("precio-desc", "Uva")]
    public async Task Ordena_por_precio_con_desempate_estable(string orden, string primero)
    {
        var pagina = await CrearServicio().ConsultarAsync("fruteria-el-sol", new(Orden: orden, Tamano: 100), default);
        Assert.Equal(primero, pagina.Productos.Items[0].Nombre);
        Assert.Equal(new long[] { 100107, 100108 },
            pagina.Productos.Items.Where(p => p.Precio == 5m).Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Combina_categoria_y_busqueda_normalizada_sin_perder_las_categorias_de_tienda()
    {
        var pagina = await CrearServicio().ConsultarAsync("fruteria-el-sol",
            new(CategoriaId: 3, Buscar: "  TOmaTE  ", Orden: " PRECIO-ASC "), default);
        Assert.Equal("Tomate italiano", Assert.Single(pagina.Productos.Items).Nombre);
        Assert.Equal("TOmaTE", pagina.Filtro.Buscar);
        Assert.Equal("precio-asc", pagina.Filtro.Orden);
        Assert.Equal(2, pagina.Categorias.Length);

        var ajena = await CrearServicio().ConsultarAsync("fruteria-el-sol", new(CategoriaId: 2), default);
        Assert.Equal(0, ajena.Productos.Total);
        Assert.Empty(ajena.Productos.Items);
        Assert.DoesNotContain(ajena.Categorias, c => c.Id == 2);
    }

    [Theory]
    [InlineData("tienda-oculta")]
    [InlineData("tienda-suspendida")]
    [InlineData("inexistente")]
    public async Task Visibilidad_se_verifica_antes_de_consultar_productos(string slug)
    {
        var repositorio = new RepositorioControlado([]);
        var servicio = new CatalogoTiendaServicio(new TiendaServicio(new RepositorioTiendasDemostracion()), repositorio);
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ConsultarAsync(slug, new(), default));
        Assert.Equal(404, error.Status);
        Assert.Equal(0, repositorio.Consultas);
    }

    [Theory]
    [InlineData("fecha")]
    [InlineData("precio;DROP TABLE Producto")]
    [InlineData("")]
    public async Task Orden_no_admitido_devuelve_400(string orden)
    {
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            CrearServicio().ConsultarAsync("fruteria-el-sol", new(Orden: orden), default));
        Assert.Equal(400, error.Status);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(100001, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Rechaza_paginacion_invalida(int pagina, int tamano)
    {
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            CrearServicio().ConsultarAsync("fruteria-el-sol", new(Pagina: pagina, Tamano: tamano), default));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Rechaza_categoria_y_busqueda_invalidas()
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            CrearServicio().ConsultarAsync("fruteria-el-sol", new(CategoriaId: 0), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            CrearServicio().ConsultarAsync("fruteria-el-sol", new(Buscar: new string('a', 201)), default))).Status);
    }

    [Theory]
    [InlineData(1002, "PUBLICADO", 9)]
    [InlineData(1001, "BORRADOR", 9)]
    [InlineData(1001, "PUBLICADO", -1)]
    public async Task Rechaza_filas_inseguras_del_adaptador(long negocio, string estado, int precio)
    {
        var fila = new ProductoTiendaLectura(negocio, estado, new ProductoPublico(
            1, 1, "Frutas", "Producto interno", null, precio, "KG", false, null, null));
        var servicio = new CatalogoTiendaServicio(new TiendaServicio(new RepositorioTiendasDemostracion()),
            new RepositorioControlado([fila]));
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ConsultarAsync("fruteria-el-sol", new(), default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain("Producto interno", error.Message);
    }

    [Fact]
    public async Task Descarta_url_insegura_y_disponible_cuando_no_controla_stock()
    {
        var fila = new ProductoTiendaLectura(1001, "PUBLICADO", new ProductoPublico(
            1, 1, "Frutas", "Producto ficticio", null, 1m, "UND", false, 99m, "javascript:alert(1)"));
        var servicio = new CatalogoTiendaServicio(new TiendaServicio(new RepositorioTiendasDemostracion()),
            new RepositorioControlado([fila]));
        var resultado = await servicio.ConsultarAsync("fruteria-el-sol", new(), default);
        var producto = Assert.Single(resultado.Productos.Items);
        Assert.Null(producto.ImagenPrincipal);
        Assert.Null(producto.Disponible);
    }

    [Fact]
    public async Task Conserva_disponibilidad_cero_y_positiva_sin_modificar_stock()
    {
        var pagina = await CrearServicio().ConsultarAsync("fruteria-el-sol", new(Tamano: 100), default);
        Assert.Equal<decimal?>(0m, pagina.Productos.Items.Single(p => p.Nombre == "Tomate italiano").Disponible);
        Assert.Equal<decimal?>(10.25m, pagina.Productos.Items.Single(p => p.Nombre == "Palta Hass").Disponible);
        Assert.Null(pagina.Productos.Items.Single(p => p.Nombre == "Lechuga").Disponible);
    }

    private static CatalogoTiendaServicio CrearServicio() =>
        new(new TiendaServicio(new RepositorioTiendasDemostracion()), new RepositorioCatalogoDemostracion());

    private sealed class RepositorioControlado(ProductoTiendaLectura[] filas) : IRepositorioCatalogoPublico
    {
        public int Consultas { get; private set; }

        public Task<CatalogoTiendaLectura> ConsultarAsync(long idNegocio, ProductoTiendaFiltro filtro, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(new CatalogoTiendaLectura(
                new Page<ProductoTiendaLectura>(filas, filas.Length, filtro.Pagina, filtro.Tamano), []));
        }
    }
}

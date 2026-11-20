using System.Text.Json;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class TiendaServicioTests
{
    [Fact]
    public async Task Directorio_filtra_visibilidad_antes_del_total_y_pagina_en_orden()
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion());
        var primera = await servicio.ListarAsync(new(Tamano: 1), default);
        var segunda = await servicio.ListarAsync(new(Pagina: 2, Tamano: 1), default);
        var vacia = await servicio.ListarAsync(new(Pagina: 3, Tamano: 1), default);

        Assert.Equal(2, primera.Total);
        Assert.Equal(2, segunda.Total);
        Assert.Equal(2, vacia.Total);
        Assert.Equal("abarrotes-lima", Assert.Single(primera.Items).Slug);
        Assert.Equal("fruteria-el-sol", Assert.Single(segunda.Items).Slug);
        Assert.Empty(vacia.Items);
    }

    [Fact]
    public async Task Directorio_combina_mercado_categoria_y_nombre_normalizado()
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion());
        var pagina = await servicio.ListarAsync(new(MercadoId: 100, CategoriaId: 1, Buscar: "  EL SOL  "), default);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(1001, Assert.Single(pagina.Items).IdNegocio);

        var sinCoincidencias = await servicio.ListarAsync(new(MercadoId: 100, CategoriaId: 2), default);
        Assert.Equal(0, sinCoincidencias.Total);
        Assert.Empty(sinCoincidencias.Items);
    }

    [Theory]
    [InlineData("tienda-pendiente")]
    [InlineData("tienda-suspendida")]
    [InlineData("tienda-baja")]
    [InlineData("tienda-oculta")]
    [InlineData("mercado-inactivo")]
    [InlineData("inexistente")]
    public async Task Ficha_oculta_e_inexistente_comparten_respuesta(string slug)
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion());
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ObtenerAsync(slug, default));
        Assert.Equal(404, error.Status);
        Assert.Equal("No se encontró la tienda.", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../perfil")]
    [InlineData("dos/partes")]
    [InlineData("-invalido")]
    [InlineData("invalido--doble")]
    public async Task Slug_invalido_se_rechaza_antes_de_acceder_al_repositorio(string slug)
    {
        var repositorio = new RepositorioControlado([]);
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            new TiendaServicio(repositorio).ObtenerAsync(slug, default));
        Assert.Equal(404, error.Status);
        Assert.Equal(0, repositorio.Consultas);
    }

    [Fact]
    public async Task Contactos_privados_se_omiten_y_los_aprobados_se_publican()
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion());
        var privada = await servicio.ObtenerAsync("  FRUTERIA-EL-SOL  ", default);
        Assert.Null(privada.Telefono);
        Assert.Null(privada.Correo);
        var json = JsonSerializer.Serialize(privada);
        Assert.DoesNotContain("sol@example.invalid", json);
        Assert.DoesNotContain("000000001", json);
        Assert.DoesNotContain("RUC", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DireccionFiscal", json);
        Assert.DoesNotContain("IdComerciante", json);

        var publica = await servicio.ObtenerAsync("abarrotes-lima", default);
        Assert.Equal("abarrotes@example.invalid", publica.Correo);
        Assert.Equal("000000002", publica.Telefono);
    }

    [Fact]
    public async Task Slug_duplicado_entre_mercados_no_resuelve_una_tienda_arbitraria()
    {
        var original = await TiendaDePrueba();
        var duplicada = original with { IdNegocio = 2000, IdMercado = 200 };
        var servicio = new TiendaServicio(new RepositorioControlado([original, duplicada]));
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ObtenerAsync(original.Slug, default));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Ficha_rechaza_una_fila_de_otro_slug_devuelta_por_el_adaptador()
    {
        var original = await TiendaDePrueba();
        var servicio = new TiendaServicio(new RepositorioControlado([original]));
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ObtenerAsync("otra-tienda", default));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Directorio_rechaza_adaptador_que_devuelve_una_tienda_privada()
    {
        var original = await TiendaDePrueba();
        var servicio = new TiendaServicio(new RepositorioControlado([original with { VisibilidadAprobada = false }]));
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ListarAsync(new(), default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain(original.NombreComercial, error.Message);
    }

    [Theory]
    [InlineData("http://example.invalid/logo.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://usuario:clave@example.invalid/logo.jpg")]
    public async Task Logo_inseguro_se_omite_en_ficha_y_directorio(string logo)
    {
        var original = await TiendaDePrueba();
        var servicio = new TiendaServicio(new RepositorioControlado([original with { LogoUrl = logo }]));
        Assert.Null((await servicio.ObtenerAsync(original.Slug, default)).LogoUrl);
        Assert.Null(Assert.Single((await servicio.ListarAsync(new(), default)).Items).LogoUrl);
    }

    [Fact]
    public async Task Logo_https_valido_se_conserva()
    {
        var original = await TiendaDePrueba();
        const string logo = "https://example.invalid/logo.jpg";
        var servicio = new TiendaServicio(new RepositorioControlado([original with { LogoUrl = logo }]));
        Assert.Equal(logo, (await servicio.ObtenerAsync(original.Slug, default)).LogoUrl);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(100001, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Paginacion_invalida_se_rechaza_antes_del_repositorio(int pagina, int tamano)
    {
        var repositorio = new RepositorioControlado([]);
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            new TiendaServicio(repositorio).ListarAsync(new(Pagina: pagina, Tamano: tamano), default));
        Assert.Equal(400, error.Status);
        Assert.Equal(0, repositorio.Consultas);
    }

    [Fact]
    public async Task Filtros_invalidos_se_rechazan()
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion());
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(new(MercadoId: 0), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(new(CategoriaId: -1), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(new(Buscar: new string('a', 181)), default))).Status);
    }

    [Fact]
    public async Task Opciones_publicas_omiten_mercados_ocultos()
    {
        var opciones = await new TiendaServicio(new RepositorioTiendasDemostracion()).OpcionesAsync(default);
        Assert.Equal(new long[] { 100, 101 }, opciones.Mercados.Select(m => m.Id).ToArray());
        Assert.Equal(new long[] { 1, 2, 3 }, opciones.Categorias.Select(c => c.Id).ToArray());
    }

    private static async Task<TiendaLectura> TiendaDePrueba() =>
        Assert.Single(await new RepositorioTiendasDemostracion().BuscarPorSlugAsync("fruteria-el-sol", default));

    private sealed class RepositorioControlado(TiendaLectura[] filas) : IRepositorioTiendas
    {
        public int Consultas { get; private set; }

        public Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct) =>
            Task.FromResult(new TiendaOpciones([], []));

        public Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(new Page<TiendaLectura>(filas, filas.Length, filtro.Pagina, filtro.Tamano));
        }

        public Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(filas);
        }
    }
}

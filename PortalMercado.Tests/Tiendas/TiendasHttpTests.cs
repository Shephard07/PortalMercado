using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class TiendasHttpTests
{
    [Theory]
    [InlineData("/tiendas")]
    [InlineData("/tiendas/fruteria-el-sol")]
    [InlineData("/tiendas/abarrotes-lima")]
    public async Task Las_pantallas_publicas_abren_sin_iniciar_sesion(string ruta)
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        using var respuesta = await client.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Demostración", WebUtility.HtmlDecode(html));
        Assert.Contains("/css/tiendas.css", html);
        Assert.Contains("Saltar al contenido", html);
    }

    [Fact]
    public async Task La_ficha_omite_contactos_privados_y_codifica_texto()
    {
        await using var factory = new WebFactory(textoDePrueba: true);
        using var client = Cliente(factory);
        var html = await client.GetStringAsync("/tiendas/fruteria-el-sol");
        Assert.DoesNotContain("<script>PRUEBA</script>", html);
        Assert.Contains("&lt;script&gt;PRUEBA&lt;/script&gt;", html);
        Assert.DoesNotContain("sol@example.invalid", html);
        Assert.DoesNotContain("000000001", html);
        Assert.Contains("data-reemplazo=", html);
    }

    [Fact]
    public async Task La_paginacion_mvc_conserva_busqueda_y_tamano()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        var html = await client.GetStringAsync("/tiendas?q=a&tamano=1");
        var enlace = Regex.Matches(html, "href=\"([^\"]+)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Single(url => url.Contains("pagina=2", StringComparison.Ordinal));
        var uri = new Uri(client.BaseAddress!, enlace);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("a", query["q"].ToString());
        Assert.Equal("1", query["tamano"].ToString());
        var segunda = await client.GetStringAsync(enlace);
        Assert.Contains("fruteria-el-sol", segunda);
        Assert.DoesNotContain("/tiendas/abarrotes-lima", segunda);
    }

    [Theory]
    [InlineData("/tiendas/tienda-oculta", HttpStatusCode.NotFound)]
    [InlineData("/tiendas/no-existe", HttpStatusCode.NotFound)]
    [InlineData("/tiendas?mercadoId=abc", HttpStatusCode.BadRequest)]
    [InlineData("/tiendas?pagina=0", HttpStatusCode.BadRequest)]
    public async Task Mvc_devuelve_errores_publicos_con_su_estado(string ruta, HttpStatusCode estado)
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        using var respuesta = await client.GetAsync(ruta);
        Assert.Equal(estado, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Volver al directorio", html);
        Assert.DoesNotContain("ConnectionStrings", html);
    }

    [Fact]
    public async Task Api_publica_filtra_y_no_revela_contactos_privados()
    {
        await using var factory = new ApiTiendasFactory();
        using var client = Cliente(factory);
        var json = await client.GetStringAsync("/api/tiendas?mercadoId=100&categoriaId=1&q=sol");
        using var pagina = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, pagina.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1001, pagina.RootElement.GetProperty("items")[0].GetProperty("idNegocio").GetInt64());

        var ficha = await client.GetStringAsync("/api/tiendas/fruteria-el-sol");
        Assert.DoesNotContain("sol@example.invalid", ficha);
        Assert.DoesNotContain("000000001", ficha);
        Assert.DoesNotContain("direccionFiscal", ficha, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idComerciante", ficha, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tienda-pendiente")]
    [InlineData("tienda-suspendida")]
    [InlineData("tienda-baja")]
    [InlineData("tienda-oculta")]
    [InlineData("mercado-inactivo")]
    [InlineData("inexistente")]
    public async Task Api_oculta_estados_y_negocios_inexistentes_con_404(string slug)
    {
        await using var factory = new ApiTiendasFactory();
        using var client = Cliente(factory);
        using var respuesta = await client.GetAsync("/api/tiendas/" + slug);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("No se encontró la tienda.", json.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Sin_modo_demo_el_directorio_devuelve_503_en_ambos_hosts()
    {
        await using var web = new WebFactory(demostracion: false);
        using var clienteWeb = Cliente(web);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await clienteWeb.GetAsync("/tiendas")).StatusCode);

        await using var api = new ApiTiendasFactory(demostracion: false);
        using var clienteApi = Cliente(api);
        using var respuesta = await clienteApi.GetAsync("/api/tiendas");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.DoesNotContain("abarrotes-lima", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_demostracion_se_rechaza_fuera_de_development()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Tiendas:Demostracion"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTiendas(config, new EntornoProduccion()));
    }

    [Fact]
    public async Task Api_catalogo_aisla_tienda_y_omite_estados_privados()
    {
        await using var factory = new ApiTiendasFactory();
        using var client = Cliente(factory);
        var texto = await client.GetStringAsync("/api/tiendas/fruteria-el-sol/productos?tamano=100&idNegocio=1002");
        using var json = System.Text.Json.JsonDocument.Parse(texto);
        Assert.Equal(1001, json.RootElement.GetProperty("tienda").GetProperty("idNegocio").GetInt64());
        var pagina = json.RootElement.GetProperty("productos");
        Assert.Equal(15, pagina.GetProperty("total").GetInt32());
        Assert.All(pagina.GetProperty("items").EnumerateArray(),
            p => Assert.InRange(p.GetProperty("id").GetInt64(), 100101L, 100115L));
        Assert.DoesNotContain("reservado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Arroz extra", texto);
    }

    [Fact]
    public async Task Mvc_catalogo_conserva_todos_los_filtros_al_paginar()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        var html = await client.GetStringAsync("/tiendas/fruteria-el-sol?categoriaId=1&q=a&orden=precio-desc&tamano=2");
        var enlace = Regex.Matches(html, "href=\"([^\"]+)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Single(url => url.Contains("pagina=2", StringComparison.Ordinal));
        var query = QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, enlace).Query);
        Assert.Equal("1", query["categoriaId"].ToString());
        Assert.Equal("a", query["q"].ToString());
        Assert.Equal("precio-desc", query["orden"].ToString());
        Assert.Equal("2", query["tamano"].ToString());
        Assert.EndsWith("#catalogo", enlace);
        using var segunda = await client.GetAsync(enlace);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Contains("Agregar al carrito", await segunda.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/tiendas/fruteria-el-sol/productos?orden=fecha", HttpStatusCode.BadRequest)]
    [InlineData("/api/tiendas/fruteria-el-sol/productos?categoriaId=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/tiendas/tienda-oculta/productos", HttpStatusCode.NotFound)]
    [InlineData("/api/tiendas/tienda-suspendida/productos", HttpStatusCode.NotFound)]
    [InlineData("/api/tiendas/inexistente/productos", HttpStatusCode.NotFound)]
    public async Task Api_catalogo_valida_filtros_y_visibilidad(string ruta, HttpStatusCode estado)
    {
        await using var factory = new ApiTiendasFactory();
        using var client = Cliente(factory);
        using var respuesta = await client.GetAsync(ruta);
        Assert.Equal(estado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Mvc_catalogo_muestra_estado_vacio_para_busqueda_sin_coincidencias()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        var html = await client.GetStringAsync("/tiendas/fruteria-el-sol?q=no-hay-este-producto");
        Assert.Contains("No hay productos para mostrar", html);
        Assert.DoesNotContain("tp-producto-cuerpo", html);
    }

    [Fact]
    public async Task Ficha_mvc_muestra_solo_puestos_vigentes()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        var html = await client.GetStringAsync("/tiendas/fruteria-el-sol");
        Assert.Contains("Puesto A-101", html);
        Assert.Contains("Puesto A-102", html);
        Assert.DoesNotContain("VENCIDO", html);
        Assert.DoesNotContain("FUTURO", html);
        Assert.DoesNotContain("NO-HABILITADO", html);

        var sinAsignacion = WebUtility.HtmlDecode(await client.GetStringAsync("/tiendas/abarrotes-lima"));
        Assert.Contains("Ubicación de puestos por confirmar", sinAsignacion);
        Assert.DoesNotContain("Puesto A-101", sinAsignacion);
    }

    [Fact]
    public async Task Ficha_api_publica_ubicaciones_sin_direccion_fiscal_ni_fechas_internas()
    {
        await using var factory = new ApiTiendasFactory();
        using var client = Cliente(factory);
        var texto = await client.GetStringAsync("/api/tiendas/fruteria-el-sol");
        using var json = System.Text.Json.JsonDocument.Parse(texto);
        var ubicaciones = json.RootElement.GetProperty("ubicaciones");
        Assert.Equal(2, ubicaciones.GetArrayLength());
        Assert.Equal("A-101", ubicaciones[0].GetProperty("puesto").GetString());
        Assert.DoesNotContain("direccionFiscal", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idPuesto", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"desde\"", texto);
        Assert.DoesNotContain("\"hasta\"", texto);
    }

    private static HttpClient Cliente<T>(WebApplicationFactory<T> factory) where T : class =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private sealed class WebFactory(bool demostracion = true, bool textoDePrueba = false)
        : WebApplicationFactory<PortalMercado.Web.Controllers.TiendasController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:PortalMercado",
                "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            builder.UseSetting("Tiendas:Demostracion", demostracion.ToString());
            if (textoDePrueba)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IRepositorioTiendas>();
                    services.AddSingleton<IRepositorioTiendas>(new RepositorioConTexto());
                });
            }
        }
    }

    private sealed class ApiTiendasFactory(bool demostracion = true)
        : WebApplicationFactory<PortalMercado.API.Controllers.TiendasController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:PortalMercado",
                "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            builder.UseSetting("Tiendas:Demostracion", demostracion.ToString());
        }
    }

    private sealed class RepositorioConTexto : IRepositorioTiendas
    {
        private readonly RepositorioTiendasDemostracion origen = new();

        public Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct) => origen.OpcionesPublicasAsync(ct);
        public Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct) =>
            origen.ListarPublicasAsync(filtro, ct);

        public async Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct) =>
            (await origen.BuscarPorSlugAsync(slug, ct))
                .Select(t => t with { NombreComercial = "<script>PRUEBA</script>" }).ToArray();
    }

    private sealed class EntornoProduccion : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

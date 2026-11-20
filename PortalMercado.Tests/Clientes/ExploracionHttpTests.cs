using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Exploracion;

namespace PortalMercado.Tests.Clientes;

public sealed class ExploracionHttpTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/explorar")]
    public async Task Portada_publica_abre_sin_cuenta_con_catalogo_y_busqueda_global(string ruta)
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        var respuesta = await c.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Productos de tus tiendas favoritas", html);
        Assert.Contains("Explorar tiendas", html); Assert.Contains("Frutería El Sol", html);
        Assert.Contains("action=\"/explorar/buscar\"", html);
        Assert.Contains("/css/explorar.css", html); Assert.Contains("/js/explorar.js", html);
        Assert.Contains("Saltar al contenido", html); Assert.Contains("name=\"viewport\"", html);
        Assert.DoesNotContain("id=\"menu-lateral\"", html);
        Assert.Contains("no-store", respuesta.Headers.CacheControl!.ToString());
    }
    [Theory]
    [InlineData("/explorar/buscar")]
    [InlineData("/explorar/buscar?q=Tomate&tipo=productos")]
    [InlineData("/explorar/buscar?tipo=tiendas")]
    [InlineData("/explorar/buscar?tipo=categorias")]
    public async Task Buscador_es_publico_y_no_crea_un_flujo_de_compra(string ruta)
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        var respuesta = await c.GetAsync(ruta); var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Encuentra lo que necesitas", html);
        Assert.DoesNotContain("checkout", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pago centralizado", html, StringComparison.OrdinalIgnoreCase);
        if (ruta.Contains("productos")) Assert.Contains("disabled", html);
    }
    [Fact]
    public async Task Paginacion_conserva_mercado_categoria_busqueda_tipo_orden_y_tamano()
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/explorar/buscar?q=Frutas&mercadoId=100&categoriaId=1&tipo=productos&orden=precio-asc&tamano=1"));
        Assert.Contains("Siguiente", html);
        foreach (var filtro in new[] { "q=Frutas", "mercadoId=100", "categoriaId=1", "tipo=productos", "orden=precio-asc", "tamano=1", "pagina=2" })
            Assert.Contains(filtro, html);
    }
    [Fact]
    public async Task Texto_de_consulta_y_resultados_se_codifica_como_html()
    {
        await using var f = new WebFactory(texto: true); using var c = Cliente(f);
        var html = await c.GetStringAsync("/explorar/buscar?tipo=productos&q=" + Uri.EscapeDataString("<script>PRUEBA</script>"));
        Assert.DoesNotContain("<script>PRUEBA</script>", html);
        Assert.Contains("&lt;script&gt;PRUEBA&lt;/script&gt;", html);
    }
    [Fact]
    public async Task Sin_coincidencias_informa_estado_vacio()
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/explorar/buscar?q=no-existe-xyz"));
        Assert.Contains("No encontramos resultados", html); Assert.Contains("Ver todo", html);
    }
    [Theory]
    [InlineData("tipo=usuarios")]
    [InlineData("mercadoId=abc")]
    [InlineData("pagina=0")]
    [InlineData("tamano=101")]
    [InlineData("orden=precio-desc&tipo=tiendas")]
    public async Task Parametros_invalidos_producen_400_controlado(string query)
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        var respuesta = await c.GetAsync("/explorar/buscar?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Volver al buscador", await respuesta.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Sin_proveedor_la_portada_muestra_aviso_y_el_buscador_responde_503()
    {
        await using var f = new WebFactory(demo: false); using var c = Cliente(f);
        var portada = await c.GetStringAsync("/explorar");
        Assert.Contains("No podemos cargar", portada); Assert.DoesNotContain("Fruter", portada);
        var respuesta = await c.GetAsync("/explorar/buscar?q=arroz");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ConnectionStrings", html); Assert.DoesNotContain("SqlException", html);
    }
    [Fact]
    public async Task Api_publica_devuelve_resultados_y_mercados_sin_contactos()
    {
        await using var f = new ApiFactory(); using var c = Cliente(f);
        var respuesta = await c.GetAsync("/api/publico/buscar?q=Tomate&tipo=productos");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(texto);
        Assert.True(json.RootElement.GetProperty("resultados").GetProperty("total").GetInt32() > 0);
        foreach (var privado in new[] { "password", "telefono", "correo", "direccionFiscal", "idCliente" })
            Assert.DoesNotContain(privado, texto, StringComparison.OrdinalIgnoreCase);
        using var mercados = JsonDocument.Parse(await c.GetStringAsync("/api/publico/mercados"));
        Assert.Equal(2, mercados.RootElement.GetArrayLength());
    }
    [Theory]
    [InlineData("pagina=-1")]
    [InlineData("mercadoId=invalido")]
    [InlineData("tipo=clientes")]
    public async Task Api_valida_parametros_sin_autenticacion(string query)
    {
        await using var f = new ApiFactory(); using var c = Cliente(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/publico/buscar?" + query)).StatusCode);
    }
    private static HttpClient Cliente<T>(WebApplicationFactory<T> f) where T : class => f.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private sealed class WebFactory(bool demo = true, bool texto = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", demo.ToString());
            if (texto) b.ConfigureTestServices(s =>
            {
                var repo = new ExploracionRepoPrueba();
                repo.Datos.Productos[0] = repo.Datos.Productos[0] with { Producto = repo.Datos.Productos[0].Producto with { Nombre = "<script>PRUEBA</script>" } };
                s.RemoveAll<IRepositorioExploracionPublica>(); s.AddSingleton<IRepositorioExploracionPublica>(repo);
            });
        }
    }
    private sealed class ApiFactory : WebApplicationFactory<PortalMercado.API.Controllers.PublicoController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "true");
        }
    }
}

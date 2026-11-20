using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class ConsentimientosComprasWebTests
{
    [Theory]
    [InlineData("/Consentimientos")]
    [InlineData("/Compras/Preparar?returnUrl=%2Fexplorar%2Fbuscar%3Fq%3DMango")]
    public async Task Rutas_privadas_exigen_sesion_y_conservan_destino(string ruta)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Cuenta/Login", r.Headers.Location!.OriginalString);
        Assert.Contains("ReturnUrl", r.Headers.Location.OriginalString);
    }
    [Fact]
    public async Task Historial_es_privado_y_autorizar_requiere_accion_explicita()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/Consentimientos"); var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Historial", html); Assert.Contains("UTC", html); Assert.Contains("TERMINOS-V1", html);
        Assert.Contains("Autorizar promociones", html); Assert.DoesNotContain("checked=", html);
        Assert.Equal(0, f.Repo.Escrituras); Assert.Equal(77, f.Repo.Usuario);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
    }
    [Theory]
    [InlineData("Autorizar", true)]
    [InlineData("Retirar", false)]
    public async Task Cambio_se_registra_solo_para_usuario_autenticado(string accion, bool esperado)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var token = Token(await c.GetStringAsync("/Consentimientos"));
        var r = await c.PostAsync("/Consentimientos/" + accion, Form(token,
            ("version", "test-1"), ("IdUsuario", "999"), ("Tipo", "PRIVACIDAD"), ("Aceptado", (!esperado).ToString())));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(1, f.Repo.Escrituras); Assert.Equal(77, f.Repo.Usuario);
        Assert.Equal("PROMOCIONES", f.Repo.Guardado!.Tipo); Assert.Equal(esperado, f.Repo.Guardado.Aceptado);
        Assert.Equal(2, f.Repo.Datos.Length);
    }
    [Fact]
    public async Task POST_sin_csrf_no_guarda_preferencia()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsync("/Consentimientos/Autorizar", Form(null, ("version", "test-1")));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Repo.Escrituras);
    }
    [Fact]
    public async Task Version_antigua_no_puede_autorizarse_como_vigente()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsync("/Consentimientos/Autorizar", Form(Token(await c.GetStringAsync("/Consentimientos")), ("version", "anterior")));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Repo.Escrituras);
    }
    [Fact]
    public async Task Retiro_sigue_ofrecido_sin_documentos_actuales_y_no_ofrece_opt_in()
    {
        await using var f = new Factory(documentos: false); using var c = await Entrar(f);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Consentimientos"));
        Assert.DoesNotContain(">Autorizar promociones</button>", html);
        Assert.Contains("Retirar autorización", html);
        var r = await c.PostAsync("/Consentimientos/Retirar", Form(Token(html), ("version", "test-1")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.False(f.Repo.Guardado!.Aceptado);
    }
    [Fact]
    public async Task Proveedor_pendiente_muestra_503_sin_historial_inventado()
    {
        await using var f = new Factory(pendiente: true); using var c = await Entrar(f);
        var r = await c.GetAsync("/Consentimientos");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.DoesNotContain("TERMINOS-V1", await r.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("/explorar/buscar?q=Mango", "/explorar/buscar?q=Mango")]
    [InlineData("https://externo.invalid", "/tiendas")]
    [InlineData("//externo.invalid", "/tiendas")]
    public async Task Entrada_futura_es_informativa_y_vuelve_solo_a_destino_local(string retorno, string esperado)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/Compras/Preparar?returnUrl=" + Uri.EscapeDataString(retorno));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.Contains("href=\"" + esperado + "\"", html);
        Assert.Contains("Las compras estarán disponibles próximamente", html);
        Assert.DoesNotContain("name=\"cantidad\"", html);
        Assert.Equal(0, f.Repo.Escrituras);
    }
    [Fact]
    public async Task Entrada_futura_se_conserva_despues_de_login()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var destino = "/Compras/Preparar?returnUrl=%2Fexplorar%2Fbuscar%3Fq%3DMango";
        var r = await c.PostAsync("/Cuenta/Login", Form(Token(await c.GetStringAsync("/Cuenta/Login")),
            ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!"), ("returnUrl", destino)));
        Assert.Equal(destino, r.Headers.Location!.OriginalString);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync(r.Headers.Location));
        Assert.Contains("href=\"/explorar/buscar?q=Mango\"", html);
    }
    private static async Task<HttpClient> Entrar(Factory f)
    {
        var c = f.Cliente();
        var r = await c.PostAsync("/Cuenta/Login", Form(Token(await c.GetStringAsync("/Cuenta/Login")),
            ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); return c;
    }
    private static string Token(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success); return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string? token, params (string Clave, string Valor)[] campos)
    {
        var datos = new Dictionary<string,string>();
        foreach (var (clave, valor) in campos) datos[clave] = valor;
        if (token is not null) datos["__RequestVerificationToken"] = token;
        return new(datos);
    }
    private sealed class Factory(bool documentos = true, bool pendiente = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public ConsentimientosClientePrueba Repo { get; } = new()
        {
            Datos = [new("TERMINOS", "TERMINOS-V1", true, RelojClientes.Fecha.AddDays(-1)),
                new("PROMOCIONES", "test-1", true, RelojClientes.Fecha)]
        };
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "false");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(new ControlledAuth());
                s.RemoveAll<IRepositorioPerfilCliente>();
                s.AddSingleton<IRepositorioPerfilCliente>(new PerfilClientesPrueba { Perfil = ClientesPrueba.Perfil(usuario: 77) });
                s.RemoveAll<DocumentacionRegistro>(); s.RemoveAll<DocumentosClienteVigentes>();
                var doc = new DocumentoRegistro("test-1", documentos ? "Texto exclusivo de prueba." : "");
                s.AddSingleton(new DocumentacionRegistro(doc, doc, documentos ? doc : null));
                s.AddSingleton(new DocumentosClienteVigentes("test-1", "test-1", documentos ? "test-1" : null));
                if (!pendiente)
                { s.RemoveAll<IRepositorioConsentimientosCliente>(); s.AddSingleton<IRepositorioConsentimientosCliente>(Repo); }
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

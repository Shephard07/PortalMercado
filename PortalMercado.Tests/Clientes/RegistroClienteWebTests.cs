using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class RegistroClienteWebTests
{
    [Fact]
    public async Task Sin_contrato_aprobado_responde_503_y_permiso_de_explorar_sin_capturar_password()
    {
        await using var f = new Factory(proveedor: false); using var c = f.Cliente();
        var r = await c.GetAsync("/Registro");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.Contains("registro no está disponible", html);
        Assert.DoesNotContain("name=\"Password\"", html);
        Assert.Contains("Seguir explorando", html);
    }
    [Fact]
    public async Task Sin_textos_documentados_no_se_ofrece_el_formulario()
    {
        await using var f = new Factory(documentos: false); using var c = f.Cliente();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/Registro")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Registro/Documento?tipo=terminos")).StatusCode);
    }
    [Fact]
    public async Task Formulario_publico_muestra_documentos_y_promociones_sin_preseleccionar()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.GetAsync("/Registro?returnUrl=%2Fexplorar%2Fbuscar%3Fq%3DMango");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("autocomplete=\"new-password\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.DoesNotContain("checked=", html);
        Assert.Contains("name=\"VersionPrivacidad\"", html);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
        Assert.Contains("/explorar/buscar?q=Mango", WebUtility.HtmlDecode(html));
    }
    [Fact]
    public async Task Alta_sin_csrf_no_alcanza_el_repositorio()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Registro", Form())).StatusCode);
        Assert.Equal(0, f.Repo.Llamadas);
    }
    [Theory]
    [InlineData("AceptaTerminos", "false")]
    [InlineData("AceptaPrivacidad", "false")]
    [InlineData("VersionTerminos", "anterior")]
    [InlineData("VersionPrivacidad", "anterior")]
    [InlineData("Password", "solominusculas")]
    [InlineData("Correo", "invalido")]
    public async Task Alta_invalida_no_guarda_ni_repite_password(string campo, string valor)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var token = Token(await c.GetStringAsync("/Registro"));
        var r = await c.PostAsync("/Registro", Form(token, (campo, valor)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PruebaSegura123!", html);
        Assert.Equal(0, f.Repo.Llamadas);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Alta_usa_un_solo_comando_y_no_inicia_sesion(bool confirmar)
    {
        await using var f = new Factory(confirmar: confirmar); using var c = f.Cliente();
        var token = Token(await c.GetStringAsync("/Registro"));
        var r = await c.PostAsync("/Registro", Form(token, ("IdUsuario", "999"), ("Rol", "ADMIN_GENERAL")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(1, f.Repo.Llamadas);
        Assert.Equal(0, f.Repo.Alta!.Operacion.IdUsuario);
        Assert.DoesNotContain("PruebaSegura123!", f.Repo.Alta.PasswordHash);
        Assert.False(Assert.Single(f.Repo.Alta.Consentimientos, x => x.Tipo == "PROMOCIONES").Aceptado);
        Assert.DoesNotContain(r.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-PortalMercado.MVC="));
        var html = WebUtility.HtmlDecode(await c.GetStringAsync(r.Headers.Location!));
        Assert.Contains(confirmar ? "Revisa tu correo" : "Ya puedes iniciar sesión", html);
        // La página de éxito solo puede consumirse una vez después de la operación real.
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync(r.Headers.Location!)).StatusCode);
    }
    [Fact]
    public async Task Duplicado_muestra_rechazo_generico_sin_datos_de_cuenta()
    {
        await using var f = new Factory(duplicado: true); using var c = f.Cliente();
        var token = Token(await c.GetStringAsync("/Registro"));
        var r = await c.PostAsync("/Registro", Form(token));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.Contains(RegistroClienteServicio.RechazoGenerico, html);
        Assert.DoesNotContain("ya existe", html); Assert.DoesNotContain("PruebaSegura123!", html);
    }
    [Fact]
    public async Task Falla_de_proveedor_no_muestra_exito()
    {
        await using var f = new Factory(fallo: true); using var c = f.Cliente();
        var token = Token(await c.GetStringAsync("/Registro"));
        var r = await c.PostAsync("/Registro", Form(token));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Null(r.Headers.Location);
    }
    [Fact]
    public async Task Pagina_de_exito_no_puede_inventarse_con_query_string()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Registro/Completado?confirmacion=false")).StatusCode);
    }
    [Theory]
    [InlineData("terminos")]
    [InlineData("privacidad")]
    [InlineData("promociones")]
    public async Task Documentos_se_muestran_como_texto_y_con_version(string tipo)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var html = await c.GetStringAsync("/Registro/Documento?tipo=" + tipo);
        Assert.Contains("test-1", html); Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>contenido", html);
    }
    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string? token = null, params (string Clave, string Valor)[] cambios)
    {
        var datos = new Dictionary<string, string>
        {
            ["Nombre"] = "Comprador prueba", ["Correo"] = "nuevo@example.invalid",
            ["Password"] = "PruebaSegura123!", ["ConfirmarPassword"] = "PruebaSegura123!",
            ["AceptaTerminos"] = "true", ["AceptaPrivacidad"] = "true",
            ["VersionTerminos"] = "test-1", ["VersionPrivacidad"] = "test-1", ["VersionPromociones"] = "test-1",
            ["returnUrl"] = "/explorar/buscar?q=Mango"
        };
        if (token is not null) datos["__RequestVerificationToken"] = token;
        foreach (var cambio in cambios) datos[cambio.Clave] = cambio.Valor;
        return new(datos);
    }
    private sealed class Repo(bool confirmar, bool duplicado, bool fallo) : IRepositorioRegistroCliente
    {
        public int Llamadas { get; private set; }
        public RegistroClienteAlta? Alta { get; private set; }
        public Task<RegistroClientePersistido> RegistrarAsync(RegistroClienteAlta alta, CancellationToken ct)
        {
            Llamadas++; Alta = alta;
            if (fallo) throw new AccessFailure(503, "Servicio no disponible.");
            return Task.FromResult(new RegistroClientePersistido(duplicado ? EstadoRegistroCliente.NoDisponible : EstadoRegistroCliente.Creado, confirmar));
        }
    }
    private sealed class Factory(bool proveedor = true, bool documentos = true, bool confirmar = false,
        bool duplicado = false, bool fallo = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public Repo Repo { get; } = new(confirmar, duplicado, fallo);
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "true");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<DocumentacionRegistro>(); s.RemoveAll<DocumentosClienteVigentes>();
                var doc = new DocumentoRegistro("test-1", documentos ? "<script>contenido de prueba</script>" : "");
                s.AddSingleton(new DocumentacionRegistro(doc, doc, doc));
                s.AddSingleton(new DocumentosClienteVigentes("test-1", "test-1", "test-1"));
                if (proveedor) { s.RemoveAll<IRepositorioRegistroCliente>(); s.AddSingleton<IRepositorioRegistroCliente>(Repo); }
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

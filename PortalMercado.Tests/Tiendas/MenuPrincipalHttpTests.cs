using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Tiendas;

public sealed class MenuPrincipalHttpTests
{
    [Fact]
    public async Task Visitante_encuentra_tiendas_y_login_sin_diagnostico_tecnico()
    {
        await using var factory = new Factory(); using var client = Cliente(factory);
        var respuesta = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Explorar tiendas", html);
        Assert.DoesNotContain("id=\"menu-lateral\"", html);
        Assert.DoesNotContain("Estado técnico", html);
        Assert.DoesNotContain("SQL Server", html);
        Assert.DoesNotContain("Versión", html);
    }

    [Fact]
    public async Task Login_abre_panel_con_menu_y_codifica_nombre_del_usuario()
    {
        await using var factory = new Factory(); using var client = Cliente(factory);
        var destino = await Login(client);
        Assert.Contains(destino, new[] { "/", "/Home", "/Home/Index" });
        var html = await client.GetStringAsync(destino);
        Assert.Contains("id=\"menu-lateral\"", html);
        Assert.Contains("aria-controls=\"menu-lateral\"", html);
        Assert.Contains("/css/menu-principal.css", html);
        Assert.Contains("/js/menu-principal.js", html);
        Assert.Contains("Tu espacio de trabajo", html);
        Assert.Contains("&lt;script&gt;NOMBRE&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>NOMBRE</script>", html);
        Assert.DoesNotContain("Pedidos del día", html);
        Assert.DoesNotContain("Ventas del día", html);
        Assert.Contains("/Productos", html);
        Assert.Contains("/mis-tiendas", html);
        Assert.DoesNotContain("href=\"/Usuarios", html);
        Assert.Contains("no-store", (await client.GetAsync("/")).Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Busqueda_filtra_accesos_permitidos_y_muestra_estado_vacio()
    {
        await using var factory = new Factory(); using var client = Cliente(factory); await Login(client);
        var html = await client.GetStringAsync("/?q=productos");
        var tarjetas = Tarjetas(html);
        Assert.Contains("Productos", tarjetas);
        Assert.DoesNotContain("Mi perfil", tarjetas);
        var vacio = await client.GetStringAsync("/?q=usuarios");
        Assert.Contains("No encontramos módulos disponibles", WebUtility.HtmlDecode(vacio));
        Assert.DoesNotContain("href=\"/Usuarios", vacio);
        Assert.Contains("Ver todos", vacio);
    }

    [Fact]
    public async Task Permiso_retirado_desaparece_del_menu_sin_iniciar_sesion_de_nuevo()
    {
        await using var factory = new Factory(); using var client = Cliente(factory); await Login(client);
        Assert.Contains("/Productos", await client.GetStringAsync("/"));
        factory.Auth.Permisos = [];
        var html = await client.GetStringAsync("/");
        Assert.DoesNotContain("href=\"/Productos", html);
        Assert.DoesNotContain("href=\"/mis-tiendas", html);
        Assert.Contains("Mi perfil", html);
    }

    [Fact]
    public async Task Menu_de_cuenta_conserva_cierre_de_sesion_por_post_y_csrf()
    {
        await using var factory = new Factory(); using var client = Cliente(factory); await Login(client);
        var html = await client.GetStringAsync("/");
        Assert.Contains("action=\"/Cuenta/Logout\"", html);
        Assert.NotEmpty(Token(html));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Cuenta/Logout", new FormUrlEncodedContent([]))).StatusCode);
        var respuesta = await client.PostAsync("/Cuenta/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.DoesNotContain("id=\"menu-lateral\"", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Portal_publico_ofrece_regreso_al_panel_autenticado()
    {
        await using var factory = new Factory(); using var client = Cliente(factory); await Login(client);
        var html = await client.GetStringAsync("/tiendas");
        Assert.Contains("Mi panel", html);
        Assert.DoesNotContain("id=\"menu-lateral\"", html);
        Assert.DoesNotContain("menu-principal.css", html);
    }

    [Fact]
    public async Task Busqueda_muy_larga_devuelve_error_controlado()
    {
        await using var factory = new Factory(); using var client = Cliente(factory); await Login(client);
        var respuesta = await client.GetAsync("/?q=" + new string('x', 81));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("80 caracteres", await respuesta.Content.ReadAsStringAsync());
    }

    private static string Tarjetas(string html)
    {
        var indice = html.IndexOf("pm-accesos-cuadricula", StringComparison.Ordinal);
        return indice >= 0 ? html[indice..html.IndexOf("</section>", indice, StringComparison.Ordinal)] : "";
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<string> Login(HttpClient c)
    {
        var html = await c.GetStringAsync("/Cuenta/Login");
        var respuesta = await c.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Correo"] = "v@example.invalid", ["Password"] = "PruebaSegura123!"
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return respuesta.Headers.Location!.ToString();
    }
    private static HttpClient Cliente(Factory f) => f.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private sealed class Factory : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public AutenticacionMenu Auth { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.UseSetting("Tiendas:Demostracion", "true");
            b.ConfigureTestServices(s => { s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(Auth); });
        }
    }
    private sealed class AutenticacionMenu : IAuthService
    {
        private readonly ControlledAuth origen = new();
        public string[] Permisos { get; set; } = ["PRODUCTO_VER", "PRODUCTO_GESTIONAR"];
        private AuthProfile Perfil(AuthProfile perfil) => perfil with { Nombre = "<script>NOMBRE</script>", Permisos = Permisos };
        public async Task<LoginOutcome?> LoginAsync(LoginRequest r, string host, string correlation, CancellationToken ct)
        {
            var resultado = await origen.LoginAsync(r, host, correlation, ct);
            return resultado?.Session is { } sesion ? new LoginOutcome(sesion with { Profile = Perfil(sesion.Profile) }, null) : resultado;
        }
        public async Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct)
        {
            var perfil = await origen.ValidateAsync(credential, host, ct);
            return perfil is null ? null : Perfil(perfil);
        }
        public Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct) =>
            origen.LogoutAsync(credential, host, correlationId, ct);
    }
}

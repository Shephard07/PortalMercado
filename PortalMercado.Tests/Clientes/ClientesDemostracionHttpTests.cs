using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PortalMercado.Tests.Clientes;

public sealed class ClientesDemostracionHttpTests
{
    [Fact]
    public async Task Web_registra_entra_edita_perfil_y_gestiona_direcciones_sin_conectar_SQL()
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        await RegistrarEntrar(c, "cliente1@example.invalid");
        var perfil = await c.GetStringAsync("/Cliente/Perfil");
        Assert.Contains("Clientes en demostraci", WebUtility.HtmlDecode(perfil));
        var guardar = await c.PostAsync("/Cliente/Perfil", Form(Token(perfil), ("Datos.Apellidos", "Prueba"), ("Datos.Celular", "000000001")));
        Assert.Equal(HttpStatusCode.Redirect, guardar.StatusCode);
        Assert.Contains("value=\"Prueba\"", await c.GetStringAsync("/Cliente/Perfil"));
        var crear = await c.PostAsync("/Cliente/Guardar", Form(Token(await c.GetStringAsync("/Cliente/Nueva")), ("Etiqueta", "Casa ficticia"), ("Direccion", "Calle ficticia 123")));
        Assert.Equal(HttpStatusCode.Redirect, crear.StatusCode);
        var lista = await c.GetStringAsync("/Cliente/Direcciones"); Assert.Contains("Calle ficticia 123", lista);
        var principal = await c.PostAsync("/Cliente/Principal", Form(Token(lista), ("id", "1")));
        Assert.Equal(HttpStatusCode.Redirect, principal.StatusCode);
        Assert.Contains("Dirección principal", WebUtility.HtmlDecode(await c.GetStringAsync("/Cliente/Direcciones")));
        var editar = await c.PostAsync("/Cliente/Guardar", Form(Token(await c.GetStringAsync("/Cliente/Editar?id=1")), ("id", "1"), ("Direccion", "Calle ficticia 456")));
        Assert.Equal(HttpStatusCode.Redirect, editar.StatusCode);
        Assert.Contains("Calle ficticia 456", await c.GetStringAsync("/Cliente/Direcciones"));
        var eliminar = await c.PostAsync("/Cliente/ConfirmarEliminar", Form(Token(await c.GetStringAsync("/Cliente/Eliminar?id=1")), ("id", "1")));
        Assert.Equal(HttpStatusCode.Redirect, eliminar.StatusCode);
        Assert.DoesNotContain("Calle ficticia 456", await c.GetStringAsync("/Cliente/Direcciones"));
    }
    [Fact]
    public async Task Web_promociones_son_opcionales_y_retiro_conserva_historial()
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        await RegistrarEntrar(c, "cliente1@example.invalid");
        var html = await c.GetStringAsync("/Consentimientos");
        Assert.Contains("No autorizado", html); // No se seleccionó marketing al registrar.
        var autorizar = await c.PostAsync("/Consentimientos/Autorizar", Form(Token(html), ("version", "demo-promociones-1")));
        Assert.Equal(HttpStatusCode.Redirect, autorizar.StatusCode);
        html = await c.GetStringAsync("/Consentimientos"); Assert.Contains("Retirar", html);
        var retirar = await c.PostAsync("/Consentimientos/Retirar", Form(Token(html), ("version", "demo-promociones-1")));
        Assert.Equal(HttpStatusCode.Redirect, retirar.StatusCode);
        html = WebUtility.HtmlDecode(await c.GetStringAsync("/Consentimientos"));
        Assert.Contains("Aceptado", html); Assert.Contains("No autorizado", html);
        Assert.Equal(3, Regex.Matches(html, "Versión: demo-promociones-1").Count);
    }
    [Fact]
    public async Task Dos_clientes_no_comparten_direcciones_ni_pueden_editar_una_ajena()
    {
        await using var f = new WebFactory(); using var a = Cliente(f); using var b = Cliente(f);
        await RegistrarEntrar(a, "cliente1@example.invalid"); await RegistrarEntrar(b, "cliente2@example.invalid");
        await a.PostAsync("/Cliente/Guardar", Form(Token(await a.GetStringAsync("/Cliente/Nueva")), ("Direccion", "Solo cliente uno")));
        Assert.DoesNotContain("Solo cliente uno", await b.GetStringAsync("/Cliente/Direcciones"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync("/Cliente/Editar?id=1")).StatusCode);
        var r = await b.PostAsync("/Cliente/Principal", Form(Token(await b.GetStringAsync("/Cliente/Nueva")), ("id", "1")));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
    [Fact]
    public async Task Logout_revoca_sesion_de_demostracion()
    {
        await using var f = new WebFactory(); using var c = Cliente(f);
        await RegistrarEntrar(c, "cliente1@example.invalid");
        var cuenta = await c.GetStringAsync("/Cuenta/Perfil");
        var r = await c.PostAsync("/Cuenta/Logout", Form(Token(cuenta)));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Cliente/Perfil")).StatusCode);
    }
    [Fact]
    public async Task API_simulada_registra_y_guarda_por_cookies_con_aviso_en_cabecera()
    {
        await using var f = new ApiFactory(); using var c = Cliente(f);
        await Csrf(c);
        var registro = await c.PostAsJsonAsync("/api/clientes/registro", new
        {
            nombre = "Cliente ficticio", correo = "cliente1@example.invalid", password = "Demo-Prueba2026!", confirmarPassword = "Demo-Prueba2026!",
            aceptaTerminos = true, aceptaPrivacidad = true, versionTerminos = "demo-terminos-1", versionPrivacidad = "demo-privacidad-1"
        });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        Assert.Equal("Demostracion-temporal", Assert.Single(registro.Headers.GetValues("X-PortalMercado-Clientes-Modo")));
        var login = await c.PostAsJsonAsync("/api/auth/login", new { correo = "cliente1@example.invalid", password = "Demo-Prueba2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Csrf(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/mi-perfil")).StatusCode);
        var direccion = await c.PostAsJsonAsync("/api/mis-direcciones", new { direccion = "Dirección ficticia API" });
        Assert.Equal(HttpStatusCode.Created, direccion.StatusCode);
        var leer = await c.GetAsync(direccion.Headers.Location!); Assert.Equal(HttpStatusCode.OK, leer.StatusCode);
        Assert.Contains("ficticia API", WebUtility.HtmlDecode(await leer.Content.ReadAsStringAsync()));
    }
    private static async Task RegistrarEntrar(HttpClient c, string correo)
    {
        var html = await c.GetStringAsync("/Registro");
        Assert.Contains("demo-terminos-1", html);
        var alta = await c.PostAsync("/Registro", Form(Token(html),
            ("Nombre", "Cliente ficticio"), ("Correo", correo), ("Password", "Demo-Prueba2026!"), ("ConfirmarPassword", "Demo-Prueba2026!"),
            ("AceptaTerminos", "true"), ("AceptaPrivacidad", "true"), ("VersionTerminos", "demo-terminos-1"),
            ("VersionPrivacidad", "demo-privacidad-1"), ("VersionPromociones", "demo-promociones-1")));
        Assert.Equal(HttpStatusCode.Redirect, alta.StatusCode);
        Assert.Contains("Registro completado", await c.GetStringAsync(alta.Headers.Location!));
        var loginHtml = await c.GetStringAsync("/Cuenta/Login");
        var login = await c.PostAsync("/Cuenta/Login", Form(Token(loginHtml), ("Correo", correo), ("Password", "Demo-Prueba2026!"), ("returnUrl", "/Cliente/Perfil")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Cliente/Perfil", login.Headers.Location!.OriginalString);
    }
    private static async Task Csrf(HttpClient c)
    {
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    private static string Token(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success); return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string token, params (string Clave, string Valor)[] campos) =>
        new(campos.Select(x => new KeyValuePair<string,string>(x.Clave, x.Valor)).Append(new KeyValuePair<string,string>("__RequestVerificationToken", token)));
    private static HttpClient Cliente<T>(WebApplicationFactory<T> f) where T : class => f.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private sealed class WebFactory : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b) => Configurar(b);
    }
    private sealed class ApiFactory : WebApplicationFactory<PortalMercado.API.Controllers.AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b) => Configurar(b);
    }
    private static void Configurar(IWebHostBuilder b)
    {
        b.UseEnvironment("Development"); b.UseSetting("Clientes:Demostracion", "true");
        b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "false");
        // Conexión deliberadamente inaccesible: los flujos de clientes simulados no deben consultarla.
        b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1,1;Database=Inaccesible;Integrated Security=True;Connect Timeout=1;");
    }
}

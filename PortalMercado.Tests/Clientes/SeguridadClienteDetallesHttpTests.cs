using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

public sealed class SeguridadClienteDetallesHttpTests
{
    [Fact]
    public async Task Recuperar_y_restablecer_reutilizan_formularios_y_rechazan_enlace_usado()
    {
        await using var f = new Factory(); using var c = Cliente(f); await Alta(c);
        await c.PostAsync("/Cuenta/Recuperar", Form(Token(await c.GetStringAsync("/Cuenta/Recuperar")), ("Correo", "cliente@example.invalid")));
        var link = f.Entrega.Reset!.PathAndQuery; var token = f.Entrega.Reset.Query.Split('=')[1];
        var html = await c.GetStringAsync(link);
        var r = await c.PostAsync("/Cuenta/Restablecer", Form(Token(html), ("Token", token), ("NewPassword", "Nueva-Prueba2026!"), ("ConfirmPassword", "Nueva-Prueba2026!")));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Contains("actualizada", await r.Content.ReadAsStringAsync());
        var segunda = await c.PostAsync("/Cuenta/Restablecer", Form(Token(await c.GetStringAsync(link)), ("Token", token), ("NewPassword", "Otra-Prueba2026!"), ("ConfirmPassword", "Otra-Prueba2026!")));
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
        var error = await segunda.Content.ReadAsStringAsync(); Assert.DoesNotContain("value=\"Otra-Prueba2026!\"", error);
    }
    [Fact]
    public async Task Cambiar_password_cierra_sesion_y_exige_entrar_con_la_nueva()
    {
        await using var f = new Factory(); using var c = Cliente(f); await Alta(c); await Login(c);
        var r = await c.PostAsync("/Cuenta/CambiarPassword", Form(Token(await c.GetStringAsync("/Cuenta/CambiarPassword")),
            ("CurrentPassword", "Demo-Prueba2026!"), ("NewPassword", "Nueva-Prueba2026!"), ("ConfirmPassword", "Nueva-Prueba2026!")));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Cliente/Perfil")).StatusCode);
        await Login(c, "cliente@example.invalid", "Nueva-Prueba2026!");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Cliente/Perfil")).StatusCode);
    }
    [Fact]
    public async Task Cambio_correo_requiere_csrf_confirmacion_POST_y_no_modifica_en_GET()
    {
        await using var f = new Factory(); using var c = Cliente(f); await Alta(c); await Login(c);
        var campos = new[] { ("Correo", "nuevo@example.invalid"), ("ConfirmarCorreo", "nuevo@example.invalid"), ("PasswordActual", "Demo-Prueba2026!") };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Cuenta/CambiarCorreo", new FormUrlEncodedContent(campos.Select(x => new KeyValuePair<string,string>(x.Item1,x.Item2))))).StatusCode);
        var r = await c.PostAsync("/Cuenta/CambiarCorreo", Form(Token(await c.GetStringAsync("/Cuenta/CambiarCorreo")), campos));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var enlace = Regex.Match(f.Entrega.Cuerpo!, @"https://localhost/Cuenta/ConfirmarCorreo\?token=([A-Za-z0-9_-]{43})"); Assert.True(enlace.Success);
        var html = await c.GetStringAsync(enlace.Value);
        var perfil = await c.GetStringAsync("/Cliente/Perfil"); Assert.Contains("cliente@example.invalid", perfil); Assert.DoesNotContain("nuevo@example.invalid", perfil);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/Cuenta/ConfirmarCorreo", new FormUrlEncodedContent(new Dictionary<string,string> { ["Token"] = enlace.Groups[1].Value }))).StatusCode);
        var confirmado = await c.PostAsync("/Cuenta/ConfirmarCorreo", Form(Token(html), ("Token", enlace.Groups[1].Value)));
        Assert.Equal(HttpStatusCode.OK, confirmado.StatusCode); Assert.Contains("Correo actualizado", await confirmado.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Cliente/Perfil")).StatusCode);
        await Login(c, "nuevo@example.invalid"); Assert.Contains("nuevo@example.invalid", await c.GetStringAsync("/Cliente/Perfil"));
    }
    [Fact]
    public async Task Errores_del_formulario_no_reflejan_password_y_anonimo_no_cambia_correo()
    {
        await using var f = new Factory(); using var c = Cliente(f);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Cuenta/CambiarCorreo")).StatusCode);
        await Alta(c); await Login(c);
        var r = await c.PostAsync("/Cuenta/CambiarCorreo", Form(Token(await c.GetStringAsync("/Cuenta/CambiarCorreo")),
            ("Correo", "nuevo@example.invalid"), ("ConfirmarCorreo", "otro@example.invalid"), ("PasswordActual", "Secreto-Actual2026!")));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.DoesNotContain("Secreto-Actual2026!", await r.Content.ReadAsStringAsync()); Assert.Null(f.Entrega.Cuerpo);
    }
    [Fact]
    public async Task Mensaje_de_cambio_utiliza_transporte_Pickup_existente_sin_publicar_enlace()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "pm-detalles-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var f = new Factory(carpeta); using var c = Cliente(f); await Alta(c); await Login(c);
            var r = await c.PostAsync("/Cuenta/CambiarCorreo", Form(Token(await c.GetStringAsync("/Cuenta/CambiarCorreo")),
                ("Correo", "nuevo@example.invalid"), ("ConfirmarCorreo", "nuevo@example.invalid"), ("PasswordActual", "Demo-Prueba2026!")));
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            var contenido = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(carpeta,"*.eml")));
            Assert.Contains("To: nuevo@example.invalid", contenido); Assert.Contains("/Cuenta/ConfirmarCorreo?token=", contenido);
            var pagina = await c.GetStringAsync(r.Headers.Location!); Assert.DoesNotContain("?token=", pagina);
        }
        finally { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
    }
    private static async Task Alta(HttpClient c)
    {
        var r = await c.PostAsync("/Registro", Form(Token(await c.GetStringAsync("/Registro")), ("Nombre", "Cliente de prueba"), ("Correo", "cliente@example.invalid"),
            ("Password", "Demo-Prueba2026!"), ("ConfirmarPassword", "Demo-Prueba2026!"), ("AceptaTerminos", "true"), ("AceptaPrivacidad", "true"),
            ("VersionTerminos", "demo-terminos-1"), ("VersionPrivacidad", "demo-privacidad-1"), ("VersionPromociones", "demo-promociones-1")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }
    private static async Task Login(HttpClient c, string correo = "cliente@example.invalid", string password = "Demo-Prueba2026!")
    {
        var r = await c.PostAsync("/Cuenta/Login", Form(Token(await c.GetStringAsync("/Cuenta/Login")), ("Correo", correo), ("Password", password), ("returnUrl", "/Cliente/Perfil")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.Equal("/Cliente/Perfil", r.Headers.Location!.OriginalString);
    }
    private static string Token(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""); Assert.True(m.Success);
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string token, params (string Clave,string Valor)[] campos) =>
        new(campos.Select(x => new KeyValuePair<string,string>(x.Clave,x.Valor)).Append(new KeyValuePair<string,string>("__RequestVerificationToken",token)));
    private static HttpClient Cliente(Factory f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private sealed class Entrega : IRecoveryDelivery, IAccountDelivery
    {
        public Uri? Reset { get; private set; }
        public string? Cuerpo { get; private set; }
        public void CheckConfiguration() { }
        public Task DeliverAsync(string recipient, Uri resetLink, CancellationToken ct) { Reset = resetLink; return Task.CompletedTask; }
        public Task DeliverMessageAsync(string recipient, string subject, string body, CancellationToken ct) { Cuerpo = body; return Task.CompletedTask; }
    }
    private sealed class Factory(string? carpeta = null) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public Entrega Entrega { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development"); b.UseSetting("Clientes:Demostracion","true"); b.UseSetting("Tiendas:SqlLocal","false"); b.UseSetting("Tiendas:Demostracion","false");
            b.UseSetting("Recovery:Provider","Pickup"); b.UseSetting("Recovery:WebBaseUrl","https://localhost/");
            b.UseSetting("ConnectionStrings:PortalMercado","Server=127.0.0.1,1;Database=Inaccesible;Integrated Security=True;Connect Timeout=1;");
            if (carpeta is not null) b.UseSetting("Recovery:PickupPath", carpeta);
            else b.ConfigureTestServices(s => { s.AddSingleton<IRecoveryDelivery>(Entrega); s.AddSingleton<IAccountDelivery>(Entrega); });
        }
    }
}

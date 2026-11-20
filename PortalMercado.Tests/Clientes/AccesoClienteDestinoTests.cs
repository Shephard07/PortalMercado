using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

public sealed class AccesoClienteDestinoTests
{
    [Theory]
    [InlineData("/explorar/buscar?q=Mango", "/explorar/buscar?q=Mango")]
    [InlineData("/tiendas/fruteria-el-sol?q=Tomate#catalogo", "/tiendas/fruteria-el-sol?q=Tomate#catalogo")]
    [InlineData("https://externo.invalid", "/tiendas")]
    [InlineData("//externo.invalid", "/tiendas")]
    [InlineData("/%2fexterno.invalid", "/tiendas")]
    [InlineData("/%255cexterno.invalid", "/tiendas")]
    public async Task Login_reutiliza_identidad_y_redirige_solo_a_destino_local(string solicitado, string esperado)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var html = await c.GetStringAsync("/Cuenta/Login?returnUrl=" + Uri.EscapeDataString(solicitado));
        var r = await c.PostAsync("/Cuenta/Login", Login(Token(html), solicitado));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(esperado, r.Headers.Location!.OriginalString);
        Assert.Equal(1, f.Auth.LoginCalls);
    }
    [Fact]
    public async Task Login_sin_destino_conserva_el_panel_actual()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.PostAsync("/Cuenta/Login", Login(Token(await c.GetStringAsync("/Cuenta/Login"))));
        Assert.Equal("/", r.Headers.Location!.OriginalString);
    }
    [Fact]
    public async Task Credenciales_invalidas_conservan_destino_sin_repetir_password()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.PostAsync("/Cuenta/Login", Login(Token(await c.GetStringAsync("/Cuenta/Login")), "/explorar", "ClaveIncorrecta123!"));
        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("value=\"/explorar\"", html);
        Assert.DoesNotContain("ClaveIncorrecta123!", html);
        Assert.DoesNotContain("__Host-PortalMercado.MVC=", string.Join("", r.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : []));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Destino_se_conserva_despues_de_MFA_y_codigos_de_recuperacion(bool codigos)
    {
        await using var f = new Factory(mfa: true, codigos: codigos); using var c = f.Cliente();
        var r = await c.PostAsync("/Cuenta/Login", Login(Token(await c.GetStringAsync("/Cuenta/Login")), "/explorar/buscar?q=Mango"));
        Assert.Contains("ConfigurarMfa", r.Headers.Location!.OriginalString);
        var cookies = r.Headers.GetValues("Set-Cookie");
        var destino = Assert.Single(cookies, x => x.StartsWith("__Host-PortalMercado.MfaDestino.MVC="));
        Assert.Contains("secure", destino.ToLowerInvariant()); Assert.Contains("httponly", destino.ToLowerInvariant());
        Assert.DoesNotContain("Mango", destino);
        var setup = await c.GetStringAsync("/Cuenta/ConfigurarMfa");
        var verificar = await c.PostAsync("/Cuenta/ConfirmarMfa", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Code"] = "123456", ["__RequestVerificationToken"] = Token(setup) }));
        if (codigos)
        {
            Assert.Equal(HttpStatusCode.OK, verificar.StatusCode);
            var html = WebUtility.HtmlDecode(await verificar.Content.ReadAsStringAsync());
            Assert.Contains("href=\"/explorar/buscar?q=Mango\"", html);
            Assert.Contains("CODIGO-PRUEBA", html);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Redirect, verificar.StatusCode);
            Assert.Equal("/explorar/buscar?q=Mango", verificar.Headers.Location!.OriginalString);
        }
    }
    [Fact]
    public async Task Cookie_de_destino_modificada_no_redirige_fuera_del_sitio()
    {
        await using var f = new Factory(mfa: true);
        using var c = f.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        var pagina = await c.GetAsync("/Cuenta/Login");
        var csrfCookie = string.Join("; ", pagina.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0]));
        using var login = new HttpRequestMessage(HttpMethod.Post, "/Cuenta/Login")
            { Content = Login(Token(await pagina.Content.ReadAsStringAsync()), "/explorar") };
        login.Headers.Add("Cookie", csrfCookie);
        var inicio = await c.SendAsync(login);
        var retoCookie = Assert.Single(inicio.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-PortalMercado.Mfa.MVC="));
        var cookies = csrfCookie + "; " + retoCookie.Split(';')[0] + "; __Host-PortalMercado.MfaDestino.MVC=alterado";
        using var getSetup = new HttpRequestMessage(HttpMethod.Get, "/Cuenta/ConfigurarMfa");
        getSetup.Headers.Add("Cookie", cookies);
        var setup = await c.SendAsync(getSetup);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Cuenta/ConfirmarMfa")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string,string>
                { ["Code"] = "123456", ["__RequestVerificationToken"] = Token(await setup.Content.ReadAsStringAsync()) })
        };
        request.Headers.Add("Cookie", cookies);
        var respuesta = await c.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", respuesta.Headers.Location!.OriginalString);
    }
    private static FormUrlEncodedContent Login(string token, string? destino = null, string password = "PruebaSegura123!")
    {
        var datos = new Dictionary<string,string> { ["Correo"] = "v@example.invalid", ["Password"] = password, ["__RequestVerificationToken"] = token };
        if (destino is not null) datos["returnUrl"] = destino;
        return new(datos);
    }
    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private sealed class MfaConCodigos(ControlledAuth auth, bool codigos) : IMfaService
    {
        private readonly ControlledMfa original = new(auth);
        public Task<MfaEnrollment?> GetEnrollmentAsync(string challenge, string host, CancellationToken ct) => original.GetEnrollmentAsync(challenge, host, ct);
        public async Task<MfaCompletion?> CompleteAsync(string challenge, string code, string host, string correlationId, CancellationToken ct, bool regenerateRecoveryCodes = false)
        {
            var r = await original.CompleteAsync(challenge, code, host, correlationId, ct, regenerateRecoveryCodes);
            return r is null ? null : new MfaCompletion(r.Session, codigos ? ["CODIGO-PRUEBA"] : []);
        }
    }
    private sealed class Factory(bool mfa = false, bool codigos = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public ControlledAuth Auth { get; } = new() { RequireMfa = mfa };
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "false");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(Auth);
                s.RemoveAll<IMfaService>(); s.AddSingleton<IMfaService>(new MfaConCodigos(Auth, codigos));
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

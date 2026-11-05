using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests;

public sealed class MfaWebTests
{
    [Fact]
    public async Task PrimerFactorMvcNoEntregaSesionYSegundoFactorSi()
    {
        await using var factory = new MfaWebFactory();
        var client = factory.Client();
        var csrf = Token(await client.GetStringAsync("/Cuenta/Login"));
        var login = await client.PostAsync("/Cuenta/Login", Form(csrf, ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("ConfigurarMfa", login.Headers.Location!.ToString());
        Assert.DoesNotContain(login.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-PortalMercado.MVC="));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Cuenta/Perfil")).StatusCode);
        var setup = await client.GetStringAsync("/Cuenta/ConfigurarMfa");
        Assert.Contains("Configura la", setup);
        var verify = await client.PostAsync("/Cuenta/ConfirmarMfa", Form(Token(setup), ("Code", "123456")));
        Assert.Equal(HttpStatusCode.Redirect, verify.StatusCode);
        Assert.Contains("Perfil", verify.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Cuenta/Perfil")).StatusCode);
    }
    [Fact]
    public async Task CodigoMfaIncorrectoNoEntregaSesionNiSeRepiteEnHtml()
    {
        await using var factory = new MfaWebFactory(); var client = factory.Client();
        var csrf = Token(await client.GetStringAsync("/Cuenta/Login"));
        await client.PostAsync("/Cuenta/Login", Form(csrf, ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!")));
        var setup = await client.GetStringAsync("/Cuenta/ConfigurarMfa");
        var response = await client.PostAsync("/Cuenta/ConfirmarMfa", Form(Token(setup), ("Code", "000000")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("000000", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Cuenta/Perfil")).StatusCode);
    }
    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields) =>
        new(fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value))
            .Append(new KeyValuePair<string, string>("__RequestVerificationToken", token)));
}
public sealed class MfaWebFactory : WebApplicationFactory<PortalMercado.Web.Controllers.CuentaController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
        builder.ConfigureTestServices(services =>
        {
            var auth = new ControlledAuth { RequireMfa = true };
            services.RemoveAll<IAuthService>(); services.AddSingleton<IAuthService>(auth);
            services.RemoveAll<IMfaService>(); services.AddSingleton<IMfaService>(new ControlledMfa(auth));
        });
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false});
}

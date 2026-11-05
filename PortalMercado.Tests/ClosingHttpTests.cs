using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;
namespace PortalMercado.Tests;

public sealed class ClosingHttpTests
{
    [Fact] public async Task RevokedMvcSessionRedirectsToDedicatedPage()
    {
        await using var f = new WebFactory(); var client = f.Client();
        var html = await client.GetStringAsync("/Cuenta/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var login = await client.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = token, ["Correo"] = "v@example.invalid", ["Password"] = "PruebaSegura123!" }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Cuenta/Perfil")).StatusCode);
        f.Auth.Active = false;
        var revoked = await client.GetAsync("/Cuenta/Perfil");
        Assert.Equal("/Cuenta/SesionExpirada", revoked.Headers.Location?.OriginalString);
        var page = await client.GetAsync(revoked.Headers.Location);
        Assert.Equal(HttpStatusCode.Unauthorized, page.StatusCode);
        Assert.Contains("Inicia sesión nuevamente", WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()));
    }
    [Fact] public async Task NewAnonymousVisitorStillGoesToLogin()
    {
        await using var f = new WebFactory(); var response = await f.Client().GetAsync("/Cuenta/Perfil");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Cuenta/Login", response.Headers.Location?.OriginalString);
    }
    [Theory]
    [InlineData("PATCH", "/api/usuarios/2/estado")]
    [InlineData("PUT", "/api/usuarios/2/roles")]
    [InlineData("PUT", "/api/usuarios/2/negocios")]
    [InlineData("PUT", "/api/usuarios/2/mercados")]
    [InlineData("PUT", "/api/roles/2/permisos")]
    public async Task MissingChangeFieldsCannotDeactivateOrClearAssignments(string method, string path)
    {
        await using var f = new AccessFactory(); var client = f.Client(); await Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" })).StatusCode);
        await Csrf(client);
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }
    [Fact] public async Task OpenApiShowsFictitiousExamplesAndDocumentsPasswordResponses()
    {
        await using var f = new AccessFactory(); var doc = await f.Client().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.Equal("vendedor.et02@example.invalid", schemas.GetProperty("LoginRequest").GetProperty("examples")[0].GetProperty("correo").GetString());
        Assert.Equal("REEMPLAZAR_CLAVE_LOCAL", schemas.GetProperty("LoginRequest").GetProperty("examples")[0].GetProperty("password").GetString());
        var responses = doc.GetProperty("paths").GetProperty("/api/auth/forgot-password").GetProperty("post").GetProperty("responses");
        Assert.True(responses.TryGetProperty("202", out _)); Assert.True(responses.TryGetProperty("503", out _));
    }
    private static async Task Csrf(HttpClient c)
    {
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    private sealed class WebFactory : WebApplicationFactory<PortalMercado.Web.Controllers.CuentaController>
    {
        public ControlledAuth Auth { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            builder.ConfigureTestServices(s => { s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(Auth); });
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

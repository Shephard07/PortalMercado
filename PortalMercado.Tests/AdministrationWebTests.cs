using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
using Xunit;
namespace PortalMercado.Tests;
public sealed class AdministrationWebTests
{
    [Theory] [InlineData("/Usuarios")] [InlineData("/Usuarios/Crear")] [InlineData("/Usuarios/Editar/2")]
    [InlineData("/Administracion/Permisos")] [InlineData("/Negocios")]
    public async Task ScreensRenderWithRealRazorAndSafeEncoding(string path)
    {
        await using var f = new Factory(); var client = f.Client(); await Login(client);
        var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("<script>FICTICIO</script>", html);
        if (path != "/Usuarios") Assert.Contains("__RequestVerificationToken", html);
    }
    [Fact] public async Task MvcMutationWithoutCsrfIsRejected()
    {
        await using var f = new Factory(); var client = f.Client(); await Login(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Usuarios/Estado/2", new FormUrlEncodedContent(new Dictionary<string,string> { ["activo"] = "false" }))).StatusCode);
    }
    [Fact] public async Task DeniedAdministrationUsesSafe403View()
    {
        await using var f = new Factory(); var client = f.Client(); await Login(client);
        var html = await client.GetStringAsync("/Usuarios/Crear"); var token = Token(html);
        var response = await client.PostAsync("/Usuarios/Crear", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["Correo"] = "v@example.invalid", ["Nombre"] = "Ficticio", ["Password"] = "NoRepetirEsta123!" }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain("NoRepetirEsta123!", await response.Content.ReadAsStringAsync());
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task Login(HttpClient client)
    {
        var html = await client.GetStringAsync("/Cuenta/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = Token(html), ["Correo"] = "v@example.invalid", ["Password"] = "PruebaSegura123!" }))).StatusCode);
    }
    private sealed class Factory : WebApplicationFactory<PortalMercado.Web.Controllers.UsuariosController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development"); b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.ConfigureTestServices(s => { s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(new ControlledAuth()); s.RemoveAll<IUserAdministration>(); s.AddSingleton<IUserAdministration>(new ViewAdministration()); });
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
    private sealed class ViewAdministration : IUserAdministration
    {
        private static UserDto User => new(2, "ficticio@example.invalid", "<script>FICTICIO</script>", null, true, ["VENDEDOR"], [1], [], false);
        public Task<AccessSnapshot> AccessAsync(long actor, CancellationToken ct) => Task.FromResult(new AccessSnapshot(actor, true, ["ADMIN_GENERAL"], Permissions.All, [], []));
        public Task<Page<UserDto>> ListAsync(long actor, int page, int size, CancellationToken ct) => Task.FromResult(new Page<UserDto>([User], 1, 1, 20));
        public Task<UserDto> GetAsync(long actor, long id, CancellationToken ct) => Task.FromResult(User);
        public Task<AdminCatalog> CatalogAsync(long actor, CancellationToken ct) => Task.FromResult(new AdminCatalog([new RoleDto(1, "VENDEDOR", "Vendedor", [1])], [new PermissionDto(1, Permissions.Context, "Contexto")], [new NamedId(1, "Negocio ficticio")], [new NamedId(1, "Mercado ficticio")]));
        public Task<BusinessContext> ContextAsync(long actor, string credential, string host, CancellationToken ct) => Task.FromResult(new BusinessContext([new NamedId(1, "Negocio ficticio")], 1));
        public Task<UserDto> CreateAsync(long actor, CreateUserRequest request, string correlation, CancellationToken ct) => throw new AccessFailure(403, "Operación no autorizada.");
        public Task EditAsync(long actor, long id, EditUserRequest request, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task StateAsync(long actor, long id, bool active, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task RolesAsync(long actor, long id, int[] roles, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task BusinessesAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task MarketsAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task PermissionsAsync(long actor, int role, int[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task SelectContextAsync(long actor, string credential, string host, long business, string correlation, CancellationToken ct) => throw new NotSupportedException();
    }
}

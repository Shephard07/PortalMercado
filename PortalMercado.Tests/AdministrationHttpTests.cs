using System.Net;
using System.Net.Http.Json;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
using Xunit;
namespace PortalMercado.Tests;
public sealed class AdministrationHttpTests
{
    [Theory]
    [InlineData("/api/usuarios")]
    [InlineData("/api/roles")]
    [InlineData("/api/permisos")]
    [InlineData("/api/negocios/contexto")]
    public async Task ProtectedRoutesReturn401WithoutLogin(string route)
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }
    [Fact] public async Task LoggedInMutationStillRequiresCsrf()
    {
        await using var factory = new AccessFactory(); var client = factory.Client(); var csrf = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/usuarios/78", new EditUserRequest { Nombre = "Ficticio" })).StatusCode);
    }
    [Fact] public async Task OpenApiDescribesAdministrationAndSafeDto()
    {
        await using var factory = new AccessFactory(); var json = await factory.Client().GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/usuarios/{id}/roles", json); Assert.Contains("/api/roles/{id}/permisos", json);
        Assert.Contains("/api/negocios/contexto", json); Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }
}

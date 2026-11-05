using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Tests;

public sealed class CredentialPolicyTests
{
    [Theory]
    [InlineData("  vendedor@example.invalid  ", "VENDEDOR@EXAMPLE.INVALID")]
    [InlineData("Ventas@Example.Invalid", "VENTAS@EXAMPLE.INVALID")]
    public void NormalizaCorreo(string input, string expected) => Assert.Equal(expected, CredentialPolicy.NormalizeEmail(input));
    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("a b@example.invalid")]
    [InlineData("josé@example.invalid")]
    public void RechazaCorreoNoCompatibleConReglaSql(string input) => Assert.Null(CredentialPolicy.NormalizeEmail(input));
    [Theory]
    [InlineData("corta1A!", false)]
    [InlineData("abcdefghijkl", false)]
    [InlineData("EjemploSeguro123!", true)]
    public void PoliticaDePassword(string input, bool expected) => Assert.Equal(expected, CredentialPolicy.IsValidPassword(input));
    [Theory]
    [InlineData(false, "ADMIN_GENERAL", true)]
    [InlineData(false, "admin_mercado", true)]
    [InlineData(false, "VENDEDOR", false)]
    [InlineData(true, "VENDEDOR", true)]
    public void AdministradoresNuncaAccedenSinSegundoFactor(bool flag, string role, bool expected) =>
        Assert.Equal(expected, AuthenticationRules.RequiresSecondFactor(flag, [role]));
    [Fact]
    public void HashUsaSalYVerificaPassword()
    {
        var user = new Usuario(); var hasher = new PasswordHasher<Usuario>();
        var a = hasher.HashPassword(user, "PruebaSegura123!");
        var b = hasher.HashPassword(user, "PruebaSegura123!");
        Assert.NotEqual(a, b); Assert.DoesNotContain("PruebaSegura123!", a);
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, a, "PruebaSegura123!"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, a, "OtraClave"));
    }
}

public sealed class AccessHttpTests
{
    [Fact]
    public async Task MeAnonimoDevuelve401SinRedireccion()
    {
        await using var factory = new AccessFactory();
        var client = factory.Client();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
    [Fact]
    public async Task LoginSinAntiforgeryRechazadoAntesDeAutenticar()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Auth.LoginCalls);
    }
    [Fact]
    public async Task ErrorDeCredencialesEsGenerico()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        await PrepareCsrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "nadie@example.invalid", Password = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("nadie@", body); Assert.DoesNotContain("incorrecta", body);
        Assert.Contains("No se pudo iniciar", body);
    }
    [Fact]
    public async Task LoginCookieSeguraPerfilYLogoutRevocanCredencialAnterior()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        await PrepareCsrf(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-PortalMercado.API="));
        Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        var profile = await client.GetStringAsync("/api/auth/me");
        Assert.Contains("Vendedor ficticio", profile);
        Assert.DoesNotContain("PasswordHash", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Auth.Credential, profile);
        // Los tokens antiforgery cambian de identidad tras login.
        await PrepareCsrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        using var replay = factory.Client(false);
        replay.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task EstadoRevocadoInvalidaCookieEnSiguientePeticion()
    {
        await using var factory = new AccessFactory(); var client = factory.Client(); await PrepareCsrf(client);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        factory.Auth.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task AutenticadoSinRolDevuelve403()
    {
        await using var factory = new AccessFactory(); var client = factory.Client(); await PrepareCsrf(client);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/test-access/admin")).StatusCode);
    }
    [Fact]
    public async Task RateLimitResponde429()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        HttpResponseMessage? response = null;
        for (var i = 0; i < 61; i++) response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }
    [Fact]
    public async Task LoginMantieneSuLimiteIndependienteDeCsrf()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        await PrepareCsrf(client);
        HttpResponseMessage? response = null;
        for (var i = 0; i < 11; i++) response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "incorrecta" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }
    private static async Task PrepareCsrf(HttpClient client)
    {
        var data = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", data!.RequestToken);
    }
    private sealed record CsrfResponse(string RequestToken);
}

public sealed class AccessFactory : WebApplicationFactory<PortalMercado.API.Controllers.AuthController>
{
    public ControlledAuth Auth { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>(); services.AddSingleton<IAuthService>(Auth);
            services.RemoveAll<IMfaService>(); services.AddSingleton<IMfaService>(new ControlledMfa(Auth));
            services.AddControllers().AddApplicationPart(typeof(TestAccessController).Assembly);
        });
    }
    public HttpClient Client(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = cookies });
}
public sealed class ControlledAuth : IAuthService
{
    public string Credential { get; } = new('a', 43);
    public bool Active { get; set; } = true;
    public bool RequireMfa { get; set; }
    public bool MfaCompleted { get; set; }
    public int LoginCalls { get; private set; }
    private static readonly AuthProfile Profile = new(77, "v@example.invalid", "Vendedor ficticio", ["VENDEDOR"], [1], []);
    public Task<LoginOutcome?> LoginAsync(LoginRequest request, string host, string correlationId, CancellationToken ct)
    {
        LoginCalls++;
        return Task.FromResult<LoginOutcome?>(request.Correo == "v@example.invalid" && request.Password == "PruebaSegura123!"
            ? RequireMfa
                ? new LoginOutcome(null, new MfaChallenge(Credential, true, DateTime.UtcNow.AddMinutes(5)))
                : new LoginOutcome(new AuthSession(Credential, DateTime.UtcNow.AddMinutes(30), Profile), null) : null);
    }
    public Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct) =>
        Task.FromResult<AuthProfile?>(Active && (!RequireMfa || MfaCompleted) && credential == Credential ? Profile : null);
    public Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct) { Active = false; return Task.CompletedTask; }
}
[ApiController, Route("api/test-access")]
public sealed class TestAccessController : ControllerBase
{
    [HttpGet("admin"), Authorize(Roles = "ADMIN_GENERAL")]
    public IActionResult Admin() => Ok();
}

public sealed class ControlledMfa(ControlledAuth auth) : IMfaService
{
    public Task<MfaEnrollment?> GetEnrollmentAsync(string challenge, string host, CancellationToken ct) =>
        Task.FromResult<MfaEnrollment?>(auth.RequireMfa && !auth.MfaCompleted && challenge == auth.Credential
            ? new MfaEnrollment("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", "otpauth://totp/FICTICIO") : null);
    public Task<MfaCompletion?> CompleteAsync(string challenge, string code, string host, string correlationId, CancellationToken ct, bool regenerateRecoveryCodes = false)
    {
        if (!auth.RequireMfa || auth.MfaCompleted || challenge != auth.Credential || code != "123456") return Task.FromResult<MfaCompletion?>(null);
        auth.MfaCompleted = true;
        var profile = new AuthProfile(77, "v@example.invalid", "Vendedor ficticio", ["VENDEDOR"], [1], []);
        return Task.FromResult<MfaCompletion?>(new MfaCompletion(new AuthSession(auth.Credential, DateTime.UtcNow.AddMinutes(30), profile), []));
    }
}

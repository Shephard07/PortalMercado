using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Tests;

public sealed class MfaCryptographyTests
{
    // RFC 6238: clave ASCII de 20 bytes, t=59, TOTP SHA1 de 8 dígitos 94287082 -> 6 dígitos 287082.
    private const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private static readonly DateTime At59 = DateTime.UnixEpoch.AddSeconds(59);
    [Fact]
    public void VectorRfc6238SeValidaYNoSeReutiliza()
    {
        Assert.True(MfaCryptography.TryVerifyTotp(Secret, "287082", At59, null, out var step));
        Assert.Equal(1L, step);
        Assert.False(MfaCryptography.TryVerifyTotp(Secret, "287082", At59, step, out _));
    }
    [Fact]
    public void CodigoFueraDeVentanaSeRechaza() => Assert.False(
        MfaCryptography.TryVerifyTotp(Secret, "287082", At59.AddMinutes(3), null, out _));
    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("ABCDEF")]
    public void FormatoTotpInvalidoSeRechaza(string code) => Assert.False(
        MfaCryptography.TryVerifyTotp(Secret, code, At59, null, out _));
    [Fact]
    public void PasoAnteriorNoReemplazaUnoMasReciente() => Assert.False(
        MfaCryptography.TryVerifyTotp(Secret, "287082", At59, 2, out _));
    [Fact]
    public void RecuperacionTieneEntropiaYNormalizaFormato()
    {
        var a = MfaCryptography.RecoveryCode(); var b = MfaCryptography.RecoveryCode();
        Assert.Equal(32, a.Length); Assert.NotEqual(a, b);
        Assert.Equal(a, MfaCryptography.NormalizeRecovery(a.ToLowerInvariant()));
        Assert.Null(MfaCryptography.NormalizeRecovery("123456"));
        Assert.DoesNotContain(a, MfaCryptography.Hash(a));
    }
    [Fact]
    public void SecretoCifradoSeComparteSoloConMismasClavesYProposito()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pm-mfa-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config = new ConfigurationBuilder().Build();
            var environment = new FakeEnvironment { EnvironmentName = "Development" };
            var provider = DataProtectionProvider.Create(new DirectoryInfo(directory), b => b.SetApplicationName("PortalMercado.Security"));
            var a = new MfaCryptography(provider, config, environment);
            var value = a.Protect(Secret);
            Assert.DoesNotContain(Secret, value);
            var otherHost = DataProtectionProvider.Create(new DirectoryInfo(directory), b => b.SetApplicationName("PortalMercado.Security"));
            Assert.Equal(Secret, new MfaCryptography(otherHost, config, environment).Unprotect(value));
            Assert.Throws<CryptographicException>(() => provider.CreateProtector("OTRO_PROPOSITO").Unprotect(value));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    [Fact]
    public void ProduccionSinAlmacenConfiguradoRechazaProteccion()
    {
        var provider = new EphemeralDataProtectionProvider();
        var crypto = new MfaCryptography(provider, new ConfigurationBuilder().Build(), new FakeEnvironment { EnvironmentName = "Production" });
        Assert.Throws<InvalidOperationException>(() => crypto.Protect(Secret));
    }
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class MfaHttpTests
{
    [Fact]
    public async Task PasswordSinSegundoFactorNoCreaSesion()
    {
        await using var factory = new AccessFactory(); factory.Auth.RequireMfa = true;
        var client = factory.Client(); await PrepareCsrf(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        Assert.False(login.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(x => x.StartsWith("__Host-PortalMercado.API=")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task CodigoIncorrectoNoCreaSesionYCorrectoConsumeReto()
    {
        await using var factory = new AccessFactory(); factory.Auth.RequireMfa = true;
        var client = factory.Client(); await PrepareCsrf(client);
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        var verify = new MfaVerifyRequest { Challenge = factory.Auth.Credential, Code = "000000" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/mfa/verify", verify)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        verify.Code = "123456";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/mfa/verify", verify)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        await PrepareCsrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/mfa/verify", verify)).StatusCode);
    }
    [Fact]
    public async Task OpenApiDescribe204Y401()
    {
        await using var factory = new AccessFactory(); var client = factory.Client();
        var json = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/openapi/v1.json");
        Assert.True(json.GetProperty("paths").GetProperty("/api/auth/logout").GetProperty("post").GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(json.GetProperty("paths").GetProperty("/api/auth/me").GetProperty("get").GetProperty("responses").TryGetProperty("401", out _));
    }
    private static async Task PrepareCsrf(HttpClient client)
    {
        var value = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", value.GetProperty("requestToken").GetString());
    }
}

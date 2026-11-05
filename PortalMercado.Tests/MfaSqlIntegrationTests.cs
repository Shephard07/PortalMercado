using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OtpNet;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Tests;

[Trait("Category", "SqlIntegration")]
public sealed class MfaSqlIntegrationTests
{
    [SqlIntegrationFact]
    public async Task EnrolamientoActivaCuentaSoloTrasSegundoFactorYRetoNoSeReutiliza()
    {
        await using var fixture = await Fixture.CreateAsync();
        var challenge = await fixture.BeginAsync();
        await using (var scope = fixture.Provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PortalMercadoDbContext>();
            Assert.False((await db.Usuarios.SingleAsync(x => x.IdUsuario == fixture.UserId)).Activo);
            Assert.False(await db.Set<UsuarioSesion>().AnyAsync(x => x.IdUsuario == fixture.UserId));
        }
        var result = await fixture.EnrollAsync(challenge);
        Assert.Equal(10, result.RecoveryCodes.Length);
        await using var after = fixture.Provider.CreateAsyncScope();
        var context = after.ServiceProvider.GetRequiredService<PortalMercadoDbContext>();
        var user = await context.Usuarios.SingleAsync(x => x.IdUsuario == fixture.UserId);
        Assert.True(user.Activo); Assert.True(user.DosFactoresHabilitado);
        Assert.NotNull(user.UltimoAcceso);
        Assert.True((await context.Set<UsuarioSesion>().SingleAsync(x => x.IdUsuario == fixture.UserId)).MfaVerificado);
        Assert.Null(await after.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(challenge.Challenge, "123456", "API", "test", default));
    }
    [SqlIntegrationFact]
    public async Task RecuperacionSeConsumeUnaVezYLogoutRevocaSesion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var enrolled = await fixture.EnrollAsync(await fixture.BeginAsync());
        var challenge = await fixture.BeginAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IMfaService>();
        var recovered = await service.CompleteAsync(challenge.Challenge, enrolled.RecoveryCodes[0], "API", "test", default);
        Assert.NotNull(recovered);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        Assert.NotNull(await auth.ValidateAsync(recovered.Session.Credential, "API", default));
        await auth.LogoutAsync(recovered.Session.Credential, "API", "test", default);
        Assert.Null(await auth.ValidateAsync(recovered.Session.Credential, "API", default));
        var second = await fixture.BeginAsync();
        await using var nextScope = fixture.Provider.CreateAsyncScope();
        Assert.Null(await nextScope.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(second.Challenge, enrolled.RecoveryCodes[0], "API", "test", default));
    }
    [SqlIntegrationFact]
    public async Task RetoApiNoSirveEnMvcYTotpNoSeReutilizaEnOtroReto()
    {
        await using var fixture = await Fixture.CreateAsync(); var first = await fixture.BeginAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var mfa = scope.ServiceProvider.GetRequiredService<IMfaService>();
        Assert.Null(await mfa.GetEnrollmentAsync(first.Challenge, "MVC", default));
        var enrollment = await mfa.GetEnrollmentAsync(first.Challenge, "API", default);
        Assert.NotNull(enrollment);
        var code = new Totp(Base32Encoding.ToBytes(enrollment.Secret)).ComputeTotp();
        Assert.Null(await mfa.CompleteAsync(first.Challenge, code, "MVC", "test", default));
        var success = await mfa.CompleteAsync(first.Challenge, code, "API", "test", default);
        Assert.NotNull(success);
        var second = await fixture.BeginAsync();
        await using var nextScope = fixture.Provider.CreateAsyncScope();
        Assert.Null(await nextScope.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(second.Challenge, code, "API", "test", default));
    }

    [SqlIntegrationFact]
    public async Task RegenerarRecuperacionInvalidaCodigosYSesionesAnteriores()
    {
        await using var fixture = await Fixture.CreateAsync();
        var initial = await fixture.EnrollAsync(await fixture.BeginAsync());
        var challenge = await fixture.BeginAsync();
        MfaCompletion? renewed;
        await using (var scope = fixture.Provider.CreateAsyncScope())
        {
            renewed = await scope.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(
                challenge.Challenge, initial.RecoveryCodes[0], "API", "test", default, regenerateRecoveryCodes: true);
            Assert.NotNull(renewed); Assert.Equal(10, renewed.RecoveryCodes.Length);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateAsync(initial.Session.Credential, "API", default));
        }
        var oldCodeChallenge = await fixture.BeginAsync();
        await using (var scope = fixture.Provider.CreateAsyncScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(oldCodeChallenge.Challenge, initial.RecoveryCodes[1], "API", "test", default));
        var newCodeChallenge = await fixture.BeginAsync();
        await using (var scope = fixture.Provider.CreateAsyncScope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IMfaService>().CompleteAsync(newCodeChallenge.Challenge, renewed.RecoveryCodes[0], "API", "test", default));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required string KeyPath { get; init; }
        public required string Correo { get; init; }
        public required string Password { get; init; }
        public long UserId { get; private set; }
        public static async Task<Fixture> CreateAsync()
        {
            var keyPath = Path.Combine(Path.GetTempPath(), "pm-mfa-sql-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalMercado"] = @"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;",
                ["Security:DataProtectionKeyPath"] = keyPath
            }).Build();
            var env = new EnvironmentForTests();
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton<IConfiguration>(config); services.AddSingleton<IHostEnvironment>(env);
            services.AddInfrastructure(config); services.AddPortalAccess("API", config, env);
            var fixture = new Fixture { Provider = services.BuildServiceProvider(), KeyPath = keyPath,
                Correo = "mfa-test-" + Guid.NewGuid().ToString("N") + "@example.invalid", Password = MfaCryptography.NewCredential() + "aA1!" };
            try
            {
                await using var scope = fixture.Provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PortalMercadoDbContext>();
                Assert.True(await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-002"), "Aplicar ET02-002 antes de las pruebas SQL MFA.");
                var role = await db.Set<Rol>().SingleOrDefaultAsync(x => x.Codigo == "ADMIN_GENERAL");
                if (role is null) { role = new Rol { Codigo = "ADMIN_GENERAL", Nombre = "Administrador general" }; db.Add(role); await db.SaveChangesAsync(); }
                var user = new Usuario { Correo = fixture.Correo, Nombre = "Administrador ficticio de prueba MFA", Activo = false, CreadoEn = DateTime.UtcNow };
                user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(user, fixture.Password);
                db.Add(user); await db.SaveChangesAsync(); fixture.UserId = user.IdUsuario;
                db.Add(new UsuarioSeguridad { IdUsuario = user.IdUsuario, Sello = Guid.NewGuid() });
                db.Add(new UsuarioRol { IdUsuario = user.IdUsuario, IdRol = role.IdRol });
                db.Add(new UsuarioMfa { IdUsuario = user.IdUsuario, PendienteBootstrap = true });
                await db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async Task<MfaChallenge> BeginAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Correo = Correo, Password = Password }, "API", "test", default);
            Assert.NotNull(result?.Mfa); Assert.Null(result.Session);
            return result.Mfa;
        }
        public async Task<MfaCompletion> EnrollAsync(MfaChallenge challenge)
        {
            await using var scope = Provider.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IMfaService>();
            var enrollment = await service.GetEnrollmentAsync(challenge.Challenge, "API", default);
            Assert.NotNull(enrollment);
            var code = new Totp(Base32Encoding.ToBytes(enrollment.Secret)).ComputeTotp();
            var result = await service.CompleteAsync(challenge.Challenge, code, "API", "test", default);
            Assert.NotNull(result); return result;
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (UserId != 0)
                {
                    await using var scope = Provider.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<PortalMercadoDbContext>();
                    await db.Set<UsuarioMfaRecuperacion>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Set<UsuarioMfaReto>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Set<UsuarioMfa>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Set<UsuarioSesion>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Set<AuditoriaAcceso>().Where(x => x.IdActor == UserId).ExecuteDeleteAsync();
                    await db.Set<UsuarioRol>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Set<UsuarioSeguridad>().Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                    await db.Usuarios.Where(x => x.IdUsuario == UserId).ExecuteDeleteAsync();
                }
            }
            finally
            {
                await Provider.DisposeAsync();
                if (Directory.Exists(KeyPath)) Directory.Delete(KeyPath, true);
            }
        }
    }
    private sealed class EnvironmentForTests : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

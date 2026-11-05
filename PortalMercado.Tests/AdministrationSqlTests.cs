using System.Text.Json;
using OtpNet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Infrastructure.Security;
using Xunit;
namespace PortalMercado.Tests;

[Trait("Category", "SqlIntegration")]
public sealed class AdministrationSqlTests
{
    [SqlIntegrationFact] public async Task OwnerListAndIdLookupExcludeForeignAndSharedUsers()
    {
        await using var f = await Fixture.CreateAsync();
        var owner = await f.User("PROPIETARIO_NEGOCIO", [f.BusinessA]);
        var seller = await f.User("VENDEDOR", [f.BusinessA]);
        var foreign = await f.User("VENDEDOR", [f.BusinessB]);
        var shared = await f.User("VENDEDOR", [f.BusinessA, f.BusinessB]);
        await f.Run(async (admin, db) =>
        {
            var page = await admin.ListAsync(owner, 1, 100, default);
            Assert.Contains(page.Items, x => x.IdUsuario == seller); Assert.DoesNotContain(page.Items, x => x.IdUsuario == foreign || x.IdUsuario == shared);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.GetAsync(owner, foreign, default))).Status);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.EditAsync(owner, shared, new EditUserRequest { Nombre = "No permitido" }, "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task OwnerCreatesOnlyOwnSellerWithSafeHashAndAudit()
    {
        await using var f = await Fixture.CreateAsync(); var owner = await f.User("PROPIETARIO_NEGOCIO", [f.BusinessA]);
        await f.Run(async (admin, db) =>
        {
            var dto = await admin.CreateAsync(owner, new CreateUserRequest { Correo = f.Email(), Nombre = "Vendedor de prueba", Password = f.Password, Negocios = [f.BusinessA] }, "test", default);
            f.Users.Add(dto.IdUsuario);
            var row = await db.Usuarios.AsNoTracking().SingleAsync(x => x.IdUsuario == dto.IdUsuario);
            Assert.NotEqual(f.Password, row.PasswordHash); Assert.DoesNotContain("PasswordHash", JsonSerializer.Serialize(dto));
            Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<Usuario>().VerifyHashedPassword(row, row.PasswordHash, f.Password));
            Assert.True(await db.Set<AuditoriaAcceso>().AnyAsync(x => x.IdActor == owner && x.IdObjetivo == dto.IdUsuario));
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.CreateAsync(owner, new CreateUserRequest { Correo = f.Email(), Nombre = "No permitido", Password = f.Password, Rol = "ADMIN_GENERAL", Negocios = [f.BusinessA] }, "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task SellerCannotEditAndOwnerCannotAssignForeignBusinessOrElevateSelf()
    {
        await using var f = await Fixture.CreateAsync(); var owner = await f.User("PROPIETARIO_NEGOCIO", [f.BusinessA]); var seller = await f.User("VENDEDOR", [f.BusinessA]);
        await f.Run(async (admin, db) =>
        {
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.EditAsync(seller, owner, new EditUserRequest { Nombre = "No permitido" }, "test", default))).Status);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.BusinessesAsync(owner, seller, [f.BusinessB], "test", default))).Status);
            var role = await db.Set<Rol>().SingleAsync(x => x.Codigo == "ADMIN_GENERAL");
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.RolesAsync(owner, owner, [role.IdRol], "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task DeactivationAndAssignmentsInvalidatePreviousSession()
    {
        await using var f = await Fixture.CreateAsync(); var general = await f.User("ADMIN_GENERAL", []); var seller = await f.User("VENDEDOR", [f.BusinessA]);
        var login = await f.Login(seller);
        await f.Run(async (admin, db) => await admin.BusinessesAsync(general, seller, [f.BusinessB], "test", default));
        await f.Scope(async services => Assert.Null(await services.GetRequiredService<IAuthService>().ValidateAsync(login.Credential, "API", default)));
        var next = await f.Login(seller); Assert.Equal([f.BusinessB], next.Profile.Negocios);
        await f.Run(async (admin, db) => await admin.StateAsync(general, seller, false, "test", default));
        await f.Scope(async services => Assert.Null(await services.GetRequiredService<IAuthService>().ValidateAsync(next.Credential, "API", default)));
    }
    [SqlIntegrationFact] public async Task ContextChecksOwnershipAndPersistsPerHostSession()
    {
        await using var f = await Fixture.CreateAsync(); var seller = await f.User("VENDEDOR", [f.BusinessA]); var login = await f.Login(seller);
        await f.Run(async (admin, db) => await admin.SelectContextAsync(seller, login.Credential, "API", f.BusinessA, "test", default));
        await f.Run(async (admin, db) =>
        {
            var context = await admin.ContextAsync(seller, login.Credential, "API", default); Assert.Equal(f.BusinessA, context.IdNegocioActivo);
            Assert.DoesNotContain(context.Disponibles, x => x.Id == f.BusinessB);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.SelectContextAsync(seller, login.Credential, "API", f.BusinessB, "test", default))).Status);
            Assert.Equal(401, (await Assert.ThrowsAsync<AccessFailure>(() => admin.ContextAsync(seller, login.Credential, "MVC", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task NewAdministratorCannotBeActivatedBeforeMfa()
    {
        await using var f = await Fixture.CreateAsync(); var general = await f.User("ADMIN_GENERAL", []);
        await f.Run(async (admin, db) =>
        {
            var dto = await admin.CreateAsync(general, new CreateUserRequest { Correo = f.Email(), Nombre = "Administrador de prueba", Password = f.Password, Rol = "ADMIN_MERCADO", Mercados = [f.MarketA] }, "test", default);
            f.Users.Add(dto.IdUsuario); Assert.False(dto.Activo); Assert.True(dto.PendienteMfa);
            Assert.Equal(409, (await Assert.ThrowsAsync<AccessFailure>(() => admin.StateAsync(general, dto.IdUsuario, true, "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task RolePermissionChangeRevokesSessionsAndProtectsOwnRole()
    {
        await using var f = await Fixture.CreateAsync(); var general = await f.User("ADMIN_GENERAL", []);
        int custom = 0;
        await f.Scope(async services =>
        {
            var db = services.GetRequiredService<PortalMercadoDbContext>();
            var role = new Rol { Codigo = "TEST_" + Guid.NewGuid().ToString("N"), Nombre = "Rol efímero de prueba" }; db.Add(role); await db.SaveChangesAsync(); custom = role.IdRol; f.Roles.Add(custom);
        });
        var user = await f.User("VENDEDOR", [f.BusinessA]);
        await f.Scope(async services => { var db = services.GetRequiredService<PortalMercadoDbContext>(); db.Add(new UsuarioRol { IdUsuario = user, IdRol = custom }); await db.SaveChangesAsync(); });
        var login = await f.Login(user);
        await f.Run(async (admin, db) =>
        {
            var view = await db.Set<Permiso>().SingleAsync(x => x.Codigo == Permissions.View);
            await admin.PermissionsAsync(general, custom, [view.IdPermiso], "test", default);
            var own = await db.Set<Rol>().SingleAsync(x => x.Codigo == "ADMIN_GENERAL");
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.PermissionsAsync(general, own.IdRol, [], "test", default))).Status);
        });
        await f.Scope(async services => Assert.Null(await services.GetRequiredService<IAuthService>().ValidateAsync(login.Credential, "API", default)));
    }
    [SqlIntegrationFact] public async Task MarketAdminCannotInheritAdministrationOutsideAssignedMarkets()
    {
        await using var f = await Fixture.CreateAsync();
        var actor = await f.User("ADMIN_MERCADO", [f.BusinessB]);
        var own = await f.User("VENDEDOR", [f.BusinessA]);
        var foreign = await f.User("VENDEDOR", [f.BusinessB]);
        await f.Scope(async provider =>
        {
            var db = provider.GetRequiredService<PortalMercadoDbContext>();
            db.Add(new UsuarioMercado { IdUsuario = actor, IdMercado = f.MarketA });
            await db.SaveChangesAsync();
        });
        await f.Run(async (admin, db) =>
        {
            var page = await admin.ListAsync(actor, 1, 100, default);
            Assert.Contains(page.Items, x => x.IdUsuario == own);
            Assert.DoesNotContain(page.Items, x => x.IdUsuario == foreign);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.GetAsync(actor, foreign, default))).Status);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.BusinessesAsync(actor, own, [f.BusinessB], "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task InactiveAssociationDoesNotGrantOwnerAccess()
    {
        await using var f = await Fixture.CreateAsync();
        var owner = await f.User("PROPIETARIO_NEGOCIO", [f.BusinessA]);
        var target = await f.User("VENDEDOR", [f.BusinessA]);
        await f.Scope(async provider =>
        {
            var db = provider.GetRequiredService<PortalMercadoDbContext>();
            await db.Set<UsuarioNegocio>().Where(x => x.IdUsuario == target).ExecuteUpdateAsync(x => x.SetProperty(y => y.Activo, false));
        });
        await f.Run(async (admin, db) =>
        {
            Assert.DoesNotContain((await admin.ListAsync(owner, 1, 100, default)).Items, x => x.IdUsuario == target);
            Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => admin.EditAsync(owner, target,
                new EditUserRequest { Nombre = "No permitido" }, "test", default))).Status);
        });
    }
    [SqlIntegrationFact] public async Task SellerCanSwitchBetweenTwoAuthorizedBusinesses()
    {
        await using var f = await Fixture.CreateAsync(); var seller = await f.User("VENDEDOR", [f.BusinessA, f.BusinessB]);
        var login = await f.Login(seller);
        foreach (var id in new[] { f.BusinessA, f.BusinessB })
        {
            await f.Run(async (admin, db) => await admin.SelectContextAsync(seller, login.Credential, "API", id, "test", default));
            await f.Run(async (admin, db) => Assert.Equal(id, (await admin.ContextAsync(seller, login.Credential, "API", default)).IdNegocioActivo));
        }
        await f.Scope(async provider =>
        {
            var auth = provider.GetRequiredService<IAuthService>();
            var profile = await auth.ValidateAsync(login.Credential, "API", default);
            Assert.NotNull(profile); Assert.DoesNotContain(Permissions.View, profile.Permisos);
        });
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public string Password { get; } = MfaCryptography.NewCredential() + "aA1!";
        public List<long> Users { get; } = []; public List<int> Roles { get; } = [];
        public long MarketA, MarketB, BusinessA, BusinessB;
        public required string KeyPath { get; init; }
        public string Email() => "admin-test-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        public static async Task<Fixture> CreateAsync()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:PortalMercado"] = @"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;",
                ["Security:DataProtectionKeyPath"] = Path.Combine(Path.GetTempPath(), "pm-admin-" + Guid.NewGuid().ToString("N")) }).Build();
            var env = new TestEnvironment(); var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(config); services.AddSingleton<IHostEnvironment>(env);
            services.AddInfrastructure(config); services.AddPortalAccess("API", config, env);
            var f = new Fixture { Provider = services.BuildServiceProvider(), KeyPath = config["Security:DataProtectionKeyPath"]! };
            try
            {
                await f.Scope(async provider =>
                {
                    var db = provider.GetRequiredService<PortalMercadoDbContext>(); Assert.True(await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-003"), "Primero aplicar ET02-003.");
                    var merchant = await db.Negocios.Where(x => x.Slug == "negocio-prueba-et01").Select(x => x.IdComerciante).SingleAsync();
                    var a = new Mercado { Nombre = "Prueba A " + Guid.NewGuid(), Activo = true, CreadoEn = DateTime.UtcNow };
                    db.Add(a); await db.SaveChangesAsync(); f.MarketA = a.IdMercado;
                    var b = new Mercado { Nombre = "Prueba B " + Guid.NewGuid(), Activo = true, CreadoEn = DateTime.UtcNow };
                    db.Add(b); await db.SaveChangesAsync(); f.MarketB = b.IdMercado;
                    var na = new Negocio { IdComerciante = merchant, IdMercado = f.MarketA, NombreComercial = "Prueba A", Slug = "test-" + Guid.NewGuid().ToString("N"), Estado = "ACTIVO" };
                    db.Add(na); await db.SaveChangesAsync(); f.BusinessA = na.IdNegocio;
                    var nb = new Negocio { IdComerciante = merchant, IdMercado = f.MarketB, NombreComercial = "Prueba B", Slug = "test-" + Guid.NewGuid().ToString("N"), Estado = "ACTIVO" };
                    db.Add(nb); await db.SaveChangesAsync(); f.BusinessB = nb.IdNegocio;
                });
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async Task<long> User(string role, long[] businesses)
        {
            long id = 0;
            await Scope(async provider =>
            {
                var db = provider.GetRequiredService<PortalMercadoDbContext>();
                var u = new Usuario { Correo = Email(), Nombre = "Cuenta efímera de prueba", Activo = true, CreadoEn = DateTime.UtcNow };
                u.PasswordHash = provider.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(u, Password); db.Add(u); await db.SaveChangesAsync(); id = u.IdUsuario; Users.Add(id);
                db.Add(new UsuarioSeguridad { IdUsuario = id, Sello = Guid.NewGuid() });
                var r = await db.Set<Rol>().SingleAsync(x => x.Codigo == role); db.Add(new UsuarioRol { IdUsuario = id, IdRol = r.IdRol });
                foreach (var business in businesses) db.Add(new UsuarioNegocio { IdUsuario = id, IdNegocio = business, Activo = true });
                await db.SaveChangesAsync();
            }); return id;
        }
        public async Task<AuthSession> Login(long id)
        {
            AuthSession? result = null;
            await Scope(async provider =>
            {
                var db = provider.GetRequiredService<PortalMercadoDbContext>(); var email = await db.Usuarios.Where(x => x.IdUsuario == id).Select(x => x.Correo).SingleAsync();
                result = (await provider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Correo = email, Password = Password }, "API", "test", default))?.Session;
            }); Assert.NotNull(result); return result;
        }
        public async Task Scope(Func<IServiceProvider,Task> operation) { await using var scope = Provider.CreateAsyncScope(); await operation(scope.ServiceProvider); }
        public Task Run(Func<IUserAdministration,PortalMercadoDbContext,Task> operation) => Scope(p => operation(p.GetRequiredService<IUserAdministration>(), p.GetRequiredService<PortalMercadoDbContext>()));
        public async ValueTask DisposeAsync()
        {
            try { await Scope(async p => {
                var db = p.GetRequiredService<PortalMercadoDbContext>();
                await db.Set<UsuarioMfaRecuperacion>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioMfaReto>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioMfa>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioSesion>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<AuditoriaAcceso>().Where(x => (x.IdActor != null && Users.Contains(x.IdActor.Value)) || (x.IdObjetivo != null && Users.Contains(x.IdObjetivo.Value))).ExecuteDeleteAsync();
                await db.Set<UsuarioRol>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioNegocio>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioMercado>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<UsuarioSeguridad>().Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Usuarios.Where(x => Users.Contains(x.IdUsuario)).ExecuteDeleteAsync();
                await db.Set<RolPermiso>().Where(x => Roles.Contains(x.IdRol)).ExecuteDeleteAsync(); await db.Set<Rol>().Where(x => Roles.Contains(x.IdRol)).ExecuteDeleteAsync();
                await db.Negocios.Where(x => x.IdNegocio == BusinessA || x.IdNegocio == BusinessB).ExecuteDeleteAsync();
                await db.Mercados.Where(x => x.IdMercado == MarketA || x.IdMercado == MarketB).ExecuteDeleteAsync();
            }); }
            finally { await Provider.DisposeAsync(); if (Directory.Exists(KeyPath)) Directory.Delete(KeyPath, true); }
        }
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development"; public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ""; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

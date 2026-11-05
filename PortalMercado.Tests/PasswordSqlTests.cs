using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.Tests;

[Trait("Category","SqlIntegration")]
public sealed class PasswordSqlTests
{
    [SqlIntegrationFact] public async Task ResetStoresOnlyHashIsSingleUseAndRevokesOldSession()
    {
        await using var f=await Fixture.CreateAsync();var session=await f.Login();var token=await f.Request();
        await f.Run(async (p,db)=> { var row=await db.Set<UsuarioTokenPassword>().SingleAsync(x=>x.IdUsuario==f.UserId);Assert.Equal(MfaCryptography.Hash(token),row.TokenHash);Assert.NotEqual(token,row.TokenHash); });
        await f.Reset(token);Assert.False(await f.Valid(session));
        Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(token))).Status);
        await f.Run(async (p,db)=> {
            var auth=p.GetRequiredService<IAuthService>();Assert.Null(await auth.LoginAsync(new LoginRequest { Correo=f.Email,Password=f.Password },"API","test",default));
            Assert.NotNull((await auth.LoginAsync(new LoginRequest { Correo=f.Email,Password=f.NewPassword },"API","test",default))?.Session);
            Assert.True(await db.Set<AuditoriaAcceso>().AnyAsync(x=>x.IdActor==f.UserId && x.Evento=="PASSWORD_RESTABLECIDA"));
        });
    }
    [SqlIntegrationFact] public async Task ExpiredTokenCannotChangePassword()
    {
        await using var f=await Fixture.CreateAsync();var token=await f.Request();f.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(token))).Status);Assert.NotNull(await f.Login());
    }
    [SqlIntegrationFact] public async Task NewRequestInvalidatesPreviousLink()
    {
        await using var f=await Fixture.CreateAsync();var a=await f.Request();var b=await f.Request();Assert.NotEqual(a,b);
        Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(a))).Status);await f.Reset(b);
    }
    [SqlIntegrationFact] public async Task UnknownAndInactiveAccountsReceiveSameKindOfDeliveryWithoutUsableToken()
    {
        await using var f=await Fixture.CreateAsync();var unknown="missing-"+Guid.NewGuid().ToString("N")+"@example.invalid";
        var a=await f.Request(unknown);
        await f.Run(async (p,db)=> { var u=await db.Usuarios.FindAsync(f.UserId);u!.Activo=false;await db.SaveChangesAsync(); });
        var b=await f.Request();Assert.Equal(2,f.Delivery.Messages.Count);
        await f.Run(async (p,db)=>Assert.Equal(0,await db.Set<UsuarioTokenPassword>().CountAsync(x=>x.IdUsuario==f.UserId)));
        Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(a))).Status);Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(b))).Status);
    }
    [SqlIntegrationFact] public async Task FailedDeliveryReports503AndRevokesGeneratedToken()
    {
        await using var f=await Fixture.CreateAsync();f.Delivery.Fail=true;
        Assert.Equal(503,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Request())).Status);
        await f.Run(async (p,db)=> { var row=await db.Set<UsuarioTokenPassword>().SingleAsync(x=>x.IdUsuario==f.UserId);Assert.NotNull(row.RevocadoEn); });
        Assert.NotNull(await f.Login());
    }
    [SqlIntegrationFact] public async Task PasswordChangeRevokesApiMvcSessionsAndRecoveryTokens()
    {
        await using var f=await Fixture.CreateAsync();var api=await f.Login();var mvc=await f.Login("MVC");var token=await f.Request();
        await f.Run(async (p,db)=>await p.GetRequiredService<IPasswordService>().ChangeAsync(f.UserId,api.Credential,"API",new ChangePasswordRequest { CurrentPassword=f.Password,NewPassword=f.NewPassword,ConfirmPassword=f.NewPassword },"test",default));
        Assert.False(await f.Valid(api));Assert.False(await f.Valid(mvc,"MVC"));Assert.Equal(400,(await Assert.ThrowsAsync<PasswordFailure>(()=>f.Reset(token))).Status);
    }
    [SqlIntegrationFact] public async Task RepeatedWrongCurrentPasswordBlocksSessionWithoutChangingHash()
    {
        await using var f=await Fixture.CreateAsync();var session=await f.Login();
        for(var i=0;i<5;i++) await Assert.ThrowsAsync<PasswordFailure>(()=>f.Run(async (p,db)=>await p.GetRequiredService<IPasswordService>().ChangeAsync(f.UserId,session.Credential,"API",new ChangePasswordRequest { CurrentPassword="Incorrecta123!",NewPassword=f.NewPassword,ConfirmPassword=f.NewPassword },"test",default)));
        Assert.False(await f.Valid(session));
        await f.Run(async (p,db)=> { var state=await db.Set<UsuarioSeguridad>().FindAsync(f.UserId);Assert.Equal(5,state!.IntentosFallidos);Assert.True(state.BloqueadoHasta>f.Clock.GetUtcNow().UtcDateTime); });
    }
    [SqlIntegrationFact] public async Task ConcurrentResetConsumesTokenExactlyOnce()
    {
        await using var f=await Fixture.CreateAsync();var token=await f.Request();
        async Task<bool> Attempt() { try { await f.Reset(token);return true; } catch(PasswordFailure) { return false; } }
        var results=await Task.WhenAll(Attempt(),Attempt());Assert.Equal(1,results.Count(x=>x));
    }
    [SqlIntegrationFact] public async Task ResetPreservesAdministratorMfaAndStillRequiresSecondFactor()
    {
        await using var f=await Fixture.CreateAsync();
        await f.Run(async (p,db)=> {
            var u=(await db.Usuarios.FindAsync(f.UserId))!;u.DosFactoresHabilitado=true;
            await db.Set<UsuarioRol>().Where(x=>x.IdUsuario==f.UserId).ExecuteDeleteAsync();
            var role=await db.Set<Rol>().SingleAsync(x=>x.Codigo=="ADMIN_GENERAL");db.Add(new UsuarioRol { IdUsuario=f.UserId,IdRol=role.IdRol });
            db.Add(new UsuarioMfa { IdUsuario=f.UserId,ActivadoEn=f.Clock.GetUtcNow().UtcDateTime,SecretProtegido=p.GetRequiredService<MfaCryptography>().Protect(MfaCryptography.NewSecret()) });
            db.Add(new UsuarioMfaRecuperacion { IdUsuario=f.UserId,CodigoHash=MfaCryptography.Hash(MfaCryptography.RecoveryCode()) });await db.SaveChangesAsync();
        });
        await f.Reset(await f.Request());
        await f.Run(async (p,db)=> {
            var outcome=await p.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Correo=f.Email,Password=f.NewPassword },"API","test",default);
            Assert.NotNull(outcome?.Mfa);Assert.Null(outcome.Session);Assert.False(outcome.Mfa.EnrollmentRequired);
            Assert.True((await db.Usuarios.FindAsync(f.UserId))!.DosFactoresHabilitado);Assert.Equal(1,await db.Set<UsuarioMfaRecuperacion>().CountAsync(x=>x.IdUsuario==f.UserId));
        });
    }
    [SqlIntegrationFact] public async Task AccountThrottleIsSharedAcrossScopes()
    {
        await using var f=await Fixture.CreateAsync();await f.Request();await f.Request();await f.Request();
        await f.Run(async (p,db)=>await p.GetRequiredService<IPasswordService>().RequestAsync(new ForgotPasswordRequest { Correo=f.Email },"test",default));
        Assert.Equal(3,f.Delivery.Messages.Count);
        f.Clock.Advance(TimeSpan.FromMinutes(16));await f.Request();Assert.Equal(4,f.Delivery.Messages.Count);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; } public required string KeyPath { get; init; }
        public long UserId;public string Email { get; }="password-test-"+Guid.NewGuid().ToString("N")+"@example.invalid";
        public string Password { get; }=MfaCryptography.NewCredential()+"aA1!";public string NewPassword { get; }=MfaCryptography.NewCredential()+"bB2!";
        public Clock Clock { get; private set; }=null!;public CaptureDelivery Delivery { get; private set; }=null!;private readonly HashSet<string> emails=[];
        private string Correlation { get; }="pw-"+Guid.NewGuid().ToString("N");
        public static async Task<Fixture> CreateAsync()
        {
            var keyPath=Path.Combine(Path.GetTempPath(),"pm-password-"+Guid.NewGuid().ToString("N"));
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:PortalMercado"]=@"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;",
                ["Security:DataProtectionKeyPath"]=keyPath,["Recovery:WebBaseUrl"]="https://localhost:7275/" }).Build();
            var env=new Env();var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(config);services.AddSingleton<IHostEnvironment>(env);
            services.AddInfrastructure(config);services.AddPortalAccess("API",config,env);
            // Instanciar controles antes de construir el proveedor.
            var clock=new Clock();var delivery=new CaptureDelivery();services.AddSingleton<TimeProvider>(clock);services.AddSingleton<IRecoveryDelivery>(delivery);
            var f=new Fixture { Provider=services.BuildServiceProvider(),KeyPath=keyPath };f.Clock=clock;f.Delivery=delivery;
            // El reloj y la entrega se comparten con todas las solicitudes del fixture.
            try
            {
                await f.Run(async (p,db)=> {
                    Assert.True(await db.Set<SeguridadVersion>().AnyAsync(x=>x.Codigo=="ET02-004"),"Aplicar ET02-004 antes de las pruebas SQL de contraseña.");
                    var u=new Usuario { Correo=f.Email,Nombre="Cuenta efímera de contraseña",Activo=true,CreadoEn=clock.GetUtcNow().UtcDateTime };
                    u.PasswordHash=p.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(u,f.Password);db.Add(u);await db.SaveChangesAsync();f.UserId=u.IdUsuario;
                    db.Add(new UsuarioSeguridad { IdUsuario=u.IdUsuario,Sello=Guid.NewGuid() });
                    var r=await db.Set<Rol>().SingleAsync(x=>x.Codigo=="VENDEDOR");db.Add(new UsuarioRol { IdUsuario=u.IdUsuario,IdRol=r.IdRol });await db.SaveChangesAsync();
                });return f;
            }
            catch { await f.DisposeAsync();throw; }
        }
        public async Task<string> Request(string? address=null)
        {
            address??=Email;emails.Add(CredentialPolicy.NormalizeEmail(address)!);
            await Run(async (p,db)=>await p.GetRequiredService<IPasswordService>().RequestAsync(new ForgotPasswordRequest { Correo=address },Correlation,default));
            return Delivery.Messages.Last().Link.Query[7..];
        }
        public Task Reset(string token)=>Run(async (p,db)=>await p.GetRequiredService<IPasswordService>().ResetAsync(new ResetPasswordRequest { Token=token,NewPassword=NewPassword,ConfirmPassword=NewPassword },"test",default));
        public async Task<AuthSession> Login(string host="API")
        {
            AuthSession? result=null;await Run(async (p,db)=>result=(await p.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Correo=Email,Password=Password },host,"test",default))?.Session);Assert.NotNull(result);return result;
        }
        public async Task<bool> Valid(AuthSession session,string host="API") { bool valid=false;await Run(async (p,db)=>valid=await p.GetRequiredService<IAuthService>().ValidateAsync(session.Credential,host,default)!=null);return valid; }
        public async Task Run(Func<IServiceProvider,PortalMercadoDbContext,Task> operation)
        { await using var scope=Provider.CreateAsyncScope();await operation(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<PortalMercadoDbContext>()); }
        public async ValueTask DisposeAsync()
        {
            try { if(UserId!=0 || emails.Count>0) await Run(async (p,db)=> {
                await db.Set<UsuarioTokenPassword>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();
                var hashes=emails.Select(MfaCryptography.Hash).ToArray();await db.Set<RecuperacionLimite>().Where(x=>hashes.Contains(x.CorreoHash)).ExecuteDeleteAsync();
                await db.Set<UsuarioMfaRecuperacion>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();await db.Set<UsuarioMfaReto>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();await db.Set<UsuarioMfa>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();
                await db.Set<UsuarioSesion>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();
                await db.Set<AuditoriaAcceso>().Where(x=>(UserId!=0 && (x.IdActor==UserId || x.IdObjetivo==UserId)) || x.CorrelationId==Correlation).ExecuteDeleteAsync();
                await db.Set<UsuarioRol>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();await db.Set<UsuarioSeguridad>().Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();await db.Usuarios.Where(x=>x.IdUsuario==UserId).ExecuteDeleteAsync();
            }); }
            finally { await Provider.DisposeAsync();if(Directory.Exists(KeyPath)) Directory.Delete(KeyPath,true); }
        }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Current=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Current;public void Advance(TimeSpan by)=>Current+=by;
    }
    private sealed class CaptureDelivery : IRecoveryDelivery
    {
        public bool Fail;public ConcurrentQueue<(string Recipient,Uri Link)> Messages { get; }=new();
        public void CheckConfiguration() { }
        public Task DeliverAsync(string recipient,Uri resetLink,CancellationToken ct)
        { if(Fail) throw new PasswordFailure(503,PasswordMessages.Unavailable);Messages.Enqueue((recipient,resetLink));return Task.CompletedTask; }
    }
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; }="Development";public string ApplicationName { get; set; }="Tests";
        public string ContentRootPath { get; set; }="";public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    }
}

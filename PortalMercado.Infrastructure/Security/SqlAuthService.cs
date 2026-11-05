using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Security;

public sealed class SqlAuthService(PortalMercadoDbContext db, IPasswordHasher<Usuario> hasher,
    TimeProvider clock, MfaCryptography crypto) : IAuthService
{
    // Desconocidos/inactivos también recorren PBKDF2. No es una contraseña de cuenta.
    private static readonly Usuario DummyUser = new();
    private static readonly string DummyHash = new PasswordHasher<Usuario>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 210_000 })).HashPassword(
        DummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<LoginOutcome?> LoginAsync(LoginRequest request, string host, string correlationId, CancellationToken ct)
    {
        CheckHost(host);
        var normalized = CredentialPolicy.NormalizeEmail(request.Correo);
        var password = request.Password ?? "";
        if (normalized is null || password.Length is < 1 or > 128) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa intentos contra la misma cuenta, incluso entre procesos API/MVC.
        var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE CorreoNormalizado={normalized}")
            .SingleOrDefaultAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var state = user is null ? null : await db.Set<UsuarioSeguridad>().FindAsync([user.IdUsuario], ct);
        var verification = PasswordVerificationResult.Failed;
        try
        {
            verification = hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyHash, password);
        }
        catch (FormatException) { hasher.VerifyHashedPassword(DummyUser, DummyHash, password); }
        catch (ArgumentException) { hasher.VerifyHashedPassword(DummyUser, DummyHash, password); }
        if (user is null || state is null || state.BloqueadoHasta > now)
        {
            Audit(user?.IdUsuario, "LOGIN_RECHAZADO", correlationId, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        if (verification == PasswordVerificationResult.Failed)
        {
            // Al vencer el bloqueo empieza una ventana nueva de cinco intentos.
            if (state.BloqueadoHasta.HasValue && state.BloqueadoHasta <= now) state.IntentosFallidos = 0;
            state.IntentosFallidos++;
            if (state.IntentosFallidos >= 5) state.BloqueadoHasta = now.AddMinutes(15);
            Audit(user.IdUsuario, state.IntentosFallidos >= 5 ? "CUENTA_BLOQUEADA" : "LOGIN_RECHAZADO", correlationId, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        var profile = await ProfileAsync(user, ct);
        if (AuthenticationRules.RequiresSecondFactor(user.DosFactoresHabilitado, profile.Roles))
        {
            var mfa = await db.Set<UsuarioMfa>().FindAsync([user.IdUsuario], ct);
            var enrolling = mfa is { PendienteBootstrap: true, ActivadoEn: null } && !user.Activo
                && AuthenticationRules.RequiresSecondFactor(false, profile.Roles);
            if (mfa is null || (!enrolling && (!user.Activo || !user.DosFactoresHabilitado || mfa.ActivadoEn is null)))
            {
                Audit(user.IdUsuario, "LOGIN_RECHAZADO", correlationId, now);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
            }
            if (enrolling && mfa.SecretProtegido is null) mfa.SecretProtegido = crypto.Protect(MfaCryptography.NewSecret());
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = hasher.HashPassword(user, password);
            // Un solo reto pendiente por cuenta, aun si alterna API y MVC.
            await db.Set<UsuarioMfaReto>().Where(x => x.IdUsuario == user.IdUsuario && x.ConsumidoEn == null)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.ConsumidoEn, now), ct);
            var challenge = MfaCryptography.NewCredential(); var expiry = now.AddMinutes(5);
            db.Add(new UsuarioMfaReto { IdReto = Guid.NewGuid(), IdUsuario = user.IdUsuario,
                TokenHash = MfaCryptography.Hash(challenge), Host = host, Sello = state.Sello,
                Enrolamiento = enrolling, ExpiraEn = expiry });
            Audit(user.IdUsuario, "MFA_RETO_CREADO", correlationId, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new LoginOutcome(null, new MfaChallenge(challenge, enrolling, expiry));
        }
        if (!user.Activo)
        {
            Audit(user.IdUsuario, "LOGIN_RECHAZADO", correlationId, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        var session = AuthPersistence.Issue(db, user, state, profile, host, correlationId, now, mfa: false);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new LoginOutcome(session, null);
    }

    public async Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct)
    {
        CheckHost(host);
        if (!ValidCredential(credential)) return null;
        var hash = HashCredential(credential);
        var now = clock.GetUtcNow().UtcDateTime;
        var session = await db.Set<UsuarioSesion>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.Host == host && x.RevocadoEn == null && x.ExpiraEn > now, ct);
        if (session is null) return null;
        var user = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == session.IdUsuario && x.Activo, ct);
        var state = await db.Set<UsuarioSeguridad>().AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == session.IdUsuario, ct);
        if (user is null || state is null || state.Sello != session.Sello || state.BloqueadoHasta > now) return null;
        var profile = await ProfileAsync(user, ct);
        if (AuthenticationRules.RequiresSecondFactor(user.DosFactoresHabilitado, profile.Roles))
        {
            if (!session.MfaVerificado || !user.DosFactoresHabilitado) return null;
            var mfa = await db.Set<UsuarioMfa>().AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == user.IdUsuario, ct);
            if (mfa is null || mfa.ActivadoEn is null || mfa.PendienteBootstrap) return null;
        }
        return profile;
    }

    public async Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct)
    {
        CheckHost(host);
        if (!ValidCredential(credential)) return;
        var hash = HashCredential(credential);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await db.Set<UsuarioSesion>().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Host == host, ct);
        if (session is not null && session.RevocadoEn is null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            session.RevocadoEn = now; Audit(session.IdUsuario, "LOGOUT", correlationId, now);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private Task<AuthProfile> ProfileAsync(Usuario user, CancellationToken ct) => AuthPersistence.ProfileAsync(db, user, ct);
    private void Audit(long? actor, string action, string correlationId, DateTime now) => db.Add(new AuditoriaAcceso
    {
        IdActor = actor, Evento = action, FechaUtc = now,
        CorrelationId = correlationId.Length <= 64 && correlationId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? correlationId : Guid.NewGuid().ToString("N")
    });
    private static string HashCredential(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    private static bool ValidCredential(string? value) => value is { Length: 43 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static void CheckHost(string host)
    {
        if (host is not ("API" or "MVC")) throw new ArgumentException("Host no válido.", nameof(host));
    }
}

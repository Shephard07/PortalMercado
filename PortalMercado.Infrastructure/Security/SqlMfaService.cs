using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;
namespace PortalMercado.Infrastructure.Security;

public sealed class SqlMfaService(PortalMercadoDbContext db, MfaCryptography crypto, TimeProvider clock) : IMfaService
{
    public async Task<MfaEnrollment?> GetEnrollmentAsync(string challenge, string host, CancellationToken ct)
    {
        AuthPersistence.CheckHost(host);
        if (!MfaCryptography.IsCredential(challenge)) return null;
        var now = clock.GetUtcNow().UtcDateTime; var hash = MfaCryptography.Hash(challenge);
        var pending = await db.Set<UsuarioMfaReto>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.TokenHash == hash && x.Host == host && x.Enrolamiento && x.ConsumidoEn == null && x.ExpiraEn > now && x.Intentos < 5, ct);
        if (pending is null) return null;
        var user = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == pending.IdUsuario, ct);
        var security = await db.Set<UsuarioSeguridad>().AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == pending.IdUsuario, ct);
        var mfa = await db.Set<UsuarioMfa>().AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == pending.IdUsuario, ct);
        if (user is null || user.Activo || security is null || security.Sello != pending.Sello || security.BloqueadoHasta > now
            || mfa is not { PendienteBootstrap: true, ActivadoEn: null } || mfa.SecretProtegido is null) return null;
        var profile = await AuthPersistence.ProfileAsync(db, user, ct);
        if (!AuthenticationRules.RequiresSecondFactor(false, profile.Roles)) return null;
        var secret = crypto.Unprotect(mfa.SecretProtegido);
        return new MfaEnrollment(secret, MfaCryptography.EnrollmentUri(secret, user.Correo));
    }

    public async Task<MfaCompletion?> CompleteAsync(string challenge, string code, string host, string correlationId, CancellationToken ct, bool regenerateRecoveryCodes = false)
    {
        AuthPersistence.CheckHost(host);
        if (!MfaCryptography.IsCredential(challenge) || string.IsNullOrWhiteSpace(code) || code.Length > 64) return null;
        var hash = MfaCryptography.Hash(challenge); var now = clock.GetUtcNow().UtcDateTime;
        var initial = await db.Set<UsuarioMfaReto>().AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Host == host, ct);
        if (initial is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Misma fila y orden de bloqueo que la fase de contraseña. Serializa consumo, TOTP y recuperación.
        var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE IdUsuario={initial.IdUsuario}")
            .SingleOrDefaultAsync(ct);
        var pending = await db.Set<UsuarioMfaReto>().SingleOrDefaultAsync(x => x.IdReto == initial.IdReto, ct);
        var security = await db.Set<UsuarioSeguridad>().FindAsync([initial.IdUsuario], ct);
        var mfa = await db.Set<UsuarioMfa>().FindAsync([initial.IdUsuario], ct);
        if (user is null || pending is null || pending.ConsumidoEn.HasValue || pending.ExpiraEn <= now || pending.Intentos >= 5
            || security is null || security.Sello != pending.Sello || security.BloqueadoHasta > now || mfa?.SecretProtegido is null)
            return null;
        var profile = await AuthPersistence.ProfileAsync(db, user, ct);
        var enrollmentAllowed = !user.Activo && mfa.PendienteBootstrap && mfa.ActivadoEn is null
            && AuthenticationRules.RequiresSecondFactor(false, profile.Roles);
        if (pending.Enrolamiento ? !enrollmentAllowed : (!user.Activo || !user.DosFactoresHabilitado || mfa.PendienteBootstrap || mfa.ActivadoEn is null))
            return null;
        // Releer el reloj tras esperar el bloqueo SQL.
        now = clock.GetUtcNow().UtcDateTime;
        if (pending.ExpiraEn <= now || security.BloqueadoHasta > now) return null;
        var secret = crypto.Unprotect(mfa.SecretProtegido);
        var isTotp = MfaCryptography.TryVerifyTotp(secret, code.Trim(), now, mfa.UltimoPasoTotp, out var matchedStep);
        UsuarioMfaRecuperacion? recovery = null;
        if (!isTotp && !pending.Enrolamiento)
        {
            var normalized = MfaCryptography.NormalizeRecovery(code);
            if (normalized is not null)
            {
                var recoveryHash = MfaCryptography.Hash(normalized);
                recovery = await db.Set<UsuarioMfaRecuperacion>().SingleOrDefaultAsync(x =>
                    x.IdUsuario == user.IdUsuario && x.CodigoHash == recoveryHash && x.UsadoEn == null, ct);
            }
        }
        if (!isTotp && recovery is null)
        {
            pending.Intentos++; AuthPersistence.Fail(security, now);
            if (pending.Intentos >= 5) pending.ConsumidoEn = now;
            AuthPersistence.Audit(db, user.IdUsuario, security.IntentosFallidos >= 5 ? "MFA_CUENTA_BLOQUEADA" : "MFA_RECHAZADO", correlationId, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        if (isTotp) mfa.UltimoPasoTotp = matchedStep;
        if (recovery is not null) recovery.UsadoEn = now;
        string[] codes = [];
        if (pending.Enrolamiento)
        {
            // Estado y sello cambian atómicamente con la primera sesión MFA.
            mfa.PendienteBootstrap = false; mfa.ActivadoEn = now;
            user.Activo = true; user.DosFactoresHabilitado = true;
            security.Sello = Guid.NewGuid();
            codes = Enumerable.Range(0, 10).Select(_ => MfaCryptography.RecoveryCode()).ToArray();
            foreach (var value in codes) db.Add(new UsuarioMfaRecuperacion { IdUsuario = user.IdUsuario, CodigoHash = MfaCryptography.Hash(value) });
            AuthPersistence.Audit(db, user.IdUsuario, "MFA_ENROLADO", correlationId, now);
        }
        if (!pending.Enrolamiento && regenerateRecoveryCodes)
        {
            // La contraseña se verificó al crear este reto y el segundo factor se acaba de consumir.
            // Rotar también el sello invalida las sesiones y retos anteriores.
            if (recovery is not null) db.Entry(recovery).State = EntityState.Detached;
            await db.Set<UsuarioMfaRecuperacion>().Where(x => x.IdUsuario == user.IdUsuario).ExecuteDeleteAsync(ct);
            security.Sello = Guid.NewGuid();
            codes = Enumerable.Range(0, 10).Select(_ => MfaCryptography.RecoveryCode()).ToArray();
            foreach (var value in codes) db.Add(new UsuarioMfaRecuperacion { IdUsuario = user.IdUsuario, CodigoHash = MfaCryptography.Hash(value) });
            AuthPersistence.Audit(db, user.IdUsuario, "MFA_RECUPERACION_REGENERADA", correlationId, now);
        }
        pending.ConsumidoEn = now;
        AuthPersistence.Audit(db, user.IdUsuario, recovery is null ? "MFA_VERIFICADO" : "MFA_RECUPERACION_USADA", correlationId, now);
        var session = AuthPersistence.Issue(db, user, security, profile, host, correlationId, now, mfa: true);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new MfaCompletion(session, codes);
    }
}

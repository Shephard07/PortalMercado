using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;
namespace PortalMercado.Infrastructure.Security;

public sealed class SqlPasswordService(PortalMercadoDbContext db, IPasswordHasher<Usuario> hasher, IAuthService auth,
    IRecoveryDelivery delivery, IConfiguration config, TimeProvider clock) : IPasswordService
{
    public async Task RequestAsync(ForgotPasswordRequest request, string correlation, CancellationToken ct)
    {
        var normalized = CredentialPolicy.NormalizeEmail(request.Correo);
        if (normalized is null) throw new PasswordFailure(400, "Escribe un correo válido.");
        delivery.CheckConfiguration();
        var token = MfaCryptography.NewCredential();
        var link = ResetLink(token);
        var emailHash = MfaCryptography.Hash(normalized); var now = clock.GetUtcNow().UtcDateTime;
        long? userId = null; Guid? tokenId = null;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var resource = "PortalMercado.Recovery." + emailHash;
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50040,'Recuperación ocupada.',1;", ct);
            var bucket = await db.Set<RecuperacionLimite>().FindAsync([emailHash], ct);
            if (bucket is null) { bucket = new RecuperacionLimite { CorreoHash = emailHash, VentanaUtc = now }; db.Add(bucket); }
            if (bucket.VentanaUtc <= now.AddMinutes(-15)) { bucket.VentanaUtc = now; bucket.Solicitudes = 0; }
            if (bucket.Solicitudes >= 3) { await tx.CommitAsync(ct); return; }
            bucket.Solicitudes++;
            var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE CorreoNormalizado={normalized}").SingleOrDefaultAsync(ct);
            var security = user is null ? null : await db.Set<UsuarioSeguridad>().FindAsync([user.IdUsuario], ct);
            if (user is { Activo: true } && security is not null)
            {
                userId = user.IdUsuario; tokenId = Guid.NewGuid();
                await db.Set<UsuarioTokenPassword>().Where(x => x.IdUsuario == user.IdUsuario && x.ConsumidoEn == null && x.RevocadoEn == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, now), ct);
                db.Add(new UsuarioTokenPassword { IdToken = tokenId.Value, IdUsuario = user.IdUsuario, Sello = security.Sello,
                    TokenHash = MfaCryptography.Hash(token), CreadoEn = now, ExpiraEn = now.AddMinutes(15) });
            }
            AuthPersistence.Audit(db, null, "PASSWORD_RECUPERACION_SOLICITADA", correlation, now).IdObjetivo = userId;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        try { await delivery.DeliverAsync(request.Correo.Trim(), link, ct); }
        catch
        {
            // Nunca dejar utilizable el token cuyo correo no pudo entregarse.
            if (tokenId.HasValue)
                await db.Set<UsuarioTokenPassword>().Where(x => x.IdToken == tokenId.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, clock.GetUtcNow().UtcDateTime), CancellationToken.None);
            AuthPersistence.Audit(db, null, "PASSWORD_CORREO_NO_ENTREGADO", correlation, clock.GetUtcNow().UtcDateTime).IdObjetivo = userId;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
    private Uri ResetLink(string token)
    {
        if (!Uri.TryCreate(config["Recovery:WebBaseUrl"], UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new PasswordFailure(503, PasswordMessages.Unavailable);
        // Nunca construir el origen a partir de Host, Referer o datos del solicitante.
        return new Uri(origin, "Cuenta/Restablecer?token=" + token);
    }
    public async Task ResetAsync(ResetPasswordRequest request, string correlation, CancellationToken ct)
    {
        ValidateNew(request.NewPassword, request.ConfirmPassword);
        if (!MfaCryptography.IsCredential(request.Token)) throw Invalid();
        var hash = MfaCryptography.Hash(request.Token);
        var initial = await db.Set<UsuarioTokenPassword>().AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (initial is null) throw Invalid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE IdUsuario={initial.IdUsuario}").SingleOrDefaultAsync(ct);
        var state = await db.Set<UsuarioSeguridad>().FindAsync([initial.IdUsuario], ct);
        var row = await db.Set<UsuarioTokenPassword>().SingleAsync(x => x.IdToken == initial.IdToken, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (user is not { Activo: true } || state is null || row.Sello != state.Sello || row.ExpiraEn <= now || row.ConsumidoEn.HasValue || row.RevocadoEn.HasValue) throw Invalid();
        if (Verify(user, request.NewPassword)) throw Invalid();
        await ReplaceAsync(user, state, request.NewPassword, now, ct);
        row.ConsumidoEn = now;
        AuthPersistence.Audit(db, user.IdUsuario, "PASSWORD_RESTABLECIDA", correlation, now).IdObjetivo = user.IdUsuario;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task ChangeAsync(long actor, string credential, string host, ChangePasswordRequest request, string correlation, CancellationToken ct)
    {
        ValidateNew(request.NewPassword, request.ConfirmPassword);
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128) throw new PasswordFailure(400, "Revisa la contraseña actual.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE IdUsuario={actor}").SingleOrDefaultAsync(ct);
        var profile = await auth.ValidateAsync(credential, host, ct);
        if (user is null || profile?.IdUsuario != actor) throw new PasswordFailure(401, "La sesión ya no está disponible.");
        var state = (await db.Set<UsuarioSeguridad>().FindAsync([actor], ct))!; var now = clock.GetUtcNow().UtcDateTime;
        if (!Verify(user, request.CurrentPassword))
        {
            AuthPersistence.Fail(state, now);
            AuthPersistence.Audit(db, actor, state.IntentosFallidos >= 5 ? "PASSWORD_CAMBIO_BLOQUEADO" : "PASSWORD_ACTUAL_RECHAZADA", correlation, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw new PasswordFailure(400, "No se pudo cambiar la contraseña. Revisa los datos o inicia sesión nuevamente.");
        }
        if (request.NewPassword == request.CurrentPassword) throw new PasswordFailure(400, "La nueva contraseña debe ser diferente de la actual.");
        await ReplaceAsync(user, state, request.NewPassword, now, ct);
        AuthPersistence.Audit(db, actor, "PASSWORD_CAMBIADA", correlation, now).IdObjetivo = actor;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task ReplaceAsync(Usuario user, UsuarioSeguridad state, string password, DateTime now, CancellationToken ct)
    {
        user.PasswordHash = hasher.HashPassword(user, password); state.Sello = Guid.NewGuid();
        state.IntentosFallidos = 0; state.BloqueadoHasta = null;
        await db.Set<UsuarioTokenPassword>().Where(x => x.IdUsuario == user.IdUsuario && x.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, now), ct);
        await db.Set<UsuarioSesion>().Where(x => x.IdUsuario == user.IdUsuario && x.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, now), ct);
        await db.Set<UsuarioMfaReto>().Where(x => x.IdUsuario == user.IdUsuario && x.ConsumidoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumidoEn, now), ct);
        // No modificar Activo, roles, asociaciones, secreto MFA ni códigos de segundo factor.
    }
    private bool Verify(Usuario user, string password)
    {
        try { return hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed; }
        catch (Exception e) when (e is FormatException or ArgumentException) { return false; }
    }
    private static void ValidateNew(string? password, string? confirmation)
    { if (!CredentialPolicy.IsValidPassword(password) || password != confirmation) throw new PasswordFailure(400, PasswordMessages.Policy); }
    private static PasswordFailure Invalid() => new(400, PasswordMessages.Invalid);
}

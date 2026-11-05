using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;
namespace PortalMercado.Infrastructure.Security;

internal static class AuthPersistence
{
    public static AuditoriaAcceso Audit(PortalMercadoDbContext db, long? actor, string action, string correlationId, DateTime now)
    {
        var entry = new AuditoriaAcceso
        {
        IdActor = actor, Evento = action, FechaUtc = now,
        CorrelationId = correlationId.Length <= 64 && correlationId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? correlationId : Guid.NewGuid().ToString("N")
        };
        db.Add(entry); return entry;
    }
    public static async Task<AuthProfile> ProfileAsync(PortalMercadoDbContext db, Usuario user, CancellationToken ct)
    {
        var roles = await (from ur in db.Set<UsuarioRol>().AsNoTracking()
            join r in db.Set<Rol>() on ur.IdRol equals r.IdRol
            where ur.IdUsuario == user.IdUsuario select r.Codigo).ToArrayAsync(ct);
        var negocios = await (from un in db.Set<UsuarioNegocio>().AsNoTracking()
            join n in db.Negocios on un.IdNegocio equals n.IdNegocio
            join m in db.Mercados on n.IdMercado equals m.IdMercado
            where un.IdUsuario == user.IdUsuario && un.Activo && n.Estado == "ACTIVO" && m.Activo
            select n.IdNegocio).ToArrayAsync(ct);
        var mercados = await (from um in db.Set<UsuarioMercado>().AsNoTracking()
            join m in db.Mercados on um.IdMercado equals m.IdMercado
            where um.IdUsuario == user.IdUsuario && m.Activo select m.IdMercado).ToArrayAsync(ct);
        var permissions = await (from ur in db.Set<UsuarioRol>().AsNoTracking()
            join rp in db.Set<RolPermiso>() on ur.IdRol equals rp.IdRol
            join p in db.Set<Permiso>() on rp.IdPermiso equals p.IdPermiso
            where ur.IdUsuario == user.IdUsuario select p.Codigo).Distinct().ToArrayAsync(ct);
        return new AuthProfile(user.IdUsuario, user.Correo, user.Nombre, roles, negocios, mercados) { Permisos = permissions };
    }
    public static AuthSession Issue(PortalMercadoDbContext db, Usuario user, UsuarioSeguridad state,
        AuthProfile profile, string host, string correlationId, DateTime now, bool mfa)
    {
        state.IntentosFallidos = 0; state.BloqueadoHasta = null; user.UltimoAcceso = now;
        var credential = MfaCryptography.NewCredential(); var expires = now.AddMinutes(30);
        db.Add(new UsuarioSesion { IdSesion = Guid.NewGuid(), IdUsuario = user.IdUsuario,
            TokenHash = MfaCryptography.Hash(credential), Host = host, Sello = state.Sello,
            CreadoEn = now, ExpiraEn = expires, MfaVerificado = mfa });
        Audit(db, user.IdUsuario, "LOGIN_CORRECTO", correlationId, now);
        return new AuthSession(credential, expires, profile);
    }
    public static void Fail(UsuarioSeguridad state, DateTime now)
    {
        if (state.BloqueadoHasta.HasValue && state.BloqueadoHasta <= now) state.IntentosFallidos = 0;
        state.IntentosFallidos++;
        if (state.IntentosFallidos >= 5) state.BloqueadoHasta = now.AddMinutes(15);
    }
    public static void CheckHost(string host)
    {
        if (host is not ("API" or "MVC")) throw new ArgumentException("Host no válido.", nameof(host));
    }
}

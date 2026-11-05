using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.Infrastructure.Administration;

public sealed class SqlUserAdministration(PortalMercadoDbContext db, IPasswordHasher<Usuario> hasher, TimeProvider clock) : IUserAdministration
{
    public async Task<AccessSnapshot> AccessAsync(long actor, CancellationToken ct)
    {
        var user = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.IdUsuario == actor && x.Activo, ct);
        if (user is null) throw new AccessFailure(401, "La sesión ya no está disponible.");
        var profile = await AuthPersistence.ProfileAsync(db, user, ct);
        var permissions = await (from ur in db.Set<UsuarioRol>().AsNoTracking()
            join rp in db.Set<RolPermiso>() on ur.IdRol equals rp.IdRol
            join p in db.Set<Permiso>() on rp.IdPermiso equals p.IdPermiso
            where ur.IdUsuario == actor select p.Codigo).Distinct().ToArrayAsync(ct);
        var businesses = profile.Negocios;
        if (profile.Roles.Contains("ADMIN_MERCADO", StringComparer.OrdinalIgnoreCase))
            // El rol administrativo no hereda alcance fuera de sus mercados por una asociación directa accidental.
            businesses = await db.Negocios.AsNoTracking().Where(x => x.Estado == "ACTIVO" && profile.Mercados.Contains(x.IdMercado))
                .Select(x => x.IdNegocio).ToArrayAsync(ct);
        return new AccessSnapshot(actor, true, profile.Roles, permissions, profile.Mercados, businesses);
    }
    // La unión incluye también asociaciones inactivas; no permite modificar cuentas compartidas con ámbitos ajenos.
    private async Task<UserDto> DtoAsync(Usuario user, CancellationToken ct) => new(user.IdUsuario, user.Correo, user.Nombre, user.Telefono, user.Activo,
        await (from ur in db.Set<UsuarioRol>() join r in db.Set<Rol>() on ur.IdRol equals r.IdRol where ur.IdUsuario == user.IdUsuario select r.Codigo).ToArrayAsync(ct),
        await db.Set<UsuarioNegocio>().Where(x => x.IdUsuario == user.IdUsuario && x.Activo).Select(x => x.IdNegocio).ToArrayAsync(ct),
        await db.Set<UsuarioMercado>().Where(x => x.IdUsuario == user.IdUsuario).Select(x => x.IdMercado).ToArrayAsync(ct),
        await db.Set<UsuarioMfa>().AnyAsync(x => x.IdUsuario == user.IdUsuario && x.PendienteBootstrap && x.ActivadoEn == null, ct));
    private IQueryable<Usuario> Visible(AccessSnapshot access)
    {
        var q = db.Usuarios.AsNoTracking();
        if (access.General) return q;
        // Identidad global: solo cuentas cuyo conjunto completo de asociaciones pertenece al actor.
        return q.Where(u => u.IdUsuario != access.UserId &&
            !db.Set<UsuarioRol>().Any(ur => ur.IdUsuario == u.IdUsuario && db.Set<Rol>().Any(r => r.IdRol == ur.IdRol && r.Codigo.StartsWith("ADMIN_"))) &&
            !db.Set<UsuarioNegocio>().Any(un => un.IdUsuario == u.IdUsuario && !access.Businesses.Contains(un.IdNegocio)) &&
            !db.Set<UsuarioMercado>().Any(um => um.IdUsuario == u.IdUsuario && (!access.MarketAdmin || !access.Markets.Contains(um.IdMercado))) &&
            ((access.MarketAdmin && db.Set<UsuarioMercado>().Any(um => um.IdUsuario == u.IdUsuario && access.Markets.Contains(um.IdMercado))) ||
             ((access.MarketAdmin || access.Owner) && db.Set<UsuarioNegocio>().Any(un => un.IdUsuario == u.IdUsuario && un.Activo && access.Businesses.Contains(un.IdNegocio)))));
    }
    public async Task<Page<UserDto>> ListAsync(long actor, int page, int size, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct); access.Demand(Permissions.View);
        if (page < 1 || page > 100000 || size < 1 || size > 100) throw Bad();
        var q = Visible(access); var total = await q.CountAsync(ct);
        var users = await q.OrderBy(x => x.IdUsuario).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var items = new List<UserDto>(); foreach (var u in users) items.Add(await DtoAsync(u, ct));
        return new Page<UserDto>(items.ToArray(), total, page, size);
    }
    public async Task<UserDto> GetAsync(long actor, long id, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct); access.Demand(Permissions.View);
        var user = await Visible(access).SingleOrDefaultAsync(x => x.IdUsuario == id, ct);
        if (user is null) throw Denied();
        return await DtoAsync(user, ct);
    }
    public async Task<AdminCatalog> CatalogAsync(long actor, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct);
        if (!new[] { Permissions.View, Permissions.Create, Permissions.Roles, Permissions.Manage, Permissions.Businesses, Permissions.Markets, Permissions.ViewRoles, Permissions.ViewPermissions }.Any(access.Has)) throw Denied();
        var roleRows = await db.Set<Rol>().AsNoTracking().OrderBy(x => x.Codigo).ToArrayAsync(ct);
        var roles = new List<RoleDto>();
        foreach (var r in roleRows.Where(r => access.CanAssign(r.Codigo)))
            roles.Add(new RoleDto(r.IdRol, r.Codigo, r.Nombre, access.Has(Permissions.ViewPermissions) || access.Has(Permissions.Manage)
                ? await db.Set<RolPermiso>().Where(x => x.IdRol == r.IdRol).Select(x => x.IdPermiso).ToArrayAsync(ct) : []));
        var permissions = access.Has(Permissions.ViewPermissions) || access.Has(Permissions.Manage)
            ? await db.Set<Permiso>().OrderBy(x => x.Codigo).Select(x => new PermissionDto(x.IdPermiso, x.Codigo, x.Descripcion)).ToArrayAsync(ct) : [];
        var markets = await db.Mercados.Where(x => x.Activo && (access.General || (access.MarketAdmin && access.Markets.Contains(x.IdMercado))))
            .OrderBy(x => x.Nombre).Select(x => new NamedId(x.IdMercado, x.Nombre)).ToArrayAsync(ct);
        var businesses = await db.Negocios.Where(x => x.Estado == "ACTIVO" && db.Mercados.Any(m => m.IdMercado == x.IdMercado && m.Activo) &&
            (access.General || access.Businesses.Contains(x.IdNegocio))).OrderBy(x => x.NombreComercial).Select(x => new NamedId(x.IdNegocio, x.NombreComercial)).ToArrayAsync(ct);
        return new AdminCatalog(roles.ToArray(), permissions, businesses, markets);
    }
    private async Task ValidateScopesAsync(AccessSnapshot access, long[] businesses, long[] markets, CancellationToken ct)
    {
        if (businesses.Length > 200 || markets.Length > 200 || businesses.Any(x => x < 1) || markets.Any(x => x < 1)) throw Bad();
        if (!access.General && (businesses.Any(x => !access.Businesses.Contains(x)) || markets.Any(x => !access.MarketAdmin || !access.Markets.Contains(x)))) throw Denied();
        if (await db.Negocios.CountAsync(x => businesses.Contains(x.IdNegocio) && x.Estado == "ACTIVO" && db.Mercados.Any(m => m.IdMercado == x.IdMercado && m.Activo), ct) != businesses.Distinct().Count() ||
            await db.Mercados.CountAsync(x => markets.Contains(x.IdMercado) && x.Activo, ct) != markets.Distinct().Count()) throw Denied();
    }
    private async Task MutateAsync(long actor, long? target, string permission, string action, string correlation, Func<AccessSnapshot, Task> operation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Todas las mutaciones de administración comparten este orden. Los logins/MFA bloquean solo Usuario.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int, @resource nvarchar(255)=N'PortalMercado.SecurityAdministration'; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50020,'Administración ocupada.',1;", ct);
        var access = await AccessAsync(actor, ct); access.Demand(permission);
        if (target.HasValue)
        {
            var user = await db.Usuarios.FromSqlInterpolated($"SELECT * FROM dbo.Usuario WITH (UPDLOCK, ROWLOCK) WHERE IdUsuario={target.Value}").SingleOrDefaultAsync(ct);
            if (user is null) throw Denied();
            var dto = await DtoAsync(user, ct);
            var allBusinesses = await db.Set<UsuarioNegocio>().Where(x => x.IdUsuario == user.IdUsuario).Select(x => x.IdNegocio).ToArrayAsync(ct);
            if (!access.CanManage(user.IdUsuario, dto.Roles, dto.Mercados, allBusinesses) ||
                (!access.General && dto.Mercados.Length == 0 && dto.Negocios.Length == 0)) throw Denied();
            // Proteger la continuidad del administrador general, aun ante cambios de permisos personalizados.
            if (dto.Roles.Contains("ADMIN_GENERAL", StringComparer.OrdinalIgnoreCase) && user.Activo &&
                await (from u in db.Usuarios join ur in db.Set<UsuarioRol>() on u.IdUsuario equals ur.IdUsuario
                    join r in db.Set<Rol>() on ur.IdRol equals r.IdRol where u.Activo && r.Codigo == "ADMIN_GENERAL" select u.IdUsuario).Distinct().CountAsync(ct) <= 1)
                throw new AccessFailure(409, "No se puede modificar al último administrador general activo.");
        }
        await operation(access);
        if (target.HasValue)
        {
            var state = await db.Set<UsuarioSeguridad>().FindAsync([target.Value], ct);
            if (state is not null) state.Sello = Guid.NewGuid();
        }
        AuthPersistence.Audit(db, actor, action, correlation, clock.GetUtcNow().UtcDateTime).IdObjetivo = target;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new AccessFailure(409, "Ya existe una cuenta o asignación con esos datos."); }
    }
    public async Task<UserDto> CreateAsync(long actor, CreateUserRequest request, string correlation, CancellationToken ct)
    {
        if (CredentialPolicy.NormalizeEmail(request.Correo) is null || !CredentialPolicy.IsValidPassword(request.Password) ||
            string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 150 || request.Telefono?.Length > 30 || request.Negocios is null || request.Mercados is null) throw Bad();
        Usuario? created = null;
        await MutateAsync(actor, null, Permissions.Create, "ADMIN_USUARIO_CREADO", correlation, async access =>
        {
            access.Demand(Permissions.Roles);
            if (request.Negocios.Length > 0) access.Demand(Permissions.Businesses);
            if (request.Mercados.Length > 0) access.Demand(Permissions.Markets);
            if (!access.CanAssign(request.Rol)) throw Denied();
            var role = await db.Set<Rol>().SingleOrDefaultAsync(x => x.Codigo == request.Rol, ct);
            if (role is null) throw Bad();
            await ValidateScopesAsync(access, request.Negocios, request.Mercados, ct);
            if (!access.General && request.Negocios.Length + request.Mercados.Length == 0) throw Denied();
            var normalized = CredentialPolicy.NormalizeEmail(request.Correo)!;
            if (await db.Usuarios.AnyAsync(x => EF.Property<string>(x, "CorreoNormalizado") == normalized, ct)) throw new AccessFailure(409, "Ya existe una cuenta con esos datos.");
            var admin = AuthenticationRules.RequiresSecondFactor(false, [role.Codigo]);
            created = new Usuario { Correo = request.Correo.Trim(), Nombre = request.Nombre.Trim(), Telefono = request.Telefono?.Trim(), Activo = !admin, CreadoEn = clock.GetUtcNow().UtcDateTime };
            created.PasswordHash = hasher.HashPassword(created, request.Password); db.Add(created);
            await db.SaveChangesAsync(ct);
            db.Add(new UsuarioSeguridad { IdUsuario = created.IdUsuario, Sello = Guid.NewGuid() });
            db.Add(new UsuarioRol { IdUsuario = created.IdUsuario, IdRol = role.IdRol });
            foreach (var id in request.Negocios.Distinct()) db.Add(new UsuarioNegocio { IdUsuario = created.IdUsuario, IdNegocio = id, Activo = true });
            foreach (var id in request.Mercados.Distinct()) db.Add(new UsuarioMercado { IdUsuario = created.IdUsuario, IdMercado = id });
            if (admin) db.Add(new UsuarioMfa { IdUsuario = created.IdUsuario, PendienteBootstrap = true });
            AuthPersistence.Audit(db, actor, "ADMIN_ALTA_DESTINO", correlation, clock.GetUtcNow().UtcDateTime).IdObjetivo = created.IdUsuario;
        }, ct);
        return await DtoAsync(created!, ct);
    }
    public Task EditAsync(long actor, long id, EditUserRequest request, string correlation, CancellationToken ct) =>
        MutateAsync(actor, id, Permissions.Edit, "ADMIN_USUARIO_EDITADO", correlation, async _ =>
        {
            if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 150 || request.Telefono?.Length > 30) throw Bad();
            var user = await db.Usuarios.FindAsync([id], ct); user!.Nombre = request.Nombre.Trim(); user.Telefono = request.Telefono?.Trim();
        }, ct);
    public Task StateAsync(long actor, long id, bool active, string correlation, CancellationToken ct) =>
        MutateAsync(actor, id, Permissions.State, "ADMIN_ESTADO_CAMBIADO", correlation, async _ =>
        {
            var user = (await db.Usuarios.FindAsync([id], ct))!;
            var roles = await (from ur in db.Set<UsuarioRol>() join r in db.Set<Rol>() on ur.IdRol equals r.IdRol where ur.IdUsuario == id select r.Codigo).ToArrayAsync(ct);
            if (active && AuthenticationRules.RequiresSecondFactor(false, roles) && (!user.DosFactoresHabilitado ||
                !await db.Set<UsuarioMfa>().AnyAsync(x => x.IdUsuario == id && x.ActivadoEn != null && !x.PendienteBootstrap, ct)))
                throw new AccessFailure(409, "La cuenta administrativa debe completar el enrolamiento MFA.");
            if (!active)
            {
                var mfa = await db.Set<UsuarioMfa>().FindAsync([id], ct);
                if (mfa is { PendienteBootstrap: true }) mfa.PendienteBootstrap = false; // No permitir enrolar una cuenta desactivada por un administrador.
            }
            user.Activo = active;
        }, ct);
    public Task RolesAsync(long actor, long id, int[] roles, string correlation, CancellationToken ct) =>
        MutateAsync(actor, id, Permissions.Roles, "ADMIN_ROLES_CAMBIADOS", correlation, async access =>
        {
            if (roles is null || roles.Length > 20) throw Bad();
            var rows = await db.Set<Rol>().Where(x => roles.Contains(x.IdRol)).ToArrayAsync(ct);
            if (rows.Length != roles.Distinct().Count() || rows.Any(x => !access.CanAssign(x.Codigo))) throw Denied();
            var user = (await db.Usuarios.FindAsync([id], ct))!;
            if (AuthenticationRules.RequiresSecondFactor(false, rows.Select(x => x.Codigo)) && !user.DosFactoresHabilitado)
            {
                user.Activo = false;
                var mfa = await db.Set<UsuarioMfa>().FindAsync([id], ct);
                if (mfa is null) db.Add(new UsuarioMfa { IdUsuario = id, PendienteBootstrap = true });
                else if (mfa.ActivadoEn is null) mfa.PendienteBootstrap = true;
            }
            await db.Set<UsuarioRol>().Where(x => x.IdUsuario == id).ExecuteDeleteAsync(ct);
            foreach (var role in roles.Distinct()) db.Add(new UsuarioRol { IdUsuario = id, IdRol = role });
        }, ct);
    public Task BusinessesAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) =>
        MutateAsync(actor, id, Permissions.Businesses, "ADMIN_NEGOCIOS_CAMBIADOS", correlation, async access =>
        {
            if (ids is null) throw Bad(); await ValidateScopesAsync(access, ids, [], ct);
            if (!access.General && ids.Length == 0) throw Denied();
            // Conservar las filas para trazabilidad: reactivar/desactivar en lugar de eliminarlas.
            var rows = await db.Set<UsuarioNegocio>().Where(x => x.IdUsuario == id).ToArrayAsync(ct);
            foreach (var row in rows) row.Activo = ids.Contains(row.IdNegocio);
            foreach (var business in ids.Distinct().Where(n => rows.All(x => x.IdNegocio != n))) db.Add(new UsuarioNegocio { IdUsuario = id, IdNegocio = business, Activo = true });
        }, ct);
    public Task MarketsAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) =>
        MutateAsync(actor, id, Permissions.Markets, "ADMIN_MERCADOS_CAMBIADOS", correlation, async access =>
        {
            if (ids is null) throw Bad(); await ValidateScopesAsync(access, [], ids, ct);
            if (!access.General && ids.Length == 0) throw Denied();
            await db.Set<UsuarioMercado>().Where(x => x.IdUsuario == id).ExecuteDeleteAsync(ct);
            foreach (var market in ids.Distinct()) db.Add(new UsuarioMercado { IdUsuario = id, IdMercado = market });
        }, ct);
    public Task PermissionsAsync(long actor, int role, int[] ids, string correlation, CancellationToken ct) =>
        MutateAsync(actor, null, Permissions.Manage, $"ADMIN_PERMISOS_ROL_{role}", correlation, async access =>
        {
            if (!access.General) throw Denied();
            if (ids is null || ids.Length > 100) throw Bad();
            var row = await db.Set<Rol>().FindAsync([role], ct);
            if (row is null || access.Roles.Contains(row.Codigo, StringComparer.OrdinalIgnoreCase)) throw Denied(); // Sin autoelevación ni modificación del rol propio.
            var permissions = await db.Set<Permiso>().Where(x => ids.Contains(x.IdPermiso)).ToArrayAsync(ct);
            if (permissions.Length != ids.Distinct().Count() || permissions.Any(p => !access.Has(p.Codigo))) throw Denied();
            await db.Set<RolPermiso>().Where(x => x.IdRol == role).ExecuteDeleteAsync(ct);
            foreach (var permission in ids.Distinct()) db.Add(new RolPermiso { IdRol = role, IdPermiso = permission });
            var affected = await db.Set<UsuarioRol>().Where(x => x.IdRol == role).Select(x => x.IdUsuario).ToArrayAsync(ct);
            await db.Set<UsuarioSeguridad>().Where(x => affected.Contains(x.IdUsuario)).ExecuteUpdateAsync(x => x.SetProperty(y => y.Sello, Guid.NewGuid()), ct);
        }, ct);
    public async Task<BusinessContext> ContextAsync(long actor, string credential, string host, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct); access.Demand(Permissions.Context);
        var session = await SessionAsync(actor, credential, host, ct);
        var available = await db.Negocios.Where(n => n.Estado == "ACTIVO" && db.Mercados.Any(m => m.IdMercado == n.IdMercado && m.Activo) &&
            (access.General || access.Businesses.Contains(n.IdNegocio))).OrderBy(n => n.NombreComercial).Select(n => new NamedId(n.IdNegocio, n.NombreComercial)).ToArrayAsync(ct);
        return new BusinessContext(available, available.Any(x => x.Id == session.IdNegocioActivo) ? session.IdNegocioActivo : null);
    }
    public async Task SelectContextAsync(long actor, string credential, string host, long business, string correlation, CancellationToken ct)
    {
        await MutateAsync(actor, null, Permissions.Context, "ADMIN_CONTEXTO_CAMBIADO", correlation, async access =>
        {
            await ValidateScopesAsync(access, [business], [], ct);
            var session = await SessionAsync(actor, credential, host, ct); session.IdNegocioActivo = business;
        }, ct);
    }
    private async Task<UsuarioSesion> SessionAsync(long actor, string credential, string host, CancellationToken ct)
    {
        AuthPersistence.CheckHost(host);
        if (!MfaCryptography.IsCredential(credential)) throw new AccessFailure(401, "Sesión no disponible.");
        var hash = MfaCryptography.Hash(credential); var now = clock.GetUtcNow().UtcDateTime;
        return await db.Set<UsuarioSesion>().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Host == host && x.IdUsuario == actor && x.RevocadoEn == null && x.ExpiraEn > now &&
            db.Set<UsuarioSeguridad>().Any(s => s.IdUsuario == actor && s.Sello == x.Sello && (s.BloqueadoHasta == null || s.BloqueadoHasta <= now)), ct)
            ?? throw new AccessFailure(401, "Sesión no disponible.");
    }
    private static AccessFailure Bad() => new(400, "Revisa los campos y las cantidades permitidas.");
    private static AccessFailure Denied() => new(403, "El usuario o recurso no está autorizado en tu ámbito.");
}

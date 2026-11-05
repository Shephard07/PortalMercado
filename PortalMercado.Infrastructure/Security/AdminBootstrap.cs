using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Security;

public sealed class AdminBootstrap(PortalMercadoDbContext db, IPasswordHasher<Usuario> hasher,
    TimeProvider clock, IHostEnvironment environment) : IAdminBootstrap
{
    public async Task CreateLocalTestUserAsync(string correo, string nombre, string password, CancellationToken ct)
    {
        var connection = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var sources = new[] { @".\SQLEXPRESS", @"(local)\SQLEXPRESS", @"localhost\SQLEXPRESS",
            Environment.MachineName + @"\SQLEXPRESS" };
        if (!environment.IsDevelopment() || connection.InitialCatalog != "PortalMercado_ET01_Test" ||
            !sources.Contains(connection.DataSource, StringComparer.OrdinalIgnoreCase) || !connection.IntegratedSecurity)
            throw new BootstrapFailureException("ENTORNO_NO_PERMITIDO", "El usuario ficticio solo se aprovisiona en SQLEXPRESS local con Windows y Development.");
        BootstrapValidation.Validate(correo, nombre, password, ficticio: true);
        var normalized = CredentialPolicy.NormalizeEmail(correo)!;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-001", ct))
            throw new BootstrapFailureException("FALTA_MIGRACION", "Falta la ampliación ET02-001.");
        if (await db.Usuarios.AnyAsync(x => EF.Property<string>(x, "CorreoNormalizado") == normalized, ct))
            throw new BootstrapFailureException("USUARIO_FICTICIO_EXISTENTE", "El usuario ficticio ya existe; no se sobrescribe.");
        var role = await db.Set<Rol>().SingleOrDefaultAsync(x => x.Codigo == "VENDEDOR", ct);
        if (role is null)
        {
            role = new Rol { Codigo = "VENDEDOR", Nombre = "Vendedor" };
            db.Add(role); await db.SaveChangesAsync(ct);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new Usuario { Correo = correo.Trim(), Nombre = nombre.Trim(), Activo = true, CreadoEn = now };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Add(user); await db.SaveChangesAsync(ct);
        db.Add(new UsuarioSeguridad { IdUsuario = user.IdUsuario, Sello = Guid.NewGuid() });
        db.Add(new UsuarioRol { IdUsuario = user.IdUsuario, IdRol = role.IdRol });
        db.Add(new AuditoriaAcceso { IdActor = user.IdUsuario, Evento = "USUARIO_FICTICIO_APROVISIONADO",
            FechaUtc = now, CorrelationId = Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task CreateAsync(string correo, string nombre, string password, CancellationToken ct)
    {
        BootstrapValidation.Validate(correo, nombre, password, ficticio: false);
        var normalized = CredentialPolicy.NormalizeEmail(correo)!;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // No modificar SP financieros. El bloqueo usa una variable, como exige la línea base.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @resource nvarchar(255)=N'PortalMercado.ET02.AdminBootstrap', @result int;
            EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
            IF @result<0 THROW 50220,'Bootstrap ocupado.',1;
            """, ct);
        if (!await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-001", ct))
            throw new BootstrapFailureException("FALTA_MIGRACION", "Falta aplicar la ampliación de seguridad aprobada.");
        if (!await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-002", ct))
            throw new BootstrapFailureException("FALTA_MIGRACION_MFA", "Falta la ampliación MFA ET02-002.");
        if (await db.Set<SeguridadVersion>().AnyAsync(x => x.Codigo == "ET02-BOOTSTRAP", ct) ||
            await (from ur in db.Set<UsuarioRol>() join r in db.Set<Rol>() on ur.IdRol equals r.IdRol
                   where r.Codigo == "ADMIN_GENERAL" select ur).AnyAsync(ct))
            throw new BootstrapFailureException("ADMIN_EXISTENTE", "El primer administrador ya está aprovisionado.");
        if (await db.Usuarios.AnyAsync(x => EF.Property<string>(x, "CorreoNormalizado") == normalized, ct))
            throw new BootstrapFailureException("CORREO_EXISTENTE", "No se puede aprovisionar la cuenta indicada.");
        var now = clock.GetUtcNow().UtcDateTime;
        var role = await db.Set<Rol>().SingleOrDefaultAsync(x => x.Codigo == "ADMIN_GENERAL", ct);
        if (role is null)
        {
            role = new Rol { Codigo = "ADMIN_GENERAL", Nombre = "Administrador general" };
            db.Add(role); await db.SaveChangesAsync(ct);
        }
        var user = new Usuario { Correo = correo.Trim(), Nombre = nombre.Trim(), Activo = false,
            CreadoEn = now, EmailConfirmado = false, DosFactoresHabilitado = false };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Add(user); await db.SaveChangesAsync(ct);
        db.Add(new UsuarioSeguridad { IdUsuario = user.IdUsuario, Sello = Guid.NewGuid() });
        db.Add(new UsuarioRol { IdUsuario = user.IdUsuario, IdRol = role.IdRol });
        db.Add(new UsuarioMfa { IdUsuario = user.IdUsuario, PendienteBootstrap = true });
        db.Add(new SeguridadVersion { Codigo = "ET02-BOOTSTRAP", AplicadoEn = now });
        db.Add(new AuditoriaAcceso { IdActor = user.IdUsuario, Evento = "ADMIN_APROVISIONADO_INACTIVO",
            FechaUtc = now, CorrelationId = Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace PortalMercado.Application.Administration;

public static class Permissions
{
    public const string View = "USUARIO_VER", Create = "USUARIO_CREAR", Edit = "USUARIO_EDITAR",
        State = "USUARIO_DESACTIVAR", Roles = "ROL_ASIGNAR", Manage = "PERMISO_GESTIONAR",
        Businesses = "NEGOCIO_USUARIO_ASIGNAR", Markets = "MERCADO_USUARIO_ASIGNAR",
        ViewRoles = "ROL_VER", ViewPermissions = "PERMISO_VER", Context = "NEGOCIO_CONTEXTO";
    public static readonly string[] All = [View, Create, Edit, State, Roles, Manage, Businesses, Markets, ViewRoles, ViewPermissions, Context];
}
public sealed class AccessFailure(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed record AccessSnapshot(long UserId, bool Active, string[] Roles, string[] Permissions, long[] Markets, long[] Businesses)
{
    public bool General => Roles.Contains("ADMIN_GENERAL", StringComparer.OrdinalIgnoreCase);
    public bool MarketAdmin => Roles.Contains("ADMIN_MERCADO", StringComparer.OrdinalIgnoreCase);
    public bool Owner => Roles.Contains("PROPIETARIO_NEGOCIO", StringComparer.OrdinalIgnoreCase);
    public bool Has(string permission) => Active && Permissions.Contains(permission, StringComparer.Ordinal);
    public void Demand(string permission) { if (!Has(permission)) throw new AccessFailure(403, "No tienes autorización para esta operación."); }
    // Se exige contener TODOS los ámbitos del destinatario antes de modificar su identidad global.
    public bool CanManage(long target, string[] roles, long[] markets, long[] businesses) => Active && target != UserId &&
        (General || (!roles.Any(r => r.StartsWith("ADMIN_", StringComparison.OrdinalIgnoreCase)) &&
        ((MarketAdmin && markets.All(Markets.Contains) && businesses.All(Businesses.Contains) && markets.Length + businesses.Length > 0) ||
        (Owner && markets.Length == 0 && businesses.Length > 0 && businesses.All(Businesses.Contains)))));
    public bool CanAssign(string role) => Active && (General ||
        (MarketAdmin && new[] { "PROPIETARIO_NEGOCIO", "VENDEDOR" }.Contains(role, StringComparer.OrdinalIgnoreCase)) ||
        (Owner && string.Equals(role, "VENDEDOR", StringComparison.OrdinalIgnoreCase)));
}
public sealed record NamedId(long Id, string Nombre);
public sealed record RoleDto(int IdRol, string Codigo, string Nombre, int[] Permisos);
public sealed record PermissionDto(int IdPermiso, string Codigo, string? Descripcion);
public sealed record UserDto(long IdUsuario, string Correo, string Nombre, string? Telefono, bool Activo,
    string[] Roles, long[] Negocios, long[] Mercados, bool PendienteMfa);
public sealed record Page<T>(T[] Items, int Total, int Pagina, int Tamano);
public sealed record AdminCatalog(RoleDto[] Roles, PermissionDto[] Permisos, NamedId[] Negocios, NamedId[] Mercados);
public sealed class CreateUserRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = "";
    [Required, StringLength(150)] public string Nombre { get; set; } = "";
    [StringLength(30)] public string? Telefono { get; set; }
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    public string Rol { get; set; } = "VENDEDOR";
    public long[] Negocios { get; set; } = [];
    public long[] Mercados { get; set; } = [];
}
public sealed class EditUserRequest
{
    [Required, StringLength(150)] public string Nombre { get; set; } = "";
    [StringLength(30)] public string? Telefono { get; set; }
}
public sealed class StateRequest { [JsonRequired] public bool Activo { get; set; } }
public sealed class RoleAssignment { [JsonRequired, Required, MaxLength(20)] public int[] Ids { get; set; } = []; }
public sealed class ScopeAssignment { [JsonRequired, Required, MaxLength(200)] public long[] Ids { get; set; } = []; }
public sealed class PermissionAssignment { [JsonRequired, Required, MaxLength(100)] public int[] Ids { get; set; } = []; }
public sealed class ContextRequest { [Range(1, long.MaxValue)] public long IdNegocio { get; set; } }
public sealed record BusinessContext(NamedId[] Disponibles, long? IdNegocioActivo);
public interface IUserAdministration
{
    Task<AccessSnapshot> AccessAsync(long actor, CancellationToken ct);
    Task<Page<UserDto>> ListAsync(long actor, int page, int size, CancellationToken ct);
    Task<UserDto> GetAsync(long actor, long id, CancellationToken ct);
    Task<AdminCatalog> CatalogAsync(long actor, CancellationToken ct);
    Task<UserDto> CreateAsync(long actor, CreateUserRequest request, string correlation, CancellationToken ct);
    Task EditAsync(long actor, long id, EditUserRequest request, string correlation, CancellationToken ct);
    Task StateAsync(long actor, long id, bool active, string correlation, CancellationToken ct);
    Task RolesAsync(long actor, long id, int[] roles, string correlation, CancellationToken ct);
    Task BusinessesAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct);
    Task MarketsAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct);
    Task PermissionsAsync(long actor, int role, int[] ids, string correlation, CancellationToken ct);
    Task<BusinessContext> ContextAsync(long actor, string credential, string host, CancellationToken ct);
    Task SelectContextAsync(long actor, string credential, string host, long business, string correlation, CancellationToken ct);
}

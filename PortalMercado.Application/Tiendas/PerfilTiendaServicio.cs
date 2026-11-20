using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;

namespace PortalMercado.Application.Tiendas;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PerfilTiendaDatos
{
    public Guid Version { get; set; }
    [StringLength(4000)] public string? Descripcion { get; set; }
    [StringLength(500)] public string? LogoUrl { get; set; }
    [StringLength(500)] public string? Horario { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(254), EmailAddress] public string? Correo { get; set; }
}

public sealed record PerfilTienda(long IdNegocio, string NombreComercial, string Slug,
    bool TelefonoPublico, bool CorreoPublico, PerfilTiendaDatos Datos, AuditoriaPerfilTienda? UltimoCambio);
public sealed record AuditoriaPerfilTienda(long Actor, long IdNegocio, DateTimeOffset FechaUtc, string Correlacion);

public interface IRepositorioPerfilTienda
{
    Task<PerfilTienda?> ObtenerAsync(long idNegocio, CancellationToken ct);
    // Comparar versión, actualizar exclusivamente campos permitidos y auditar en una transacción.
    Task GuardarAsync(long idNegocio, PerfilTiendaDatos datos, AuditoriaPerfilTienda auditoria, CancellationToken ct);
}

public sealed class PerfilTiendaServicio(IUserAdministration usuarios, IRepositorioPerfilTienda perfiles,
    TimeProvider? reloj = null)
{
    // Provisional: reutilizar permiso de gestión hasta recibir catálogo de permisos del DBA.
    public const string Permiso = ProductoServicio.PermisoGestionar;

    public async Task<BusinessContext> NegociosAsync(long actor, string credencial, string host, CancellationToken ct)
    {
        (await usuarios.AccessAsync(actor, ct)).Demand(Permiso);
        return await usuarios.ContextAsync(actor, credencial, host, ct);
    }

    public async Task<PerfilTienda> ObtenerAsync(long actor, string credencial, string host, long idNegocio, CancellationToken ct)
    {
        await ExigirAsync(actor, credencial, host, idNegocio, ct);
        return await perfiles.ObtenerAsync(idNegocio, ct)
            ?? throw new AccessFailure(404, "No se encontró un perfil de tienda para ese negocio.");
    }

    public async Task GuardarAsync(long actor, string credencial, string host, long idNegocio,
        PerfilTiendaDatos datos, string correlacion, CancellationToken ct)
    {
        await ExigirAsync(actor, credencial, host, idNegocio, ct);
        var limpio = new PerfilTiendaDatos
        {
            Version = datos.Version, Descripcion = Limpiar(datos.Descripcion), LogoUrl = Limpiar(datos.LogoUrl),
            Horario = Limpiar(datos.Horario), Telefono = Limpiar(datos.Telefono), Correo = Limpiar(datos.Correo)
        };
        if (limpio.Version == Guid.Empty || !Validator.TryValidateObject(limpio, new ValidationContext(limpio), [], true))
            throw new AccessFailure(400, "Revisa los datos del perfil y vuelve a cargarlo si falta su versión.");
        if (limpio.LogoUrl is not null && TiendaServicio.ImagenSegura(limpio.LogoUrl) is null)
            throw new AccessFailure(400, "El logo debe usar una URL HTTPS válida sin credenciales.");
        if (new[] { limpio.Horario, limpio.Telefono, limpio.Correo, limpio.LogoUrl }
            .Any(s => s?.Any(char.IsControl) == true))
            throw new AccessFailure(400, "Los datos de contacto no pueden contener saltos ni caracteres de control.");
        var auditoria = new AuditoriaPerfilTienda(actor, idNegocio,
            (reloj ?? TimeProvider.System).GetUtcNow(), correlacion);
        await perfiles.GuardarAsync(idNegocio, limpio, auditoria, ct);
    }

    private async Task ExigirAsync(long actor, string credencial, string host, long idNegocio, CancellationToken ct)
    {
        var contexto = await NegociosAsync(actor, credencial, host, ct);
        if (idNegocio <= 0 || !contexto.Disponibles.Any(n => n.Id == idNegocio))
            throw new AccessFailure(403, "No tienes acceso al perfil de ese negocio.");
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

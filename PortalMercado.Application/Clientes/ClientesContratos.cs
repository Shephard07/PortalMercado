using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PortalMercado.Application.Clientes;

// No acepta IdUsuario, IdCliente, rol, estado ni confirmación de correo desde el navegador.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegistroClienteSolicitud
{
    [Required, StringLength(150)] public string Nombre { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmarPassword { get; set; } = "";
    [StringLength(30)] public string? Telefono { get; set; }
    public bool AceptaTerminos { get; set; }
    public bool AceptaPrivacidad { get; set; }
    public bool AceptaPromociones { get; set; } // Nunca preseleccionar en la vista.
    [Required, StringLength(40)] public string VersionTerminos { get; set; } = "";
    [Required, StringLength(40)] public string VersionPrivacidad { get; set; } = "";
    [StringLength(40)] public string? VersionPromociones { get; set; }
    public override string ToString() => nameof(RegistroClienteSolicitud);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ClientePerfilDatos
{
    [StringLength(150)] public string? Apellidos { get; set; }
    [StringLength(30)] public string? Celular { get; set; }
    [StringLength(4)] public string? TipoDocumento { get; set; }
    [StringLength(20)] public string? NumeroDocumento { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ClienteDireccionDatos
{
    [StringLength(80)] public string? Etiqueta { get; set; }
    [Required, StringLength(300)] public string Direccion { get; set; } = "";
    [StringLength(250)] public string? Referencia { get; set; }
    [StringLength(120)] public string? Distrito { get; set; }
    [StringLength(120)] public string? Provincia { get; set; }
    [StringLength(120)] public string? Departamento { get; set; }
    [Range(typeof(decimal), "-90", "90")] public decimal? Latitud { get; set; }
    [Range(typeof(decimal), "-180", "180")] public decimal? Longitud { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ClienteConsentimientoDatos
{
    [Required, StringLength(40)] public string Tipo { get; set; } = "";
    [Required, StringLength(40)] public string VersionDocumento { get; set; } = "";
    [JsonRequired] public bool Aceptado { get; set; }
}

// DTO privados: no usar en las respuestas del buscador o del portal público.
public sealed record ClientePerfil(long IdCliente, long IdUsuario, string Nombre, string Correo,
    ClientePerfilDatos Datos);
public sealed record ClienteDireccion(long IdDireccion, ClienteDireccionDatos Datos, bool Principal, bool Activa);
public sealed record ClienteConsentimiento(string Tipo, string VersionDocumento, bool Aceptado, DateTimeOffset RegistradoEn);
public sealed record RegistroClienteResultado(bool RequiereConfirmacionCorreo);

// Metadatos creados en el host, nunca vinculados desde el formulario o cuerpo JSON.
public sealed record ClienteOperacionContexto(string Correlacion, string? Ip = null);
public sealed record ClienteOperacion(long IdUsuario, DateTimeOffset FechaUtc, string Correlacion, string? Ip);

public interface IClienteService
{
    Task<ClientePerfil> ObtenerAsync(string credencial, string host, CancellationToken ct);
    Task ActualizarAsync(string credencial, string host, ClientePerfilDatos datos,
        ClienteOperacionContexto contexto, CancellationToken ct);
}
public interface IDireccionService
{
    Task<ClienteDireccion[]> ListarAsync(string credencial, string host, CancellationToken ct);
    Task<ClienteDireccion> CrearAsync(string credencial, string host, ClienteDireccionDatos datos, ClienteOperacionContexto contexto, CancellationToken ct);
    Task ActualizarAsync(string credencial, string host, long idDireccion, ClienteDireccionDatos datos, ClienteOperacionContexto contexto, CancellationToken ct);
    Task EliminarAsync(string credencial, string host, long idDireccion, ClienteOperacionContexto contexto, CancellationToken ct);
    Task MarcarPrincipalAsync(string credencial, string host, long idDireccion, ClienteOperacionContexto contexto, CancellationToken ct);
}
public interface IConsentimientoService
{
    Task<ClienteConsentimiento[]> ListarAsync(string credencial, string host, CancellationToken ct);
    Task RegistrarAsync(string credencial, string host, ClienteConsentimientoDatos datos, ClienteOperacionContexto contexto, CancellationToken ct);
}

public interface IRepositorioPerfilCliente
{
    // Todas las operaciones resuelven Cliente desde IdUsuario. Revalidar propiedad al escribir.
    Task<ClientePerfil?> ObtenerPorUsuarioAsync(long idUsuario, CancellationToken ct);
    // Actualización limitada a ClientePerfilDatos y auditoría en la misma transacción.
    Task ActualizarPorUsuarioAsync(long idUsuario, ClientePerfilDatos datos, ClienteOperacion operacion, CancellationToken ct);
}
public interface IRepositorioDireccionesCliente
{
    Task<ClienteDireccion[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct);
    Task<ClienteDireccion> CrearPorUsuarioAsync(long idUsuario, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct);
    Task ActualizarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct);
    Task EliminarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct);
    // Bloqueo/transacción por cliente: validar destino activo, desmarcar anterior y marcar destino.
    Task MarcarPrincipalPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct);
}
public interface IRepositorioConsentimientosCliente
{
    Task<ClienteConsentimiento[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct);
    // Conservar historial; no reemplazar silenciosamente aceptaciones anteriores.
    Task RegistrarPorUsuarioAsync(long idUsuario, ClienteConsentimientoDatos datos, ClienteOperacion operacion, CancellationToken ct);
}

using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;

namespace PortalMercado.Application.Clientes;

public sealed record DocumentosClienteVigentes(string Terminos, string Privacidad, string? Promociones = null)
{
    public void ExigirConfiguracion()
    {
        if (!VersionValida(Terminos) || !VersionValida(Privacidad) ||
            (Promociones is not null && !VersionValida(Promociones)))
            throw new AccessFailure(503, "El registro no está disponible en este momento.");
    }
    private static bool VersionValida(string? valor) => valor is { Length: >= 1 and <= 40 }
        && valor == valor.Trim() && ClientesValidacion.Ascii(valor) && !valor.Any(char.IsControl);
}
public sealed record ConsentimientoRegistro(string Tipo, string VersionDocumento, bool Aceptado);
public enum EstadoRegistroCliente { Creado, NoDisponible }
public sealed record RegistroClientePersistido(EstadoRegistroCliente Estado, bool RequiereConfirmacionCorreo = false);

// Comando interno, no es una respuesta HTTP ni un objeto para logs.
public sealed class RegistroClienteAlta
{
    public required string Nombre { get; init; }
    public required string Correo { get; init; }
    public required string CorreoNormalizado { get; init; }
    public string? Telefono { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public required string PasswordHash { get; init; }
    public required ConsentimientoRegistro[] Consentimientos { get; init; }
    public required ClienteOperacion Operacion { get; init; }
    public override string ToString() => nameof(RegistroClienteAlta);
}
public interface IHashPasswordCliente
{
    // Adaptar el IPasswordHasher<Usuario> existente. Nunca implementar otro algoritmo.
    string Crear(string nombre, string correo, string password);
}
public interface IRepositorioRegistroCliente
{
    // UNA transacción: Usuario + estado de seguridad requerido por el login + Cliente único
    // + consentimientos + auditoría. Debe controlar correo duplicado aun en concurrencia.
    // NoDisponible incluye duplicados sin revelar si existe una cuenta. No conceder rol admin.
    // Fallo en cualquier paso => rollback completo. Nunca adjuntar Cliente a una cuenta previa.
    Task<RegistroClientePersistido> RegistrarAsync(RegistroClienteAlta alta, CancellationToken ct);
}
public sealed class RegistroClienteServicio(IRepositorioRegistroCliente registros, IHashPasswordCliente passwords,
    DocumentosClienteVigentes documentos, TimeProvider? reloj = null)
{
    public const string RechazoGenerico = "No se pudo completar el registro. Revisa los datos o utiliza la recuperación de acceso.";

    public async Task<RegistroClienteResultado> RegistrarAsync(RegistroClienteSolicitud solicitud,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitud);
        documentos.ExigirConfiguracion();
        ClientesValidacion.ValidarModelo(solicitud);
        var nombre = ClientesValidacion.Texto(solicitud.Nombre);
        var correo = CredentialPolicy.NormalizeEmail(solicitud.Correo);
        var telefono = ClientesValidacion.Texto(solicitud.Telefono);
        if (nombre is null || correo is null || !CredentialPolicy.IsValidPassword(solicitud.Password) ||
            solicitud.Password != solicitud.ConfirmarPassword || !ClientesValidacion.Ascii(telefono))
            throw ClientesValidacion.Invalido("Revisa el nombre, correo, contacto y fortaleza de contraseña.");
        if (!solicitud.AceptaTerminos || !solicitud.AceptaPrivacidad ||
            solicitud.VersionTerminos != documentos.Terminos || solicitud.VersionPrivacidad != documentos.Privacidad)
            throw ClientesValidacion.Invalido("Revisa y acepta las versiones vigentes de términos y privacidad.");
        if ((solicitud.AceptaPromociones && documentos.Promociones is null) ||
            (solicitud.AceptaPromociones && solicitud.VersionPromociones != documentos.Promociones) ||
            (solicitud.VersionPromociones is not null && solicitud.VersionPromociones != documentos.Promociones))
            throw ClientesValidacion.Invalido("Revisa la versión del consentimiento promocional.");
        var consentimientos = new List<ConsentimientoRegistro>
        {
            new("TERMINOS", documentos.Terminos, true), new("PRIVACIDAD", documentos.Privacidad, true)
        };
        if (documentos.Promociones is not null)
            consentimientos.Add(new("PROMOCIONES", documentos.Promociones, solicitud.AceptaPromociones));
        var operacion = ClientesValidacion.Operacion(0, contexto, reloj ?? TimeProvider.System);
        var hash = passwords.Crear(nombre, solicitud.Correo.Trim(), solicitud.Password);
        if (string.IsNullOrWhiteSpace(hash) || hash == solicitud.Password)
            throw new InvalidOperationException("El proveedor de identidad no produjo un hash válido.");
        var alta = new RegistroClienteAlta
        {
            Nombre = nombre, Correo = solicitud.Correo.Trim(), CorreoNormalizado = correo,
            Telefono = telefono, PasswordHash = hash, Consentimientos = consentimientos.ToArray(), Operacion = operacion
        };
        var resultado = await registros.RegistrarAsync(alta, ct);
        if (resultado.Estado == EstadoRegistroCliente.NoDisponible)
            throw ClientesValidacion.Invalido(RechazoGenerico);
        if (resultado.Estado != EstadoRegistroCliente.Creado)
            throw new AccessFailure(503, "El registro no está disponible en este momento.");
        // No autenticar automáticamente ni fabricar confirmación; lo decide el proveedor aprobado.
        return new(resultado.RequiereConfirmacionCorreo);
    }
}

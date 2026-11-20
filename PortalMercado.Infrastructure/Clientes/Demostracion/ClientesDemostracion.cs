using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Security;
using System.Text.Json;

namespace PortalMercado.Infrastructure.Clientes.Demostracion;

public sealed record ClientesModo(bool Demostracion);
public sealed record EventoClienteDemostracion(long Actor, string Accion, DateTimeOffset FechaUtc, string Correlacion);

// Solo para Development con activación explícita. Todos los datos pertenecen a este proceso.
public sealed partial class ClientesDemostracion : IRepositorioRegistroCliente, IRepositorioPerfilCliente,
    IRepositorioDireccionesCliente, IRepositorioConsentimientosCliente
{
    private sealed class Cuenta
    {
        public required Usuario Usuario { get; init; }
        public required long ClienteId { get; init; }
        public ClientePerfilDatos Perfil { get; set; } = new();
        public Dictionary<long, ClienteDireccion> Direcciones { get; } = [];
        public List<ClienteConsentimiento> Consentimientos { get; } = [];
        public List<ClienteOperacion> Evidencias { get; } = [];
        public int Fallos { get; set; }
        public DateTimeOffset? BloqueadoHasta { get; set; }
    }
    private sealed record Sesion(long Usuario, string Host, DateTimeOffset Expira);
    private readonly object puerta = new();
    private readonly Dictionary<long, Cuenta> cuentas = [];
    private readonly Dictionary<string, long> correos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sesion> sesiones = new(StringComparer.Ordinal);
    private readonly Queue<EventoClienteDemostracion> eventos = [];
    private readonly TimeProvider reloj;
    private readonly string hashDesconocido;
    private long siguienteUsuario = 8_000_000_000_000;
    private long siguienteDireccion;

    public ClientesDemostracion(TimeProvider reloj, IOptions<PasswordHasherOptions> opciones)
    {
        this.reloj = reloj;
        hashDesconocido = new PasswordHasher<Usuario>(opciones).HashPassword(new Usuario(), MfaCryptography.NewCredential());
    }
    private static T Copia<T>(T dato) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(dato))!;
    private static AccessFailure NoDisponible() => new(404, "La dirección no está disponible.");
    private Cuenta Propia(long usuario) => cuentas.TryGetValue(usuario, out var cuenta) ? cuenta
        : throw new AccessFailure(403, "Tu cuenta no tiene un perfil de cliente disponible.");
    private ClienteDireccion DireccionPropia(Cuenta cuenta, long id) =>
        cuenta.Direcciones.TryGetValue(id, out var direccion) && direccion.Activa ? direccion : throw NoDisponible();
    private void RegistrarEvento(long actor, string accion, ClienteOperacion op)
    {
        eventos.Enqueue(new(actor, accion, op.FechaUtc, op.Correlacion));
        while (eventos.Count > 1000) eventos.Dequeue();
    }
    private static void Actor(long usuario, ClienteOperacion op)
    { if (op.IdUsuario != usuario) throw new AccessFailure(403, "Operación no autorizada."); }
    private static void Host(string host)
    { if (host is not ("API" or "MVC")) throw new ArgumentException("Host no válido.", nameof(host)); }

    public Task<RegistroClientePersistido> RegistrarAsync(RegistroClienteAlta alta, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!alta.CorreoNormalizado.EndsWith("@EXAMPLE.INVALID", StringComparison.Ordinal))
            throw new AccessFailure(400, "En demostración utiliza un correo ficticio terminado en @example.invalid.");
        lock (puerta)
        {
            ct.ThrowIfCancellationRequested();
            if (correos.ContainsKey(alta.CorreoNormalizado)) return Task.FromResult(new RegistroClientePersistido(EstadoRegistroCliente.NoDisponible));
            if (cuentas.Count >= 100) throw new AccessFailure(503, "La demostración alcanzó su límite. Reinicia la aplicación para comenzar de nuevo.");
            var id = ++siguienteUsuario;
            var cuenta = new Cuenta
            {
                Usuario = new Usuario { IdUsuario = id, Nombre = alta.Nombre, Correo = alta.Correo,
                    PasswordHash = alta.PasswordHash, Telefono = alta.Telefono, Activo = true }, ClienteId = id
            };
            foreach (var consentimiento in alta.Consentimientos)
            {
                cuenta.Consentimientos.Add(new(consentimiento.Tipo, consentimiento.VersionDocumento, consentimiento.Aceptado, alta.Operacion.FechaUtc));
                cuenta.Evidencias.Add(alta.Operacion);
            }
            cuentas.Add(id, cuenta); correos.Add(alta.CorreoNormalizado, id);
            RegistrarEvento(id, "ALTA_DEMOSTRACION", alta.Operacion);
            return Task.FromResult(new RegistroClientePersistido(EstadoRegistroCliente.Creado));
        }
    }
    public Task<ClientePerfil?> ObtenerPorUsuarioAsync(long idUsuario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (puerta)
        {
            return Task.FromResult(cuentas.TryGetValue(idUsuario, out var c)
                ? new ClientePerfil(c.ClienteId, idUsuario, c.Usuario.Nombre, c.Usuario.Correo, Copia(c.Perfil)) : null);
        }
    }
    public Task ActualizarPorUsuarioAsync(long idUsuario, ClientePerfilDatos datos, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            Propia(idUsuario).Perfil = Copia(ClientesValidacion.Perfil(datos));
            RegistrarEvento(idUsuario, "PERFIL_DEMOSTRACION", operacion); return Task.CompletedTask;
        }
    }
    public Task<ClienteDireccion[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (puerta) return Task.FromResult(Propia(idUsuario).Direcciones.Values.Where(x => x.Activa)
            .OrderBy(x => x.IdDireccion).Select(x => Copia(x)).ToArray());
    }
    public Task<ClienteDireccion> CrearPorUsuarioAsync(long idUsuario, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            var cuenta = Propia(idUsuario);
            if (cuenta.Direcciones.Count >= 100) throw new AccessFailure(400, "La demostración admite hasta 100 direcciones por cliente.");
            var direccion = new ClienteDireccion(++siguienteDireccion, Copia(ClientesValidacion.Direccion(datos)), false, true);
            cuenta.Direcciones.Add(direccion.IdDireccion, direccion);
            RegistrarEvento(idUsuario, "DIRECCION_CREADA_DEMOSTRACION", operacion);
            return Task.FromResult(Copia(direccion));
        }
    }
    public Task ActualizarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            var cuenta = Propia(idUsuario); var anterior = DireccionPropia(cuenta, idDireccion);
            cuenta.Direcciones[idDireccion] = anterior with { Datos = Copia(ClientesValidacion.Direccion(datos)) };
            RegistrarEvento(idUsuario, "DIRECCION_EDITADA_DEMOSTRACION", operacion); return Task.CompletedTask;
        }
    }
    public Task EliminarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            var cuenta = Propia(idUsuario); var anterior = DireccionPropia(cuenta, idDireccion);
            cuenta.Direcciones[idDireccion] = anterior with { Activa = false, Principal = false };
            RegistrarEvento(idUsuario, "DIRECCION_ELIMINADA_DEMOSTRACION", operacion); return Task.CompletedTask;
        }
    }
    public Task MarcarPrincipalPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            var cuenta = Propia(idUsuario); DireccionPropia(cuenta, idDireccion);
            foreach (var id in cuenta.Direcciones.Keys.ToArray())
            {
                var anterior = cuenta.Direcciones[id];
                cuenta.Direcciones[id] = anterior with { Principal = anterior.Activa && id == idDireccion };
            }
            RegistrarEvento(idUsuario, "PRINCIPAL_DEMOSTRACION", operacion); return Task.CompletedTask;
        }
    }
    Task<ClienteConsentimiento[]> IRepositorioConsentimientosCliente.ListarPorUsuarioAsync(long idUsuario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (puerta) return Task.FromResult(Propia(idUsuario).Consentimientos.OrderByDescending(x => x.RegistradoEn).ToArray());
    }
    public Task RegistrarPorUsuarioAsync(long idUsuario, ClienteConsentimientoDatos datos, ClienteOperacion operacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Actor(idUsuario, operacion);
        lock (puerta)
        {
            var c = Propia(idUsuario);
            if (c.Consentimientos.Count >= 1000) throw new AccessFailure(400, "La demostración alcanzó su límite de historial.");
            // Orden inequívoco aun si dos decisiones llegan en el mismo instante del reloj.
            var ultima = c.Consentimientos.Count == 0 ? DateTimeOffset.MinValue : c.Consentimientos.Max(x => x.RegistradoEn);
            var fecha = operacion.FechaUtc > ultima ? operacion.FechaUtc : ultima.AddTicks(1);
            c.Consentimientos.Add(new(datos.Tipo, datos.VersionDocumento, datos.Aceptado, fecha));
            c.Evidencias.Add(operacion);
            RegistrarEvento(idUsuario, "CONSENTIMIENTO_DEMOSTRACION", operacion); return Task.CompletedTask;
        }
    }

    public Task<LoginOutcome?> LoginAsync(LoginRequest solicitud, string host, string correlacion,
        IPasswordHasher<Usuario> hasher, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Host(host);
        var correo = CredentialPolicy.NormalizeEmail(solicitud.Correo);
        if (correo is null || solicitud.Password is not { Length: >= 1 and <= 128 }) return Task.FromResult<LoginOutcome?>(null);
        lock (puerta)
        {
            var ahora = reloj.GetUtcNow();
            var cuenta = correos.TryGetValue(correo, out var id) ? Propia(id) : null;
            var valido = hasher.VerifyHashedPassword(cuenta?.Usuario ?? new Usuario(), cuenta?.Usuario.PasswordHash ?? hashDesconocido, solicitud.Password)
                != PasswordVerificationResult.Failed;
            if (cuenta is null) return Task.FromResult<LoginOutcome?>(null);
            if (cuenta.BloqueadoHasta > ahora) return Task.FromResult<LoginOutcome?>(null);
            if (cuenta.BloqueadoHasta.HasValue) { cuenta.Fallos = 0; cuenta.BloqueadoHasta = null; }
            var op = new ClienteOperacion(cuenta.Usuario.IdUsuario, ahora, correlacion, null);
            if (!valido)
            {
                if (++cuenta.Fallos >= 5) cuenta.BloqueadoHasta = ahora.AddMinutes(15);
                RegistrarEvento(cuenta.Usuario.IdUsuario, "LOGIN_RECHAZADO_DEMOSTRACION", op);
                return Task.FromResult<LoginOutcome?>(null);
            }
            cuenta.Fallos = 0;
            foreach (var clave in sesiones.Where(x => x.Value.Expira <= ahora).Select(x => x.Key).ToArray()) sesiones.Remove(clave);
            if (sesiones.Count >= 1000) throw new AccessFailure(503, "La demostración alcanzó su límite de sesiones.");
            var credencial = MfaCryptography.NewCredential(); var expira = ahora.AddMinutes(30);
            sesiones.Add(MfaCryptography.Hash(credencial), new(cuenta.Usuario.IdUsuario, host, expira));
            RegistrarEvento(cuenta.Usuario.IdUsuario, "LOGIN_DEMOSTRACION", op);
            return Task.FromResult<LoginOutcome?>(new(new AuthSession(credencial, expira.UtcDateTime, PerfilAcceso(cuenta)), null));
        }
    }
    private static AuthProfile PerfilAcceso(Cuenta c) => new(c.Usuario.IdUsuario, c.Usuario.Correo, c.Usuario.Nombre, [], [], []);
    public Task<AuthProfile?> ValidateAsync(string credencial, string host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Host(host);
        if (!MfaCryptography.IsCredential(credencial)) return Task.FromResult<AuthProfile?>(null);
        lock (puerta)
        {
            var clave = MfaCryptography.Hash(credencial);
            if (!sesiones.TryGetValue(clave, out var sesion) || sesion.Host != host) return Task.FromResult<AuthProfile?>(null);
            if (sesion.Expira <= reloj.GetUtcNow()) { sesiones.Remove(clave); return Task.FromResult<AuthProfile?>(null); }
            var cuenta = Propia(sesion.Usuario);
            if (cuenta.BloqueadoHasta > reloj.GetUtcNow()) return Task.FromResult<AuthProfile?>(null);
            return Task.FromResult<AuthProfile?>(PerfilAcceso(cuenta));
        }
    }
    public Task LogoutAsync(string credencial, string host, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Host(host);
        if (!MfaCryptography.IsCredential(credencial)) return Task.CompletedTask;
        lock (puerta)
        {
            var clave = MfaCryptography.Hash(credencial);
            if (sesiones.TryGetValue(clave, out var sesion) && sesion.Host == host)
            {
                sesiones.Remove(clave);
                RegistrarEvento(sesion.Usuario, "LOGOUT_DEMOSTRACION", new(sesion.Usuario, reloj.GetUtcNow(), correlacion, null));
            }
            return Task.CompletedTask;
        }
    }
    public EventoClienteDemostracion[] Eventos()
    { lock (puerta) return eventos.ToArray(); }
}

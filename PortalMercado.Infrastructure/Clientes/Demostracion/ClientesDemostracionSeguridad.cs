using Microsoft.AspNetCore.Identity;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Infrastructure.Clientes.Demostracion;

public sealed partial class ClientesDemostracion
{
    private sealed record Enlace(long Usuario, string Proposito, DateTimeOffset Expira, string? Correo = null);
    private sealed record Limite(DateTimeOffset Inicio, int Intentos);
    private readonly Dictionary<string, Enlace> enlaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Limite> limitesSeguridad = new(StringComparer.Ordinal);
    private static PasswordFailure Invalido() => new(400, PasswordMessages.Invalid);
    private ClienteOperacion Op(long id, string correlacion) => new(id, reloj.GetUtcNow(), correlacion, null);
    private void LimpiarSeguridad()
    {
        var ahora = reloj.GetUtcNow();
        foreach (var k in enlaces.Where(x => x.Value.Expira <= ahora).Select(x => x.Key).ToArray()) enlaces.Remove(k);
        foreach (var k in limitesSeguridad.Where(x => x.Value.Inicio <= ahora.AddMinutes(-15)).Select(x => x.Key).ToArray()) limitesSeguridad.Remove(k);
    }
    private bool Permitir(string clave)
    {
        LimpiarSeguridad();
        if (limitesSeguridad.TryGetValue(clave, out var limite))
        {
            if (limite.Intentos >= 3) return false;
            limitesSeguridad[clave] = limite with { Intentos = limite.Intentos + 1 };
        }
        else
        {
            if (limitesSeguridad.Count >= 1000) throw new PasswordFailure(503, "Espera antes de volver a intentarlo.");
            limitesSeguridad.Add(clave, new(reloj.GetUtcNow(), 1));
        }
        return true;
    }
    private void RevocarEnlaces(long id, string? proposito = null)
    {
        foreach (var k in enlaces.Where(x => x.Value.Usuario == id && (proposito is null || x.Value.Proposito == proposito)).Select(x => x.Key).ToArray()) enlaces.Remove(k);
    }
    private void RevocarSesiones(long id)
    { foreach (var k in sesiones.Where(x => x.Value.Usuario == id).Select(x => x.Key).ToArray()) sesiones.Remove(k); }
    private Cuenta ExigirSesion(string credencial, string host)
    {
        Host(host);
        if (!MfaCryptography.IsCredential(credencial) || !sesiones.TryGetValue(MfaCryptography.Hash(credencial), out var s)
            || s.Host != host || s.Expira <= reloj.GetUtcNow()) throw new PasswordFailure(401, "Inicia sesión nuevamente.");
        var c = Propia(s.Usuario);
        if (c.BloqueadoHasta > reloj.GetUtcNow()) throw new PasswordFailure(401, "Inicia sesión nuevamente.");
        return c;
    }
    private static bool Verificar(Cuenta c, string password, IPasswordHasher<Usuario> hasher) =>
        password is { Length: >= 1 and <= 128 } && hasher.VerifyHashedPassword(c.Usuario, c.Usuario.PasswordHash, password) != PasswordVerificationResult.Failed;
    private void ExigirPassword(Cuenta c, string password, IPasswordHasher<Usuario> hasher, string correlacion)
    {
        if (c.BloqueadoHasta.HasValue && c.BloqueadoHasta <= reloj.GetUtcNow()) { c.BloqueadoHasta = null; c.Fallos = 0; }
        if (Verificar(c, password, hasher)) return;
        if (++c.Fallos >= 5) { c.BloqueadoHasta = reloj.GetUtcNow().AddMinutes(15); RevocarSesiones(c.Usuario.IdUsuario); }
        RegistrarEvento(c.Usuario.IdUsuario, "PASSWORD_ACTUAL_RECHAZADA_DEMOSTRACION", Op(c.Usuario.IdUsuario, correlacion));
        throw new PasswordFailure(400, "No se pudo completar el cambio. Revisa los datos o inicia sesión nuevamente.");
    }
    private static void ValidarNueva(string password, string confirmacion)
    { if (!CredentialPolicy.IsValidPassword(password) || password != confirmacion) throw new PasswordFailure(400, PasswordMessages.Policy); }

    internal string? PrepararRecuperacion(string correo, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (puerta)
        {
            if (!Permitir("rec:" + MfaCryptography.Hash(correo))) return null;
            var token = MfaCryptography.NewCredential();
            if (correos.TryGetValue(correo, out var id))
            {
                RevocarEnlaces(id, "PASSWORD");
                enlaces.Add(MfaCryptography.Hash(token), new(id, "PASSWORD", reloj.GetUtcNow().AddMinutes(15)));
                RegistrarEvento(id, "RECUPERACION_SOLICITADA_DEMOSTRACION", Op(id, correlacion));
            }
            // Mismo mensaje para cuenta desconocida; su token no tiene asociación.
            return token;
        }
    }
    internal void CancelarEnlace(string token)
    { lock (puerta) enlaces.Remove(MfaCryptography.Hash(token)); }
    internal void Restablecer(ResetPasswordRequest request, IPasswordHasher<Usuario> hasher, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ValidarNueva(request.NewPassword, request.ConfirmPassword);
        if (!MfaCryptography.IsCredential(request.Token)) throw Invalido();
        lock (puerta)
        {
            var clave = MfaCryptography.Hash(request.Token);
            if (!enlaces.TryGetValue(clave, out var e) || e.Proposito != "PASSWORD" || e.Expira <= reloj.GetUtcNow()) throw Invalido();
            var c = Propia(e.Usuario);
            if (Verificar(c, request.NewPassword, hasher)) throw Invalido();
            c.Usuario.PasswordHash = hasher.HashPassword(c.Usuario, request.NewPassword);
            c.Fallos = 0; c.BloqueadoHasta = null;
            RevocarEnlaces(e.Usuario); RevocarSesiones(e.Usuario);
            RegistrarEvento(e.Usuario, "PASSWORD_RESTABLECIDA_DEMOSTRACION", Op(e.Usuario, correlacion));
        }
    }
    internal void CambiarPassword(long actor, string credencial, string host, ChangePasswordRequest request,
        IPasswordHasher<Usuario> hasher, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ValidarNueva(request.NewPassword, request.ConfirmPassword);
        lock (puerta)
        {
            var c = ExigirSesion(credencial, host);
            if (c.Usuario.IdUsuario != actor) throw new PasswordFailure(401, "Inicia sesión nuevamente.");
            ExigirPassword(c, request.CurrentPassword, hasher, correlacion);
            if (request.NewPassword == request.CurrentPassword) throw new PasswordFailure(400, "La nueva contraseña debe ser diferente de la actual.");
            c.Usuario.PasswordHash = hasher.HashPassword(c.Usuario, request.NewPassword); c.Fallos = 0; c.BloqueadoHasta = null;
            RevocarEnlaces(actor); RevocarSesiones(actor);
            RegistrarEvento(actor, "PASSWORD_CAMBIADA_DEMOSTRACION", Op(actor, correlacion));
        }
    }
    internal string PrepararCorreo(string credencial, string host, CambioCorreoClienteSolicitud request,
        IPasswordHasher<Usuario> hasher, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var correo = CredentialPolicy.NormalizeEmail(request.Correo);
        if (correo is null || !correo.EndsWith("@EXAMPLE.INVALID", StringComparison.Ordinal) ||
            correo != CredentialPolicy.NormalizeEmail(request.ConfirmarCorreo))
            throw new PasswordFailure(400, "Repite el mismo correo ficticio terminado en @example.invalid.");
        lock (puerta)
        {
            var c = ExigirSesion(credencial, host); var id = c.Usuario.IdUsuario;
            if (!Permitir("correo:" + id)) throw new PasswordFailure(429, "Espera 15 minutos antes de solicitar otro cambio.");
            ExigirPassword(c, request.PasswordActual, hasher, correlacion);
            if (correos.ContainsKey(correo)) throw new PasswordFailure(400, "No se pudo solicitar ese cambio de correo.");
            RevocarEnlaces(id, "CORREO");
            var token = MfaCryptography.NewCredential();
            enlaces.Add(MfaCryptography.Hash(token), new(id, "CORREO", reloj.GetUtcNow().AddMinutes(15), request.Correo.Trim()));
            RegistrarEvento(id, "CAMBIO_CORREO_SOLICITADO_DEMOSTRACION", Op(id, correlacion));
            return token;
        }
    }
    internal void ConfirmarCorreo(string credencial, string host, string token, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!MfaCryptography.IsCredential(token)) throw Invalido();
        lock (puerta)
        {
            var c = ExigirSesion(credencial, host); var id = c.Usuario.IdUsuario;
            if (!enlaces.TryGetValue(MfaCryptography.Hash(token), out var e) || e.Proposito != "CORREO" || e.Usuario != id
                || e.Expira <= reloj.GetUtcNow() || e.Correo is null) throw Invalido();
            var nuevo = CredentialPolicy.NormalizeEmail(e.Correo)!;
            if (correos.ContainsKey(nuevo)) throw new PasswordFailure(400, "No se pudo completar el cambio de correo. Solicita otro enlace.");
            correos.Remove(CredentialPolicy.NormalizeEmail(c.Usuario.Correo)!);
            correos.Add(nuevo, id); c.Usuario.Correo = e.Correo;
            // Solo acredita una prueba local; nunca una verificación externa del buzón.
            c.Usuario.EmailConfirmado = false;
            RevocarEnlaces(id); RevocarSesiones(id);
            RegistrarEvento(id, "CORREO_CAMBIADO_DEMOSTRACION", Op(id, correlacion));
        }
    }
}

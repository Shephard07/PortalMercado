using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Clientes.Demostracion;

// Solo Development; transporte Pickup existente. Nunca SMTP ni cuentas SQL.
public sealed class SeguridadClientesDemostracion(ClientesDemostracion datos, IPasswordHasher<Usuario> hasher,
    IRecoveryDelivery? delivery = null, IConfiguration? config = null, IAccountDelivery? correoDelivery = null)
    : IAuthService, IPasswordService, IMfaService, ICorreoClienteService
{
    public Task<LoginOutcome?> LoginAsync(LoginRequest request, string host, string correlationId, CancellationToken ct) =>
        datos.LoginAsync(request, host, correlationId, hasher, ct);
    public Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct) => datos.ValidateAsync(credential, host, ct);
    public Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct) => datos.LogoutAsync(credential, host, correlationId, ct);
    private Uri Origen()
    {
        if (config is null || config["Recovery:Provider"] != "Pickup" ||
            !Uri.TryCreate(config["Recovery:WebBaseUrl"], UriKind.Absolute, out var origen) || origen.Scheme != "https" ||
            !string.IsNullOrEmpty(origen.UserInfo) || origen.AbsolutePath != "/" || origen.Query.Length != 0 || origen.Fragment.Length != 0)
            throw new PasswordFailure(503, "Para probar los enlaces configura el correo local Pickup y Recovery:WebBaseUrl con el origen HTTPS de la Web.");
        return origen;
    }
    public async Task RequestAsync(ForgotPasswordRequest request, string correlation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var correo = CredentialPolicy.NormalizeEmail(request.Correo);
        if (correo is null || !correo.EndsWith("@EXAMPLE.INVALID", StringComparison.Ordinal))
            throw new PasswordFailure(400, "Utiliza un correo ficticio terminado en @example.invalid.");
        var origen = Origen();
        if (delivery is null) throw new PasswordFailure(503, PasswordMessages.Unavailable);
        delivery.CheckConfiguration();
        var token = datos.PrepararRecuperacion(correo, correlation, ct);
        if (token is null) return;
        try { await delivery.DeliverAsync(request.Correo.Trim(), new Uri(origen, "Cuenta/Restablecer?token=" + token), ct); }
        catch { datos.CancelarEnlace(token); throw; }
    }
    public Task ResetAsync(ResetPasswordRequest request, string correlation, CancellationToken ct)
    { datos.Restablecer(request, hasher, correlation, ct); return Task.CompletedTask; }
    public Task ChangeAsync(long actor, string credential, string host, ChangePasswordRequest request, string correlation, CancellationToken ct)
    { datos.CambiarPassword(actor, credential, host, request, hasher, correlation, ct); return Task.CompletedTask; }
    public async Task SolicitarAsync(string credential, string host, CambioCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var origen = Origen();
        if (correoDelivery is null) throw new PasswordFailure(503, PasswordMessages.Unavailable);
        correoDelivery.CheckConfiguration();
        var token = datos.PrepararCorreo(credential, host, solicitud, hasher, correlacion, ct);
        var enlace = new Uri(origen, "Cuenta/ConfirmarCorreo?token=" + token);
        try
        {
            await correoDelivery.DeliverMessageAsync(solicitud.Correo.Trim(), "Confirmar cambio de correo - PortalMercado",
                "PRUEBA LOCAL. Se solicito cambiar el correo de tu cuenta ficticia. Abre este enlace e inicia sesion con la cuenta original para confirmar en el formulario.\r\n\r\n" +
                enlace.AbsoluteUri + "\r\n\r\nCaduca en 15 minutos y solo puede usarse una vez. Ignora el mensaje si no solicitaste el cambio. No se envio por Internet.", ct);
        }
        catch { datos.CancelarEnlace(token); throw; }
    }
    public Task ConfirmarAsync(string credential, string host, ConfirmacionCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct)
    { datos.ConfirmarCorreo(credential, host, solicitud.Token, correlacion, ct); return Task.CompletedTask; }
    public Task<MfaEnrollment?> GetEnrollmentAsync(string challenge, string host, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<MfaEnrollment?>(null); }
    public Task<MfaCompletion?> CompleteAsync(string challenge, string code, string host, string correlationId, CancellationToken ct, bool regenerateRecoveryCodes = false)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<MfaCompletion?>(null); }
}

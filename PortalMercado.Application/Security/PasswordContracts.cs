using System.ComponentModel.DataAnnotations;
namespace PortalMercado.Application.Security;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = "";
}
public sealed class ResetPasswordRequest
{
    [Required, StringLength(43, MinimumLength = 43)] public string Token { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
public sealed class ChangePasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
public sealed class PasswordFailure(int status, string message) : Exception(message) { public int Status { get; } = status; }
public static class PasswordMessages
{
    public const string Requested = "Si la cuenta está habilitada, podrás recibir instrucciones de recuperación. Si solicitaste varios enlaces, utiliza el más reciente.";
    public const string Invalid = "No se pudo restablecer la contraseña. Solicita un enlace nuevo y revisa los datos.";
    public const string Unavailable = "No se pudo procesar el correo de recuperación. Inténtalo más tarde.";
    public const string Policy = "La contraseña debe tener de 12 a 128 caracteres e incluir mayúscula, minúscula, número y símbolo; ambas contraseñas deben coincidir.";
}
public interface IPasswordService
{
    Task RequestAsync(ForgotPasswordRequest request, string correlation, CancellationToken ct);
    Task ResetAsync(ResetPasswordRequest request, string correlation, CancellationToken ct);
    Task ChangeAsync(long actor, string credential, string host, ChangePasswordRequest request, string correlation, CancellationToken ct);
}
// Interfaz sustituible: SMTP real o salida de correo local exclusivamente en Development.
public interface IRecoveryDelivery
{
    void CheckConfiguration();
    Task DeliverAsync(string recipient, Uri resetLink, CancellationToken ct);
}

using System.ComponentModel.DataAnnotations;

namespace PortalMercado.Application.Security;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Correo { get; set; } = "";
    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}

public sealed record AuthProfile(long IdUsuario, string Correo, string Nombre,
    string[] Roles, long[] Negocios, long[] Mercados)
{
    public string[] Permisos { get; init; } = [];
}
public sealed record AuthSession(string Credential, DateTime ExpiresUtc, AuthProfile Profile);

public interface IAuthService
{
    Task<LoginOutcome?> LoginAsync(LoginRequest request, string host, string correlationId, CancellationToken ct);
    Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct);
    Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct);
}

public interface IAdminBootstrap
{
    Task CreateAsync(string correo, string nombre, string password, CancellationToken ct);
    Task CreateLocalTestUserAsync(string correo, string nombre, string password, CancellationToken ct);
}

public static class CredentialPolicy
{
    // Correo VARCHAR: regla deliberadamente ASCII para coincidir con la normalización SQL.
    public static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 254 ||
            email.Any(c => c > 127 || char.IsWhiteSpace(c)) ||
            !new EmailAddressAttribute().IsValid(email)) return null;
        return email.ToUpperInvariant();
    }

    public static bool IsValidPassword(string? value) => value is { Length: >= 12 and <= 128 }
        && value.Any(char.IsUpper) && value.Any(char.IsLower)
        && value.Any(char.IsDigit) && value.Any(c => !char.IsLetterOrDigit(c));
}

public static class AuthenticationRules
{
    public static bool RequiresSecondFactor(bool enabled, IEnumerable<string> roles) =>
        enabled || roles.Any(r => r.StartsWith("ADMIN_", StringComparison.OrdinalIgnoreCase));
}

// Solo mensajes definidos por la aplicación; nunca incorporar datos del operador.
public sealed class BootstrapFailureException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class BootstrapValidation
{
    public static void Validate(string correo, string nombre, string password, bool ficticio)
    {
        var normalized = CredentialPolicy.NormalizeEmail(correo);
        if (normalized is null || (ficticio && !normalized.EndsWith("@EXAMPLE.INVALID", StringComparison.Ordinal)))
            throw new BootstrapFailureException("CORREO_INVALIDO", ficticio
                ? "Usa un correo ficticio válido terminado en @example.invalid."
                : "Configura un correo válido para el aprovisionamiento.");
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 150)
            throw new BootstrapFailureException("NOMBRE_INVALIDO", "Configura un nombre de 1 a 150 caracteres.");
        if (!CredentialPolicy.IsValidPassword(password))
            throw new BootstrapFailureException("PASSWORD_INVALIDA",
                "La contraseña debe tener de 12 a 128 caracteres e incluir mayúscula, minúscula, número y símbolo.");
    }
}

public sealed record LoginOutcome(AuthSession? Session, MfaChallenge? Mfa);
public sealed record MfaChallenge(string Challenge, bool EnrollmentRequired, DateTime ExpiresUtc);
public sealed record MfaEnrollment(string Secret, string OtpAuthUri);
public sealed record MfaCompletion(AuthSession Session, string[] RecoveryCodes);
public sealed class MfaChallengeRequest
{
    [Required, StringLength(43, MinimumLength = 43)]
    public string Challenge { get; set; } = "";
}
public sealed class MfaVerifyRequest
{
    [Required, StringLength(43, MinimumLength = 43)]
    public string Challenge { get; set; } = "";
    [Required, StringLength(64)]
    public string Code { get; set; } = "";
    public bool RegenerateRecoveryCodes { get; set; }
}
public sealed class MfaCodeRequest
{
    [Required, StringLength(64)]
    public string Code { get; set; } = "";
    public bool RegenerateRecoveryCodes { get; set; }
}
public interface IMfaService
{
    Task<MfaEnrollment?> GetEnrollmentAsync(string challenge, string host, CancellationToken ct);
    Task<MfaCompletion?> CompleteAsync(string challenge, string code, string host, string correlationId, CancellationToken ct, bool regenerateRecoveryCodes = false);
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OtpNet;
namespace PortalMercado.Infrastructure.Security;

public sealed class MfaCryptography(IDataProtectionProvider provider, IConfiguration configuration, IHostEnvironment environment)
{
    private readonly IDataProtector protector = provider.CreateProtector("PortalMercado.MfaSecret.v1");
    private void CheckKeyStorage()
    {
        if (!environment.IsDevelopment() && (!OperatingSystem.IsWindows() ||
            string.IsNullOrWhiteSpace(configuration["Security:DataProtectionKeyPath"])))
            throw new InvalidOperationException("MFA requiere un almacén de claves protegido configurado para este ambiente.");
    }
    public string Protect(string secret) { CheckKeyStorage(); return protector.Protect(secret); }
    public string Unprotect(string secret) { CheckKeyStorage(); return protector.Unprotect(secret); }
    public static string NewSecret() => Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
    public static string NewCredential() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    public static bool IsCredential(string? value) => value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static bool TryVerifyTotp(string secret, string code, DateTime now, long? previousStep, out long matched)
    {
        matched = -1;
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return false;
        return new Totp(Base32Encoding.ToBytes(secret), step: 30, totpSize: 6)
            .VerifyTotp(now, code, out matched, new VerificationWindow(previous: 1, future: 1))
            && (!previousStep.HasValue || matched > previousStep.Value);
    }
    public static string RecoveryCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static string? NormalizeRecovery(string code)
    {
        var value = code.Trim().Replace("-", "").ToUpperInvariant();
        return value.Length == 32 && value.All(char.IsAsciiHexDigit) ? value : null;
    }
    public static string EnrollmentUri(string secret, string correo) =>
        new OtpUri(OtpType.Totp, secret, correo, "PortalMercado").ToString();
}

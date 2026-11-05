using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Security;
namespace PortalMercado.Infrastructure.Security;

public sealed class RecoveryDelivery(IConfiguration config, IHostEnvironment environment) : IRecoveryDelivery
{
    public string PickupPath => config["Recovery:PickupPath"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortalMercado", "CorreoPruebas");
    public void CheckConfiguration()
    {
        if (config["Recovery:Provider"] == "Pickup" && environment.IsDevelopment())
        {
            if (!Path.IsPathFullyQualified(PickupPath)) throw Unavailable();
            if (!string.IsNullOrWhiteSpace(environment.ContentRootPath))
            {
                var solutionRoot = Directory.GetParent(Path.GetFullPath(environment.ContentRootPath))?.FullName;
                if (solutionRoot is not null)
                {
                    var relative = Path.GetRelativePath(solutionRoot, Path.GetFullPath(PickupPath));
                    if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar)) throw Unavailable();
                }
            }
            return;
        }
        if (config["Recovery:Provider"] != "Smtp" || string.IsNullOrWhiteSpace(config["Recovery:Smtp:Host"]) ||
            !int.TryParse(config["Recovery:Smtp:Port"], out var port) || port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(config["Recovery:Smtp:User"]) || string.IsNullOrWhiteSpace(config["Recovery:Smtp:Password"])) throw Unavailable();
        try { _ = new MailAddress(config["Recovery:Smtp:From"] ?? ""); }
        catch (Exception e) when (e is FormatException or ArgumentException) { throw Unavailable(); }
    }
    public async Task DeliverAsync(string recipient, Uri resetLink, CancellationToken ct)
    {
        CheckConfiguration();
        var body = "Se solicitó recuperar el acceso a PortalMercado.\r\n\r\n" + resetLink.AbsoluteUri +
            "\r\n\r\nSi la cuenta está habilitada, el enlace permite establecer una contraseña nueva durante 15 minutos y una sola vez. " +
            "Solo sirve el enlace más reciente. Si no solicitaste este cambio, ignora este mensaje. " +
            "El segundo factor de las cuentas administrativas sigue siendo obligatorio.\r\n";
        try
        {
            // Se entrega el mismo tipo de mensaje para direcciones existentes y desconocidas.
            // Para una cuenta desconocida, el token aleatorio no está asociado a ningún usuario.
            if (config["Recovery:Provider"] == "Pickup")
            {
                Directory.CreateDirectory(PickupPath);
                var file = Path.Combine(PickupPath, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".eml");
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using var stream = new FileStream(file, options);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(("From: PortalMercado <no-reply@example.invalid>\r\nTo: " + recipient +
                    "\r\nSubject: Recuperacion de acceso a PortalMercado\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" + body).AsMemory(), ct);
                await writer.FlushAsync(ct);
                return;
            }
            using var message = new MailMessage(config["Recovery:Smtp:From"]!, recipient, "Recuperación de acceso a PortalMercado", body);
            using var smtp = new SmtpClient(config["Recovery:Smtp:Host"], int.Parse(config["Recovery:Smtp:Port"]!))
            { EnableSsl = true, UseDefaultCredentials = false, Credentials = new NetworkCredential(config["Recovery:Smtp:User"], config["Recovery:Smtp:Password"]) };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await smtp.SendMailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (Exception e) when (e is SmtpException or IOException or UnauthorizedAccessException or FormatException or ArgumentException or InvalidOperationException or System.Security.Authentication.AuthenticationException)
        { throw Unavailable(); }
    }
    private static PasswordFailure Unavailable() => new(503, PasswordMessages.Unavailable);
}

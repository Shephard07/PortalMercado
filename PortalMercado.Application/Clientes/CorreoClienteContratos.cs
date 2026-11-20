using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PortalMercado.Application.Clientes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambioCorreoClienteSolicitud
{
    [Required, EmailAddress, StringLength(254)] public string Correo { get; set; } = "";
    [Required, Compare(nameof(Correo))] public string ConfirmarCorreo { get; set; } = "";
    [Required, StringLength(128)] public string PasswordActual { get; set; } = "";
    public override string ToString() => nameof(CambioCorreoClienteSolicitud);
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ConfirmacionCorreoClienteSolicitud
{
    [Required, StringLength(43, MinimumLength = 43)] public string Token { get; set; } = "";
    public override string ToString() => nameof(ConfirmacionCorreoClienteSolicitud);
}
public interface ICorreoClienteService
{
    Task SolicitarAsync(string credential, string host, CambioCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct);
    Task ConfirmarAsync(string credential, string host, ConfirmacionCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct);
}

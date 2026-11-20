using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
namespace PortalMercado.Infrastructure.Clientes;
public sealed class CorreoClientePendiente : ICorreoClienteService
{
    private static Task Pendiente(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new PasswordFailure(503, "El cambio de correo requiere el contrato de identidad aprobado."); }
    public Task SolicitarAsync(string credential, string host, CambioCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct) => Pendiente(ct);
    public Task ConfirmarAsync(string credential, string host, ConfirmacionCorreoClienteSolicitud solicitud, string correlacion, CancellationToken ct) => Pendiente(ct);
}

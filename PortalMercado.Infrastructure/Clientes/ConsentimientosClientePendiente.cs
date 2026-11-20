using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Infrastructure.Clientes;

public sealed class ConsentimientosClientePendiente : IRepositorioConsentimientosCliente
{
    private static AccessFailure Pendiente(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return new(503, "Tus consentimientos no están disponibles en este momento."); }
    public Task<ClienteConsentimiento[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct) => throw Pendiente(ct);
    public Task RegistrarPorUsuarioAsync(long idUsuario, ClienteConsentimientoDatos datos, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
}

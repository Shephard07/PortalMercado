namespace PortalMercado.Application.Interfaces;

public interface IDatabaseHealthService
{
    Task<bool> CanConnectAsync(
        CancellationToken cancellationToken = default);

    Task<bool> CanReadMercadosAsync(
    CancellationToken cancellationToken = default);
}
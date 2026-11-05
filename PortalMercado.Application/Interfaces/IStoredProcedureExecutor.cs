using PortalMercado.Application.DTOs;

namespace PortalMercado.Application.Interfaces;

public interface IStoredProcedureExecutor
{
    Task<IReadOnlyList<SaldoNegocioDto>> ConsultarSaldosAsync(
        long? idNegocio = null,
        CancellationToken cancellationToken = default);
}
namespace PortalMercado.Application.DTOs;

public sealed record SaldoNegocioDto(
    long IdNegocio,
    string NombreComercial,
    decimal SaldoContable,
    decimal PendienteLiquidacion,
    decimal EnLiquidacionesAbiertas);
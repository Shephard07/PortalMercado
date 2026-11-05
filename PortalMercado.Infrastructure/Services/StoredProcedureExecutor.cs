using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.DTOs;
using PortalMercado.Application.Interfaces;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Services;

public sealed class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly PortalMercadoDbContext _context;

    public StoredProcedureExecutor(PortalMercadoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SaldoNegocioDto>> ConsultarSaldosAsync(
        long? idNegocio = null,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _context.Database.GetConnectionString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = "dbo.pm_ConsultarSaldos";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 15;

        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value =
            (object?)idNegocio ?? DBNull.Value;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("IdNegocio");
        var nombreOrdinal = reader.GetOrdinal("NombreComercial");
        var saldoOrdinal = reader.GetOrdinal("SaldoContable");
        var pendienteOrdinal = reader.GetOrdinal("PendienteLiquidacion");
        var abiertasOrdinal = reader.GetOrdinal("EnLiquidacionesAbiertas");

        var resultados = new List<SaldoNegocioDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            resultados.Add(new SaldoNegocioDto(
                reader.GetInt64(idOrdinal),
                reader.GetString(nombreOrdinal),
                reader.GetDecimal(saldoOrdinal),
                reader.GetDecimal(pendienteOrdinal),
                reader.GetDecimal(abiertasOrdinal)));
        }

        return resultados;
    }
}
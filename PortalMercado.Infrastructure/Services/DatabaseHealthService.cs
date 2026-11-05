using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortalMercado.Application.Interfaces;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Services;

public sealed class DatabaseHealthService : IDatabaseHealthService
{
    private readonly PortalMercadoDbContext _context;
    private readonly ILogger<DatabaseHealthService> _logger;

    public DatabaseHealthService(
        PortalMercadoDbContext context,
        ILogger<DatabaseHealthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> CanConnectAsync(
    CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
            await _context.Database.CloseConnectionAsync();

            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                "Conexión SQL fallida. Código: {SqlErrorNumber}. " +
                "Código interno: {InnerErrorCode}.",
                ex.Number,
                ex.InnerException?.HResult);

            return false;
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning(
                "La configuración de conexión SQL no es válida.");

            return false;
        }


    }

    public async Task<bool> CanReadMercadosAsync(
    CancellationToken cancellationToken = default)
    {
        try
        {
            var muestra = await _context.Mercados
                .AsNoTracking()
                .OrderBy(x => x.IdMercado)
                .Take(1)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Consulta EF de Mercado completada. Filas leídas: {RowCount}.",
                muestra.Count);

            return true;
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Falló la consulta EF de Mercado. Tipo de error: {ErrorType}.",
                ex.GetType().Name);

            return false;
        }
    }
}
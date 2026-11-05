using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Application.Interfaces;
using PortalMercado.Infrastructure.Services;

namespace PortalMercado.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("PortalMercado");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta configurar ConnectionStrings:PortalMercado.");
        }

        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "La configuración ConnectionStrings:PortalMercado no es válida.");
        }

        services.AddDbContext<PortalMercadoDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(150);
                sql.CommandTimeout(15);
            }));

        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();

        return services;
    }
}

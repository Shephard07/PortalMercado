using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Interfaces;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Infrastructure.Services;
using Xunit;

namespace PortalMercado.Tests;

[Trait("Category", "SqlIntegration")]
public sealed class SqlIntegrationTests
{
    private static PortalMercadoDbContext CreateContext()
    {
        const string connectionString =
            @"Server=.\SQLEXPRESS;" +
            "Database=PortalMercado_ET01_Test;" +
            "Integrated Security=True;" +
            "Encrypt=True;" +
            "TrustServerCertificate=True;" +
            "Connect Timeout=5;";

        var options = new DbContextOptionsBuilder<PortalMercadoDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(150);
                sql.CommandTimeout(15);
            })
            .Options;

        return new PortalMercadoDbContext(options);
    }

    [SqlIntegrationFact]
    public async Task ConexionLocal_Disponible()
    {
        await using var context = CreateContext();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [SqlIntegrationFact]
    public async Task EntidadesPiloto_SeMaterializanConSusRelaciones()
    {
        await using var context = CreateContext();

        var mercado = await context.Mercados
            .AsNoTracking()
            .SingleAsync(x => x.Nombre == "Mercado de prueba ET01");

        var sector = await context.Sectores
            .AsNoTracking()
            .SingleAsync(x =>
                x.IdMercado == mercado.IdMercado &&
                x.Codigo == "ET01");

        var puesto = await context.Puestos
            .AsNoTracking()
            .SingleAsync(x =>
                x.IdSector == sector.IdSector &&
                x.Codigo == "P-ET01");

        var negocio = await context.Negocios
            .AsNoTracking()
            .SingleAsync(x =>
                x.IdMercado == mercado.IdMercado &&
                x.Slug == "negocio-prueba-et01");

        var usuario = await context.Usuarios
            .AsNoTracking()
            .SingleAsync(x => x.Correo == "et01@example.invalid");

        Assert.True(mercado.Activo);
        Assert.Equal(mercado.IdMercado, sector.IdMercado);
        Assert.Equal(sector.IdSector, puesto.IdSector);
        Assert.Equal("DISPONIBLE", puesto.Estado);
        Assert.Equal(mercado.IdMercado, negocio.IdMercado);
        Assert.Equal("ACTIVO", negocio.Estado);
        Assert.False(usuario.Activo);
        Assert.Null(usuario.UltimoAcceso);
    }

    [SqlIntegrationFact]
    public async Task ConsultarSaldos_DevuelveNegocioFicticioConImportesCero()
    {
        await using var context = CreateContext();

        var negocio = await context.Negocios
            .AsNoTracking()
            .SingleAsync(x => x.Slug == "negocio-prueba-et01");

        IStoredProcedureExecutor executor =
            new StoredProcedureExecutor(context);

        var resultados = await executor.ConsultarSaldosAsync(
            negocio.IdNegocio);

        var saldo = Assert.Single(resultados);

        Assert.Equal(negocio.IdNegocio, saldo.IdNegocio);
        Assert.Equal("Negocio de prueba ET01", saldo.NombreComercial);
        Assert.Equal(0m, saldo.SaldoContable);
        Assert.Equal(0m, saldo.PendienteLiquidacion);
        Assert.Equal(0m, saldo.EnLiquidacionesAbiertas);
    }
}
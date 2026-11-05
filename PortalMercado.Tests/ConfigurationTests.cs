using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Interfaces;
using PortalMercado.Infrastructure;
using PortalMercado.Infrastructure.Persistence;
using Xunit;

namespace PortalMercado.Tests;

public sealed class ConfigurationTests
{
    private static IConfiguration Configuration(string? connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalMercado"] = connectionString
            }).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ConexionAusente_ImpideInicioConMensajeSeguro(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(Configuration(value)));
        Assert.Contains("ConnectionStrings:PortalMercado", exception.Message);
    }

    [Fact]
    public void ConexionMalformada_NoRevelaSuContenido()
    {
        const string invalid = "CLAVE_DESCONOCIDA=MARCADOR_PRIVADO";
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(Configuration(invalid)));
        Assert.DoesNotContain("MARCADOR_PRIVADO", exception.ToString());
    }

    [Fact]
    public void ServiciosSeResuelvenYContextoRespetaAlcance_SinAbrirSQL()
    {
        const string connection =
            "Server=127.0.0.1;Database=Configuracion_Test;Integrated Security=True;";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration(connection));
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var context = first.ServiceProvider.GetRequiredService<PortalMercadoDbContext>();
        Assert.Same(context,
            first.ServiceProvider.GetRequiredService<PortalMercadoDbContext>());
        Assert.NotSame(context,
            second.ServiceProvider.GetRequiredService<PortalMercadoDbContext>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<IDatabaseHealthService>());
        Assert.NotNull(first.ServiceProvider.GetRequiredService<IStoredProcedureExecutor>());
        var expected = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
        var actual = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(context.Database.GetConnectionString());
        // EF puede agregar Application Name y normalizar sinónimos; verificar la configuración efectiva.
        Assert.Equal(expected.DataSource, actual.DataSource);
        Assert.Equal(expected.InitialCatalog, actual.InitialCatalog);
        Assert.Equal(expected.IntegratedSecurity, actual.IntegratedSecurity);
    }
}

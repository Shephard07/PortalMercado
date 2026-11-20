using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Exploracion;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Clientes;

public sealed class ExploracionDesdeTiendasTests
{
    [Fact]
    public async Task Adaptador_reutiliza_catalogo_y_solo_publica_tiendas_visibles()
    {
        using var servicios = Crear(); using var scope = servicios.CreateScope();
        var datos = await scope.ServiceProvider.GetRequiredService<IRepositorioExploracionPublica>().LeerAsync(default);
        Assert.Equal(2, datos.Tiendas.Length);
        Assert.All(datos.Tiendas, t => Assert.Contains(t.IdNegocio, new long[] { 1001, 1002 }));
        Assert.NotEmpty(datos.Productos);
        foreach (var oculto in new[] { "Borrador reservado", "Agotado reservado", "Suspendido reservado" })
            Assert.DoesNotContain(datos.Productos, p => p.Producto.Nombre == oculto);
        Assert.All(datos.Productos, p => Assert.Contains(datos.Tiendas, t => t.IdNegocio == p.IdNegocio));
    }
    [Theory]
    [InlineData(1, 2000)]
    [InlineData(100, 1)]
    public async Task Limites_no_devuelven_resultados_truncados(int tiendas, int productos)
    {
        using var servicios = Crear(); using var scope = servicios.CreateScope(); var sp = scope.ServiceProvider;
        var adaptador = new ExploracionDesdeTiendas(sp.GetRequiredService<ITiendaService>(), sp.GetRequiredService<CatalogoTiendaServicio>(), new(tiendas, productos));
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => adaptador.LeerAsync(default))).Status);
    }
    [Fact]
    public async Task Cancelacion_del_visitante_se_conserva()
    {
        using var servicios = Crear(); using var scope = servicios.CreateScope(); using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<IRepositorioExploracionPublica>().LeerAsync(ct.Token));
    }
    private static ServiceProvider Crear()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Tiendas:Demostracion"] = "true" }).Build();
        var s = new ServiceCollection(); s.AddTiendas(config, new Desarrollo()); return s.BuildServiceProvider();
    }
    private sealed class Desarrollo : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

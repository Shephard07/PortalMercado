using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class RegistroClienteConfiguracionTests
{
    [Theory]
    [InlineData("", "texto")]
    [InlineData("version con\ncontrol", "texto")]
    [InlineData("test-1", "")]
    [InlineData("test-1", " ")]
    public void Documento_incompleto_no_habilita_registro(string version, string texto)
    {
        var doc = new DocumentoRegistro(version, texto);
        Assert.False(new DocumentacionRegistro(doc, doc, null).Disponible);
    }
    [Fact]
    public void Marketing_ausente_no_bloquea_documentos_obligatorios_completos()
    {
        var doc = new DocumentoRegistro("test-1", "texto exclusivo de prueba");
        var docs = new DocumentacionRegistro(doc, doc, null);
        Assert.True(docs.Disponible); Assert.Null(docs.Versiones.Promociones);
    }
    [Fact]
    public void Configuracion_vacia_registra_dependencias_y_proveedor_pendiente()
    {
        var servicios = new ServiceCollection();
        servicios.AddRegistroClientes(new ConfigurationBuilder().Build());
        using var proveedor = servicios.BuildServiceProvider();
        Assert.False(proveedor.GetRequiredService<DocumentacionRegistro>().Disponible);
        Assert.IsType<RegistroClientePendiente>(proveedor.GetRequiredService<IRepositorioRegistroCliente>());
    }
    [Fact]
    public async Task Proveedor_pendiente_no_informa_alta_exitosa()
    {
        var repo = new RegistroClientePendiente();
        // Nunca consulta ni persiste el objeto; el contrato sigue pendiente.
        var ex = await Assert.ThrowsAsync<AccessFailure>(() => repo.RegistrarAsync(null!, default));
        Assert.Equal(503, ex.Status);
    }
}

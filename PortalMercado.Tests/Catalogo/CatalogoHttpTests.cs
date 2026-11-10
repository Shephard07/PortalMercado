using System.Net;

namespace PortalMercado.Tests.Catalogo;

public sealed class CatalogoHttpTests
{
    [Theory]
    [InlineData("/api/catalogo/categorias")]
    [InlineData("/api/catalogo/unidades")]
    [InlineData("/api/catalogo/productos")]
    [InlineData("/api/catalogo/productos/1/detalle?idNegocio=1")]
    [InlineData("/api/catalogo/productos/1/imagenes?idNegocio=1")]
    public async Task Las_consultas_exigen_autenticacion(string ruta)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var respuesta = await client.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}

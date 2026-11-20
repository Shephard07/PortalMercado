using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Exploracion;

namespace PortalMercado.Tests.Clientes;

public sealed class PaginacionDetallesHttpTests
{
    [Fact]
    public async Task Enlaces_reales_siguiente_y_anterior_conservan_todos_los_filtros()
    {
        await using var f = new Factory(); using var c = f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var inicio = "/explorar/buscar?q=Frutas&mercadoId=10&categoriaId=1&tipo=productos&orden=precio-asc&tamano=1&pagina=1";
        var primera = await c.GetStringAsync(inicio); var siguiente = Enlace(primera,"Siguiente");
        Filtros(siguiente, "2"); Assert.Contains("Pera",primera);
        var segunda = await c.GetStringAsync(siguiente); Assert.Contains("Mango",segunda);
        var anterior = Enlace(segunda,"Anterior"); Filtros(anterior,"1");
        Assert.Contains("Pera",await c.GetStringAsync(anterior));
        Assert.DoesNotContain(">Siguiente</a>",segunda);
    }
    private static string Enlace(string html,string texto)
    {
        var m = Regex.Match(html,"<a[^>]*href=\"([^\"]+)\"[^>]*>"+texto+"</a>"); Assert.True(m.Success);
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static void Filtros(string url,string pagina)
    {
        var q = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"),url).Query);
        foreach (var par in new Dictionary<string,string> { ["q"]="Frutas",["mercadoId"]="10",["categoriaId"]="1",["tipo"]="productos",["orden"]="precio-asc",["tamano"]="1",["pagina"]=pagina })
            Assert.Equal(par.Value,q[par.Key].ToString());
    }
    private sealed class Factory : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development"); b.UseSetting("Clientes:Demostracion","false"); b.UseSetting("Tiendas:SqlLocal","false"); b.UseSetting("Tiendas:Demostracion","false");
            b.UseSetting("ConnectionStrings:PortalMercado","Server=127.0.0.1,1;Database=Inaccesible;Integrated Security=True;Connect Timeout=1;");
            b.ConfigureTestServices(s => s.AddSingleton<IRepositorioExploracionPublica>(new ExploracionRepoPrueba()));
        }
    }
}

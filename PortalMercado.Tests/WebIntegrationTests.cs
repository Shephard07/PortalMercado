using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PortalMercado.Web.Controllers;
using Xunit;

namespace PortalMercado.Tests;

public sealed class WebIntegrationTests
{
    private sealed class WebFactory : WebApplicationFactory<HomeController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:PortalMercado",
                "Server=127.0.0.1;Database=Web_Test;Integrated Security=True;");
        }
    }

    [Fact]
    public async Task Inicio_DevuelveHTMLYCorrelacion()
    {
        using var factory = new WebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("PortalMercado", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task VistaError_ExisteYNoExponeDetallesInternos()
    {
        using var factory = new WebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Contains(correlation, body);
        Assert.DoesNotContain("Development Mode", body);
        Assert.DoesNotContain("ConnectionStrings", body);
    }
}

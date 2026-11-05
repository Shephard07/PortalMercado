using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PortalMercado.Tests;

public sealed class ApiIntegrationTests
{
    private static HttpClient CreateClient(ApiFactory factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Health_Devuelve200YCorrelacion()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var correlationId = response.Headers
            .GetValues("X-Correlation-ID")
            .Single();

        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Healthy",
            json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task BaseNoDisponible_Devuelve503Seguro()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response =
            await client.GetAsync("/api/health/database");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            503,
            json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Excepcion_Devuelve500ConCorrelacionCoincidente()
    {
        using var factory = new ApiFactory(
            throwOnDatabaseCheck: true);

        using var client = CreateClient(factory);

        using var response =
            await client.GetAsync("/api/health/database");

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("DATO_INTERNO_FICTICIO", body);
        Assert.DoesNotContain("InvalidOperationException", body);

        using var json = JsonDocument.Parse(body);

        var correlationId = response.Headers
            .GetValues("X-Correlation-ID")
            .Single();

        Assert.Equal(
            correlationId,
            json.RootElement.GetProperty("traceId").GetString());
    }

    [Theory]
    [InlineData("/api/health/database")]
    [InlineData("/api/health/database/mapping")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Produccion_NoExponeDiagnosticosNiSwagger(
        string path)
    {
        using var factory = new ApiFactory(
            environment: "Production",
            throwOnDatabaseCheck: true);

        using var client = CreateClient(factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Development", "https://localhost:7275", true)]
    [InlineData("Development", "https://example.invalid", false)]
    [InlineData("Production", "https://localhost:7275", false)]
    public async Task Cors_AplicaOrigenSegunAmbiente(
        string environment, string origin, bool allowed)
    {
        using var factory = new ApiFactory(environment);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var present = response.Headers.TryGetValues(
            "Access-Control-Allow-Origin", out var values);
        Assert.Equal(allowed, present);
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(values!));
        }
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}

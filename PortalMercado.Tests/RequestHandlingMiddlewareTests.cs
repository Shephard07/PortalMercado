using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PortalMercado.API.Middleware;
using Xunit;

namespace PortalMercado.Tests;

public sealed class RequestHandlingMiddlewareTests
{
    [Fact]
    public async Task SolicitudCorrecta_ContinuaYAsignaCorrelacion()
    {
        var context = new DefaultHttpContext();
        var siguienteEjecutado = false;

        var middleware = new RequestHandlingMiddleware(
            next: _ =>
            {
                siguienteEjecutado = true;
                return Task.CompletedTask;
            },
            logger: NullLogger<RequestHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(siguienteEjecutado);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(context.TraceIdentifier));
    }

    [Fact]
    public async Task Excepcion_DevuelveProblemDetailsSinDatosSensibles()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        using var body = new MemoryStream();
        context.Response.Body = body;

        const string datoSensible = "PASSWORD_FICTICIO_NO_EXPONER";

        var middleware = new RequestHandlingMiddleware(
            next: _ => throw new InvalidOperationException(datoSensible),
            logger: NullLogger<RequestHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType ?? string.Empty);

        body.Position = 0;

        using var reader = new StreamReader(body);
        var json = await reader.ReadToEndAsync();

        Assert.DoesNotContain(datoSensible, json);
        Assert.DoesNotContain("InvalidOperationException", json);

        using var document = JsonDocument.Parse(json);
        var problem = document.RootElement;

        Assert.Equal(500, problem.GetProperty("status").GetInt32());

        Assert.Equal(
            context.TraceIdentifier,
            problem.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task CancelacionDelCliente_NoSeConvierteEnError500()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };

        var middleware = new RequestHandlingMiddleware(
            next: _ => throw new OperationCanceledException(
                cancellation.Token),
            logger: NullLogger<RequestHandlingMiddleware>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context));
    }
}
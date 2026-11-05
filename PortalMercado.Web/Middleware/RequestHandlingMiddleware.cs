using System.Diagnostics;

namespace PortalMercado.Web.Middleware;

public sealed class RequestHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestHandlingMiddleware> _logger;

    public RequestHandlingMiddleware(
        RequestDelegate next,
        ILogger<RequestHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente canceló la solicitud.
            throw;
        }
        catch (Exception ex)
        {
            // No registrar mensajes que puedan contener datos sensibles.
            _logger.LogError(
                "Error no controlado. Tipo: {ExceptionType}.",
                ex.GetType().Name);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            await Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error interno del servidor",
                detail: "No se pudo completar la solicitud.",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = correlationId
                })
                .ExecuteAsync(context);
        }
    }
}

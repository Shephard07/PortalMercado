using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Interfaces;

namespace PortalMercado.API.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "PortalMercado.API",
            status = "Healthy"
        });
    }

    [HttpGet("database")]
    public async Task<IActionResult> GetDatabase(
        [FromServices] IWebHostEnvironment environment,
        [FromServices] IDatabaseHealthService databaseHealth,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var connected = await databaseHealth
            .CanConnectAsync(cancellationToken);

        if (!connected)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo comprobar la conexión con la base de datos.");
        }

        return Ok(new
        {
            service = "PortalMercado.API",
            status = "Healthy",
            database = "Available"
        });
    }

    [HttpGet("database/mapping")]
    public async Task<IActionResult> GetDatabaseMapping(
    [FromServices] IWebHostEnvironment environment,
    [FromServices] IDatabaseHealthService databaseHealth,
    CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var readable = await databaseHealth
            .CanReadMercadosAsync(cancellationToken);

        if (!readable)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Verificación de mapeo no disponible",
                detail: "No se pudo completar la consulta EF de Mercado.");
        }

        return Ok(new
        {
            service = "PortalMercado.API",
            status = "Healthy",
            mapping = "Mercado",
            query = "Succeeded"
        });
    }
}


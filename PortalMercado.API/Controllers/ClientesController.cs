using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Infrastructure.Clientes;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.API.Controllers;

public sealed class ClientesApiErrores(ILogger<ClientesApiErrores> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessFailure error) return;
        logger.LogWarning("Operación API de cliente rechazada. Estado: {Estado}; correlación: {Correlacion}",
            error.Status, context.HttpContext.TraceIdentifier);
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = error.Status,
            Title = error.Status >= 500 ? "El servicio de clientes no está disponible en este momento." : error.Message,
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        }) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}

[ApiController, Authorize, TypeFilter(typeof(ClientesApiErrores))]
[ServiceFilter(typeof(ApiAntiforgeryFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(typeof(ProblemDetails), 400), ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403), ProducesResponseType(typeof(ProblemDetails), 404)]
[ProducesResponseType(typeof(ProblemDetails), 409), ProducesResponseType(typeof(ProblemDetails), 503)]
public sealed class ClientesController(RegistroClienteServicio registro, IClienteService clientes,
    IDireccionService direcciones, IConsentimientoService consentimientos,
    DocumentacionRegistro documentos, IRepositorioRegistroCliente repositorio) : ControllerBase
{
    private string Credencial => User.FindFirst(PortalAccess.SessionClaim)?.Value ?? "";
    private ClienteOperacionContexto Contexto => new(HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());

    [HttpPost("api/clientes/registro"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RegistroClienteResultado), 201)]
    public async Task<IActionResult> Registrar(RegistroClienteSolicitud datos, CancellationToken ct)
    {
        if (!documentos.Disponible || repositorio is RegistroClientePendiente)
            throw new AccessFailure(503, "El registro no está disponible en este momento.");
        var resultado = await registro.RegistrarAsync(datos, Contexto, ct);
        // Sin sesión automática, datos de identidad ni secretos en la respuesta.
        return StatusCode(201, resultado);
    }

    [HttpGet("api/mi-perfil"), ProducesResponseType(typeof(ClientePerfil), 200)]
    public Task<ClientePerfil> Perfil(CancellationToken ct) => clientes.ObtenerAsync(Credencial, "API", ct);

    [HttpPut("api/mi-perfil"), ProducesResponseType(204)]
    public async Task<IActionResult> GuardarPerfil(ClientePerfilDatos datos, CancellationToken ct)
    {
        await clientes.ActualizarAsync(Credencial, "API", datos, Contexto, ct);
        return NoContent();
    }

    [HttpGet("api/mis-direcciones"), ProducesResponseType(typeof(ClienteDireccion[]), 200)]
    public Task<ClienteDireccion[]> Direcciones(CancellationToken ct) => direcciones.ListarAsync(Credencial, "API", ct);

    [HttpGet("api/mis-direcciones/{id:long}"), ProducesResponseType(typeof(ClienteDireccion), 200)]
    public async Task<ClienteDireccion> Direccion(long id, CancellationToken ct)
    {
        if (id <= 0) throw new AccessFailure(400, "Revisa la dirección seleccionada.");
        return (await direcciones.ListarAsync(Credencial, "API", ct)).SingleOrDefault(x => x.IdDireccion == id)
            ?? throw new AccessFailure(404, "La dirección no está disponible.");
    }

    [HttpPost("api/mis-direcciones"), ProducesResponseType(typeof(ClienteDireccion), 201)]
    public async Task<IActionResult> CrearDireccion(ClienteDireccionDatos datos, CancellationToken ct)
    {
        var creada = await direcciones.CrearAsync(Credencial, "API", datos, Contexto, ct);
        return CreatedAtAction(nameof(Direccion), new { id = creada.IdDireccion }, creada);
    }

    [HttpPut("api/mis-direcciones/{id:long}"), ProducesResponseType(204)]
    public async Task<IActionResult> GuardarDireccion(long id, ClienteDireccionDatos datos, CancellationToken ct)
    {
        await direcciones.ActualizarAsync(Credencial, "API", id, datos, Contexto, ct);
        return NoContent();
    }

    [HttpDelete("api/mis-direcciones/{id:long}"), ProducesResponseType(204)]
    public async Task<IActionResult> EliminarDireccion(long id, CancellationToken ct)
    {
        await direcciones.EliminarAsync(Credencial, "API", id, Contexto, ct);
        return NoContent();
    }

    [HttpPut("api/mis-direcciones/{id:long}/principal"), ProducesResponseType(204)]
    public async Task<IActionResult> Principal(long id, CancellationToken ct)
    {
        await direcciones.MarcarPrincipalAsync(Credencial, "API", id, Contexto, ct);
        return NoContent();
    }

    [HttpGet("api/mis-consentimientos"), ProducesResponseType(typeof(ClienteConsentimiento[]), 200)]
    public Task<ClienteConsentimiento[]> Consentimientos(CancellationToken ct) => consentimientos.ListarAsync(Credencial, "API", ct);

    [HttpPost("api/mis-consentimientos"), ProducesResponseType(204)]
    public async Task<IActionResult> RegistrarConsentimiento(ClienteConsentimientoDatos datos, CancellationToken ct)
    {
        if (datos.Aceptado && !documentos.Disponible)
            throw new AccessFailure(503, "Las condiciones no están disponibles en este momento.");
        await consentimientos.RegistrarAsync(Credencial, "API", datos, Contexto, ct);
        return NoContent();
    }
}

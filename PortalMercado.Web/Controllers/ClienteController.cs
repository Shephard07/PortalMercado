using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

public sealed class ClienteErrores(ILogger<ClienteErrores> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessFailure error) return;
        logger.LogWarning("Operación privada de cliente rechazada. Estado: {Estado}; correlación: {Correlacion}",
            error.Status, context.HttpContext.TraceIdentifier);
        context.Result = error.Status == 401 ? new ChallengeResult() : new ViewResult
        {
            ViewName = "~/Views/Cliente/Error.cshtml", StatusCode = error.Status,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            { Model = error.Status }
        };
        context.ExceptionHandled = true;
    }
}

[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(ClienteErrores))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClienteController(IClienteService clientes, IDireccionService direcciones) : Controller
{
    private string Credencial => User.FindFirst(PortalAccess.SessionClaim)?.Value ?? "";
    private ClienteOperacionContexto Contexto => new(HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());

    [HttpGet]
    public async Task<IActionResult> Perfil(CancellationToken ct) => View(await clientes.ObtenerAsync(Credencial, "MVC", ct));

    [HttpPost]
    public async Task<IActionResult> Perfil([Bind(Prefix = "Datos")] ClientePerfilDatos datos, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await clientes.ActualizarAsync(Credencial, "MVC", datos, Contexto, ct);
                TempData["ClienteMensaje"] = "Perfil actualizado.";
                return RedirectToAction(nameof(Perfil));
            }
            catch (AccessFailure error) when (error.Status is 400 or 409)
            { ModelState.AddModelError("", error.Message); Response.StatusCode = error.Status; }
        }
        else Response.StatusCode = 400;
        return View((await clientes.ObtenerAsync(Credencial, "MVC", ct)) with { Datos = datos });
    }

    [HttpGet]
    public async Task<IActionResult> Direcciones(CancellationToken ct) =>
        View(await direcciones.ListarAsync(Credencial, "MVC", ct));

    [HttpGet]
    public async Task<IActionResult> Nueva(CancellationToken ct)
    {
        await clientes.ObtenerAsync(Credencial, "MVC", ct);
        return Formulario(null, new());
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken ct) =>
        Formulario(id, (await PropiaAsync(id, ct)).Datos);

    [HttpPost]
    public async Task<IActionResult> Guardar(long? id, ClienteDireccionDatos datos, CancellationToken ct)
    {
        // Resolver propiedad también cuando el formulario sea inválido.
        if (id.HasValue) await PropiaAsync(id.Value, ct);
        else await clientes.ObtenerAsync(Credencial, "MVC", ct);
        if (ModelState.IsValid)
        {
            try
            {
                if (id.HasValue) await direcciones.ActualizarAsync(Credencial, "MVC", id.Value, datos, Contexto, ct);
                else await direcciones.CrearAsync(Credencial, "MVC", datos, Contexto, ct);
                TempData["ClienteMensaje"] = "Dirección guardada.";
                return RedirectToAction(nameof(Direcciones));
            }
            catch (AccessFailure error) when (error.Status is 400 or 409)
            { ModelState.AddModelError("", error.Message); Response.StatusCode = error.Status; }
        }
        else Response.StatusCode = 400;
        return Formulario(id, datos);
    }

    [HttpGet]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct) => View(await PropiaAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> ConfirmarEliminar(long id, CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la dirección seleccionada.");
        await direcciones.EliminarAsync(Credencial, "MVC", id, Contexto, ct);
        TempData["ClienteMensaje"] = "Dirección eliminada.";
        return RedirectToAction(nameof(Direcciones));
    }

    [HttpPost]
    public async Task<IActionResult> Principal(long id, CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la dirección seleccionada.");
        await direcciones.MarcarPrincipalAsync(Credencial, "MVC", id, Contexto, ct);
        TempData["ClienteMensaje"] = "Dirección principal actualizada.";
        return RedirectToAction(nameof(Direcciones));
    }

    private async Task<ClienteDireccion> PropiaAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new AccessFailure(400, "Revisa la dirección seleccionada.");
        return (await direcciones.ListarAsync(Credencial, "MVC", ct)).SingleOrDefault(x => x.IdDireccion == id)
            ?? throw new AccessFailure(404, "La dirección no está disponible.");
    }
    private IActionResult Formulario(long? id, ClienteDireccionDatos datos)
    { ViewData["IdDireccion"] = id; return View("FormularioDireccion", datos); }
}

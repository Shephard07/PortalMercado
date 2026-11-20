using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Infrastructure.Clientes;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

public sealed record ConsentimientosModelo(ClienteConsentimiento[] Historial, DocumentoRegistro? Promociones,
    string? VersionRetiro);

[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(ClienteErrores))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConsentimientosController(IConsentimientoService servicio, DocumentacionRegistro documentos) : Controller
{
    private string Credencial => User.FindFirst(PortalAccess.SessionClaim)?.Value ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await ModeloAsync(ct));

    [HttpPost]
    public Task<IActionResult> Autorizar(string version, CancellationToken ct) => CambiarAsync(version, true, ct);

    [HttpPost]
    public Task<IActionResult> Retirar(string version, CancellationToken ct) => CambiarAsync(version, false, ct);

    private async Task<IActionResult> CambiarAsync(string version, bool aceptado, CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la versión del documento.");
        if (aceptado && !documentos.Disponible) throw new AccessFailure(503, "Las condiciones no están disponibles.");
        try
        {
            await servicio.RegistrarAsync(Credencial, "MVC", new()
            { Tipo = "PROMOCIONES", VersionDocumento = version, Aceptado = aceptado },
                new(HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
            TempData["ConsentimientoMensaje"] = aceptado ? "Autorización de promociones registrada." : "Retiro de autorización registrado.";
            return RedirectToAction(nameof(Index));
        }
        catch (AccessFailure ex) when (ex.Status is 400 or 409)
        {
            Response.StatusCode = ex.Status;
            ModelState.AddModelError("", ex.Status == 409 ? "La información cambió. Recarga antes de volver a intentarlo." : ex.Message);
            return View("Index", await ModeloAsync(ct));
        }
    }
    private async Task<ConsentimientosModelo> ModeloAsync(CancellationToken ct)
    {
        var historial = await servicio.ListarAsync(Credencial, "MVC", ct);
        var ultimo = historial.FirstOrDefault(x => x.Tipo == "PROMOCIONES");
        return new(historial, documentos.Disponible ? documentos.Promociones : null,
            ultimo?.Aceptado == true ? ultimo.VersionDocumento : null);
    }
}

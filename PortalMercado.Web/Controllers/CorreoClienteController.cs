using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

[Authorize, AutoValidateAntiforgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorreoClienteController(ICorreoClienteService correos) : Controller
{
    private string Credencial => User.FindFirst(PortalAccess.SessionClaim)?.Value ?? "";
    [HttpGet("Cuenta/CambiarCorreo")]
    public IActionResult Cambiar() => View(new CambioCorreoClienteSolicitud());
    [HttpPost("Cuenta/CambiarCorreo"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Cambiar(CambioCorreoClienteSolicitud solicitud, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await correos.SolicitarAsync(Credencial, "MVC", solicitud, HttpContext.TraceIdentifier, ct);
                TempData["CorreoSolicitado"] = true;
                return RedirectToAction(nameof(Solicitud));
            }
            catch (PasswordFailure e) { Response.StatusCode = e.Status; ModelState.AddModelError("", e.Message); }
        }
        ModelState.Remove(nameof(solicitud.PasswordActual)); solicitud.PasswordActual = "";
        return View(solicitud);
    }
    [HttpGet("Cuenta/CambioCorreoSolicitado")]
    public IActionResult Solicitud()
    {
        if (TempData["CorreoSolicitado"] is not true) return RedirectToAction(nameof(Cambiar));
        return View();
    }
    [HttpGet("Cuenta/ConfirmarCorreo")]
    public IActionResult Confirmar(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!MfaCryptography.IsCredential(token)) { Response.StatusCode = 400; return View("Error"); }
        // GET no consume el enlace ni cambia la identidad.
        return View(new ConfirmacionCorreoClienteSolicitud { Token = token! });
    }
    [HttpPost("Cuenta/ConfirmarCorreo"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Confirmar(ConfirmacionCorreoClienteSolicitud solicitud, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) { Response.StatusCode = 400; return View("Error"); }
        try
        {
            await correos.ConfirmarAsync(Credencial, "MVC", solicitud, HttpContext.TraceIdentifier, ct);
            await HttpContext.SignOutAsync(PortalAccess.Scheme);
            return View("Actualizado");
        }
        catch (PasswordFailure e) { Response.StatusCode = e.Status; return View("Error"); }
    }
}

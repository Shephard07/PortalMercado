using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Web.Controllers;

[AllowAnonymous, AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RegistroController(RegistroClienteServicio servicio, DocumentacionRegistro documentos,
    IRepositorioRegistroCliente repositorio, ILogger<RegistroController> logger) : Controller
{
    private bool Disponible => documentos.Disponible && repositorio is not RegistroClientePendiente;

    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        Preparar(returnUrl);
        if (!Disponible) Response.StatusCode = 503;
        return View(new RegistroClienteSolicitud
        {
            VersionTerminos = documentos.Terminos.Version, VersionPrivacidad = documentos.Privacidad.Version,
            VersionPromociones = documentos.Promociones?.Version
        });
    }

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Index(RegistroClienteSolicitud solicitud, string? returnUrl, CancellationToken ct)
    {
        Preparar(returnUrl);
        if (!Disponible) return Fallo(solicitud, 503, "El registro no está disponible en este momento.");
        if (!ModelState.IsValid) return Fallo(solicitud, 400, "Revisa los campos del formulario.");
        try
        {
            var resultado = await servicio.RegistrarAsync(solicitud,
                new(HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
            // No poner correo ni datos personales en URL, TempData, cookies o logs.
            logger.LogInformation("Registro de cliente completado. Correlación: {Correlacion}", HttpContext.TraceIdentifier);
            TempData["RegistroCompletado"] = resultado.RequiereConfirmacionCorreo ? "confirmar" : "creado";
            return RedirectToAction(nameof(Completado), new {
                returnUrl = (string)ViewData["ReturnUrl"]! });
        }
        catch (AccessFailure ex)
        {
            logger.LogWarning("Registro de cliente rechazado. Estado: {Estado}; correlación: {Correlacion}", ex.Status, HttpContext.TraceIdentifier);
            return Fallo(solicitud, ex.Status, ex.Status >= 500
                ? "El registro no está disponible en este momento. Inténtalo más tarde." : ex.Message);
        }
    }

    [HttpGet]
    public IActionResult Completado(string? returnUrl = null)
    {
        var estado = TempData["RegistroCompletado"] as string;
        if (estado is not ("creado" or "confirmar")) return RedirectToAction(nameof(Index));
        Preparar(returnUrl);
        ViewData["Confirmacion"] = estado == "confirmar";
        return View();
    }

    [HttpGet]
    public IActionResult Documento(string tipo)
    {
        var documento = tipo switch { "terminos" => documentos.Terminos, "privacidad" => documentos.Privacidad,
            "promociones" => documentos.Promociones, _ => null };
        if (!documentos.Disponible || documento is null) return NotFound();
        ViewData["Title"] = tipo switch { "terminos" => "Términos y condiciones", "privacidad" => "Privacidad", _ => "Promociones" };
        return View(documento);
    }

    private void Preparar(string? destino)
    {
        ViewData["Documentos"] = documentos;
        ViewData["Disponible"] = Disponible;
        ViewData["ReturnUrl"] = DestinoCliente.Normalizar(destino ?? "/explorar");
    }
    private IActionResult Fallo(RegistroClienteSolicitud solicitud, int estado, string mensaje)
    {
        Response.StatusCode = estado;
        ModelState.Remove(nameof(solicitud.Password)); ModelState.Remove(nameof(solicitud.ConfirmarPassword));
        solicitud.Password = solicitud.ConfirmarPassword = "";
        ModelState.AddModelError("", mensaje);
        return View("Index", solicitud);
    }
}

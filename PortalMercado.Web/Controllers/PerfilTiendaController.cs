using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PerfilTiendaController(PerfilTiendaServicio perfiles, ILogger<PerfilTiendaController> logger) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Credencial => User.FindFirstValue(PortalAccess.SessionClaim)!;
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet("mis-tiendas")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await perfiles.NegociosAsync(Actor, Credencial, "MVC", ct));

    [HttpGet("mis-tiendas/{idNegocio:long}/perfil")]
    public async Task<IActionResult> Editar(long idNegocio, CancellationToken ct) =>
        View(await perfiles.ObtenerAsync(Actor, Credencial, "MVC", idNegocio, ct));

    [HttpPost("mis-tiendas/{idNegocio:long}/perfil")]
    public async Task<IActionResult> Editar(long idNegocio,
        [Bind("Version,Descripcion,LogoUrl,Horario,Telefono,Correo")] PerfilTiendaDatos datos, CancellationToken ct)
    {
        // Autorizar también al reconstruir una vista con errores de validación.
        var perfil = await perfiles.ObtenerAsync(Actor, Credencial, "MVC", idNegocio, ct);
        if (ModelState.IsValid)
        {
            try
            {
                await perfiles.GuardarAsync(Actor, Credencial, "MVC", idNegocio, datos, Correlacion, ct);
                logger.LogInformation("TIENDA_PERFIL_ACTUALIZADO actor={Actor} negocio={Negocio} correlacion={Correlacion}",
                    Actor, idNegocio, Correlacion);
                TempData["PerfilGuardado"] = "Perfil comercial actualizado.";
                return RedirectToAction(nameof(Editar), new { idNegocio });
            }
            catch (AccessFailure error) when (error.Status is 400 or 409)
            {
                logger.LogWarning("TIENDA_PERFIL_RECHAZADO estado={Estado} negocio={Negocio} correlacion={Correlacion}",
                    error.Status, idNegocio, Correlacion);
                Response.StatusCode = error.Status;
                ModelState.AddModelError(string.Empty, error.Message);
            }
        }
        else Response.StatusCode = 400;
        return View(perfil with { Datos = datos });
    }
}

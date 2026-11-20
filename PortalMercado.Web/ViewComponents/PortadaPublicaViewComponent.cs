using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
using PortalMercado.Web.Models;
namespace PortalMercado.Web.ViewComponents;

public sealed class PortadaPublicaViewComponent(ExploracionPublicaServicio exploracion,
    ILogger<PortadaPublicaViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try { return View(new PortadaPublicaModelo(await exploracion.PortadaAsync(HttpContext.RequestAborted))); }
        catch (AccessFailure error) when (error.Status >= 500)
        {
            logger.LogWarning("PORTADA_NO_DISPONIBLE estado={Estado} correlacion={Correlacion}", error.Status, HttpContext.TraceIdentifier);
            // La portada sigue siendo una página de navegación; no presenta datos ficticios como reales.
            return View(new PortadaPublicaModelo(null, true));
        }
    }
}

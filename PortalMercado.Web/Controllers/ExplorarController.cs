using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
using PortalMercado.Web.Models;
namespace PortalMercado.Web.Controllers;

[AllowAnonymous, Route("explorar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExplorarController(ExploracionPublicaServicio exploracion, ILogger<ExplorarController> logger) : Controller
{
    [HttpGet("")]
    public IActionResult Inicio() => View();

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar(string? q, long? mercadoId, long? categoriaId,
        string tipo = "todos", string orden = "nombre", int pagina = 1, int tamano = 12, CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa los parámetros de búsqueda.");
            return View(await exploracion.BuscarAsync(new(q, mercadoId, categoriaId, tipo, orden, pagina, tamano), ct));
        }
        catch (AccessFailure error)
        {
            if (error.Status >= 500) logger.LogWarning("BUSQUEDA_NO_DISPONIBLE estado={Estado} correlacion={Correlacion}", error.Status, HttpContext.TraceIdentifier);
            Response.StatusCode = error.Status;
            return View("Error", new TiendasErrorModelo(error.Status, error.Message));
        }
    }
}

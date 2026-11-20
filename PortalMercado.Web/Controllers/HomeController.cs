using Microsoft.AspNetCore.Mvc;
using PortalMercado.Web.Models;

namespace PortalMercado.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index(string? q)
    {
        var buscar = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (buscar?.Length > 80)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            ModelState.AddModelError("q", "La búsqueda admite hasta 80 caracteres.");
        }
        return View(new PanelInicioModelo(MenuPrincipal.Crear(User), buscar));
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}

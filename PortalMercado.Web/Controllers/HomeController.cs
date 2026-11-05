using Microsoft.AspNetCore.Mvc;
using PortalMercado.Web.Models;

namespace PortalMercado.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index([FromServices] IWebHostEnvironment environment)
    {
        return View(new TechnicalHomeViewModel
        {
            Version = typeof(HomeController).Assembly.GetName().Version?.ToString()
                ?? "No disponible",
            EnvironmentName = environment.IsDevelopment() ? "Desarrollo" : "Producción"
        });
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}

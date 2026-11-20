using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Web.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ComprasController : Controller
{
    // Punto de extensión informativo. No crea carrito, reserva stock ni registra pedidos.
    [HttpGet]
    public IActionResult Preparar(string? returnUrl = null)
    {
        ViewData["Destino"] = DestinoCliente.Normalizar(returnUrl ?? "/explorar");
        return View();
    }
}

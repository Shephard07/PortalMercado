using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;

namespace PortalMercado.Web.Controllers;

[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CatalogoController(CategoriaServicio categorias, UnidadMedidaServicio unidades) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, bool? activa, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los filtros del catálogo.");

        ViewBag.Buscar = buscar;
        ViewBag.Activa = activa;
        return View(await categorias.ListarAsync(Actor, new(buscar, activa, pagina), ct));
    }

    [HttpGet]
    public async Task<IActionResult> Unidades(CancellationToken ct) => View(await unidades.ListarAsync(Actor, ct));

    [HttpPost]
    public async Task<IActionResult> Crear(CategoriaDatos datos, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los datos de la categoría.");

        await categorias.CrearAsync(Actor, datos, Correlacion, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(long id, CategoriaDatos datos, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los datos de la categoría.");

        await categorias.EditarAsync(Actor, id, datos, Correlacion, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Estado(long id, bool? activa, CancellationToken ct)
    {
        if (!ModelState.IsValid || activa is not bool estado)
            throw new AccessFailure(400, "Indica si la categoría queda activa.");

        await categorias.CambiarEstadoAsync(Actor, id, estado, Correlacion, ct);
        return RedirectToAction(nameof(Index));
    }
}

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProductosController(ProductoServicio productos) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Credencial => User.FindFirstValue(PortalAccess.SessionClaim)!;
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(long? idNegocio, long? idCategoria, string? estado, string? buscar,
        int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los filtros de productos.");

        var filtro = new ProductoFiltro(idNegocio, idCategoria, estado, buscar, pagina);
        ViewBag.Filtro = filtro;
        return View(await productos.ListarAsync(Actor, Credencial, "MVC", filtro, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(long idNegocio, long id, int pagina = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa el producto y la página del historial.");

        return View(await productos.DetalleAsync(Actor, Credencial, "MVC", idNegocio, id, pagina, ct));
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(long idNegocio, long id, ProductoEstadoDatos datos, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los datos del cambio de estado.");

        try
        {
            await productos.CambiarEstadoAsync(Actor, Credencial, "MVC", idNegocio, id, datos, Correlacion, ct);
        }
        catch (AccessFailure error) when (error.Status is 400 or 409)
        {
            TempData["ErrorEstadoProducto"] = error.Message;
        }

        return RedirectToAction(nameof(Detalle), new { idNegocio, id });
    }

    [HttpGet]
    public async Task<IActionResult> Imagenes(long idNegocio, long id, CancellationToken ct) =>
        View(await productos.ImagenesAsync(Actor, Credencial, "MVC", idNegocio, id, ct));

    [HttpPost]
    public async Task<IActionResult> GuardarImagen(long idNegocio, long id, long? idImagen, ImagenProductoDatos datos,
        CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await productos.GuardarImagenAsync(Actor, Credencial, "MVC", idNegocio, id, idImagen, datos, Correlacion, ct);
                return RedirectToAction(nameof(Imagenes), new { idNegocio, id });
            }
            catch (AccessFailure error) when (error.Status is 400 or 409)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }
        }

        return View("Imagenes", await productos.ImagenesAsync(Actor, Credencial, "MVC", idNegocio, id, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Crear(long idNegocio, CancellationToken ct)
    {
        var negocio = await productos.NegocioParaEdicionAsync(Actor, Credencial, "MVC", idNegocio, ct);
        return Formulario(negocio, null, new ProductoDatos());
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long idNegocio, long id, CancellationToken ct)
    {
        var negocio = await productos.NegocioParaEdicionAsync(Actor, Credencial, "MVC", idNegocio, ct);
        var ficha = await productos.ObtenerAsync(Actor, Credencial, "MVC", idNegocio, id, ct);
        return Formulario(negocio, id, ficha.Datos);
    }

    [HttpPost]
    public Task<IActionResult> Crear(long idNegocio, ProductoDatos datos, CancellationToken ct) =>
        Guardar(idNegocio, null, datos, ct);

    [HttpPost]
    public Task<IActionResult> Editar(long idNegocio, long id, ProductoDatos datos, CancellationToken ct) =>
        Guardar(idNegocio, id, datos, ct);

    private async Task<IActionResult> Guardar(long idNegocio, long? id, ProductoDatos datos, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await productos.GuardarAsync(Actor, Credencial, "MVC", idNegocio, id, datos, Correlacion, ct);
                return RedirectToAction(nameof(Index), new { idNegocio });
            }
            catch (AccessFailure error) when (error.Status is 400 or 409)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }
        }

        var negocio = await productos.NegocioParaEdicionAsync(Actor, Credencial, "MVC", idNegocio, ct);
        return Formulario(negocio, id, datos);
    }

    private ViewResult Formulario(NamedId negocio, long? id, ProductoDatos datos)
    {
        ViewBag.Negocio = negocio;
        ViewBag.IdProducto = id;
        return View("Formulario", datos);
    }
}

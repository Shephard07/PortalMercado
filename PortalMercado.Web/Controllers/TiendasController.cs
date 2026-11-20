using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Web.Models;

namespace PortalMercado.Web.Controllers;

[AllowAnonymous, Route("tiendas"), TypeFilter(typeof(TiendasErrores))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TiendasController(ITiendaService tiendas, CatalogoTiendaServicio catalogo) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Directorio(long? mercadoId, long? categoriaId, string? q,
        int pagina = 1, int tamano = 12, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los filtros y la página del directorio.");

        var filtro = new TiendaFiltro(mercadoId, categoriaId, q, pagina, tamano);
        var resultado = await tiendas.ListarAsync(filtro, ct);
        var opciones = await tiendas.OpcionesAsync(ct);
        return View(new TiendasDirectorioModelo(filtro, resultado, opciones));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detalle(string slug, long? categoriaId, string? q,
        string orden = "nombre", int pagina = 1, int tamano = 12, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            throw new AccessFailure(400, "Revisa los filtros y el orden del catálogo.");
        return View(await catalogo.ConsultarAsync(slug, new(categoriaId, q, orden, pagina, tamano), ct));
    }
}

public sealed class TiendasErrores(ILogger<TiendasErrores> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessFailure error)
            return;

        if (error.Status >= 500)
            logger.LogWarning("Consulta pública de tiendas no disponible. Estado {Estado}. Correlación {Correlacion}.",
                error.Status, context.HttpContext.TraceIdentifier);

        context.Result = new ViewResult
        {
            ViewName = "~/Views/Tiendas/Error.cshtml",
            StatusCode = error.Status,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                Model = new TiendasErrorModelo(error.Status, error.Message)
            }
        };
        context.ExceptionHandled = true;
    }
}

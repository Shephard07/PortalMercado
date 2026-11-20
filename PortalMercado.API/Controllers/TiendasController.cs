using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.API.Controllers;

[ApiController, AllowAnonymous, Route("api/tiendas"), TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TiendasController(ITiendaService tiendas, CatalogoTiendaServicio catalogo) : ControllerBase
{
    [HttpGet]
    public Task<Page<TiendaResumen>> Directorio(long? mercadoId, long? categoriaId, string? q,
        int pagina = 1, int tamano = 12, CancellationToken ct = default) =>
        tiendas.ListarAsync(new(mercadoId, categoriaId, q, pagina, tamano), ct);

    [HttpGet("{slug}")]
    public Task<TiendaPublica> Detalle(string slug, CancellationToken ct) =>
        tiendas.ObtenerAsync(slug, ct);

    [HttpGet("{slug}/productos")]
    public Task<CatalogoTiendaPublico> Productos(string slug, long? categoriaId, string? q,
        string orden = "nombre", int pagina = 1, int tamano = 12, CancellationToken ct = default) =>
        catalogo.ConsultarAsync(slug, new(categoriaId, q, orden, pagina, tamano), ct);
}

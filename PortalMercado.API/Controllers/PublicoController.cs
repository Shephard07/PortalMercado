using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
namespace PortalMercado.API.Controllers;

[ApiController, AllowAnonymous, Route("api/publico"), TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicoController(ExploracionPublicaServicio exploracion) : ControllerBase
{
    [HttpGet("mercados")]
    public async Task<NamedId[]> Mercados(CancellationToken ct) => (await exploracion.PortadaAsync(ct)).Mercados;

    [HttpGet("buscar")]
    public Task<ExploracionPagina> Buscar(string? q, long? mercadoId, long? categoriaId,
        string tipo = "todos", string orden = "nombre", int pagina = 1, int tamano = 12, CancellationToken ct = default) =>
        exploracion.BuscarAsync(new(q, mercadoId, categoriaId, tipo, orden, pagina, tamano), ct);
}

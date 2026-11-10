using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;

namespace PortalMercado.API.Controllers;

[ApiController, Authorize, Route("api/catalogo")]
[ServiceFilter(typeof(ApiAntiforgeryFilter)), TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CatalogoController(CategoriaServicio categorias, UnidadMedidaServicio unidades) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet("categorias")]
    public Task<Page<CategoriaDto>> ListarCategorias(string? buscar, bool? activa, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        categorias.ListarAsync(Actor, new(buscar, activa, pagina, tamano), ct);

    [HttpPost("categorias"), ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CrearCategoria(CategoriaDatos datos, CancellationToken ct)
    {
        var categoria = await categorias.CrearAsync(Actor, datos, Correlacion, ct);
        return StatusCode(StatusCodes.Status201Created, categoria);
    }

    [HttpPut("categorias/{id:long}")]
    public Task<CategoriaDto> EditarCategoria(long id, CategoriaDatos datos, CancellationToken ct) =>
        categorias.EditarAsync(Actor, id, datos, Correlacion, ct);

    [HttpPatch("categorias/{id:long}/estado"), ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CambiarEstado(long id, CategoriaEstadoRequest datos, CancellationToken ct)
    {
        if (datos.Activa is not bool activa)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Indica si la categoría queda activa." });

        await categorias.CambiarEstadoAsync(Actor, id, activa, Correlacion, ct);
        return NoContent();
    }

    [HttpGet("unidades")]
    public Task<UnidadMedidaDto[]> ListarUnidades(CancellationToken ct) => unidades.ListarAsync(Actor, ct);
}

public sealed class CategoriaEstadoRequest
{
    [Required]
    public bool? Activa { get; init; }
}

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Catalogo;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.API.Controllers;

[ApiController, Authorize, Route("api/catalogo/productos"), TypeFilter(typeof(AdministrationErrors))]
[ServiceFilter(typeof(ApiAntiforgeryFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProductosController(ProductoServicio productos) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Credencial => User.FindFirstValue(PortalAccess.SessionClaim)!;
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet]
    public Task<ProductosConsulta> Listar(long? idNegocio, long? idCategoria, string? estado, string? buscar,
        int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        productos.ListarAsync(Actor, Credencial, "API", new(idNegocio, idCategoria, estado, buscar, pagina, tamano), ct);

    [HttpGet("{id:long}")]
    public Task<ProductoFicha> Obtener(long id, [FromQuery] long idNegocio, CancellationToken ct) =>
        productos.ObtenerAsync(Actor, Credencial, "API", idNegocio, id, ct);

    [HttpGet("{id:long}/detalle")]
    public Task<ProductoDetalle> Detalle(long id, [FromQuery] long idNegocio, int pagina = 1,
        CancellationToken ct = default) =>
        productos.DetalleAsync(Actor, Credencial, "API", idNegocio, id, pagina, ct);

    [HttpPatch("{id:long}/estado")]
    public async Task<IActionResult> CambiarEstado(long id, [FromQuery] long idNegocio, ProductoEstadoDatos datos,
        CancellationToken ct)
    {
        await productos.CambiarEstadoAsync(Actor, Credencial, "API", idNegocio, id, datos, Correlacion, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/imagenes")]
    public Task<ImagenesProducto> Imagenes(long id, [FromQuery] long idNegocio, CancellationToken ct) =>
        productos.ImagenesAsync(Actor, Credencial, "API", idNegocio, id, ct);

    [HttpPost("{id:long}/imagenes")]
    public async Task<IActionResult> CrearImagen(long id, [FromQuery] long idNegocio, ImagenProductoDatos datos,
        CancellationToken ct)
    {
        var idImagen = await productos.GuardarImagenAsync(Actor, Credencial, "API", idNegocio, id, null, datos, Correlacion, ct);
        return StatusCode(StatusCodes.Status201Created, new { idImagen });
    }

    [HttpPut("{id:long}/imagenes/{idImagen:long}")]
    public async Task<IActionResult> EditarImagen(long id, long idImagen, [FromQuery] long idNegocio, ImagenProductoDatos datos,
        CancellationToken ct)
    {
        await productos.GuardarImagenAsync(Actor, Credencial, "API", idNegocio, id, idImagen, datos, Correlacion, ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromQuery] long idNegocio, ProductoDatos datos, CancellationToken ct)
    {
        var id = await productos.GuardarAsync(Actor, Credencial, "API", idNegocio, null, datos, Correlacion, ct);
        return CreatedAtAction(nameof(Obtener), new { id, idNegocio }, new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Editar(long id, [FromQuery] long idNegocio, ProductoDatos datos, CancellationToken ct)
    {
        await productos.GuardarAsync(Actor, Credencial, "API", idNegocio, id, datos, Correlacion, ct);
        return NoContent();
    }
}

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.API.Controllers;

[ApiController, Authorize, Route("api/mis-tiendas/{idNegocio:long}/perfil"), TypeFilter(typeof(AdministrationErrors))]
[ServiceFilter(typeof(ApiAntiforgeryFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PerfilTiendaController(PerfilTiendaServicio perfiles, ILogger<PerfilTiendaController> logger) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Credencial => User.FindFirstValue(PortalAccess.SessionClaim)!;
    private string Correlacion => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    [HttpGet]
    public Task<PerfilTienda> Obtener(long idNegocio, CancellationToken ct) =>
        perfiles.ObtenerAsync(Actor, Credencial, "API", idNegocio, ct);

    [HttpPut]
    public async Task<IActionResult> Guardar(long idNegocio, PerfilTiendaDatos datos, CancellationToken ct)
    {
        try
        {
            await perfiles.GuardarAsync(Actor, Credencial, "API", idNegocio, datos, Correlacion, ct);
            logger.LogInformation("TIENDA_PERFIL_ACTUALIZADO actor={Actor} negocio={Negocio} correlacion={Correlacion}",
                Actor, idNegocio, Correlacion);
            return NoContent();
        }
        catch (AccessFailure error)
        {
            logger.LogWarning("TIENDA_PERFIL_RECHAZADO estado={Estado} negocio={Negocio} correlacion={Correlacion}",
                error.Status, idNegocio, Correlacion);
            throw;
        }
    }
}

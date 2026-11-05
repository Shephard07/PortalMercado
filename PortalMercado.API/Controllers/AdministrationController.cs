using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PortalMercado.Application.Administration;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.API.Controllers;

public sealed class AdministrationErrors : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessFailure error) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = error.Status, Title = error.Message,
            Extensions = { ["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier } }) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}
[ApiController, Authorize, ServiceFilter(typeof(ApiAntiforgeryFilter)), TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(typeof(ProblemDetails), 400), ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403), ProducesResponseType(typeof(ProblemDetails), 409)]
public sealed class AdministrationController(IUserAdministration admin) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Correlation => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
    [HttpGet("api/usuarios"), ProducesResponseType(typeof(Page<UserDto>), 200)]
    public async Task<IActionResult> List(int pagina = 1, int tamano = 20, CancellationToken ct = default) => Ok(await admin.ListAsync(Actor, pagina, tamano, ct));
    [HttpGet("api/usuarios/{id:long}"), ProducesResponseType(typeof(UserDto), 200)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await admin.GetAsync(Actor, id, ct));
    [HttpPost("api/usuarios"), ProducesResponseType(typeof(UserDto), 201)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await admin.CreateAsync(Actor, request, Correlation, ct);
        return Created($"/api/usuarios/{result.IdUsuario}", result);
    }
    [HttpPut("api/usuarios/{id:long}"), ProducesResponseType(204)]
    public async Task<IActionResult> Edit(long id, EditUserRequest request, CancellationToken ct) { await admin.EditAsync(Actor, id, request, Correlation, ct); return NoContent(); }
    [HttpPatch("api/usuarios/{id:long}/estado"), ProducesResponseType(204)]
    public async Task<IActionResult> State(long id, StateRequest request, CancellationToken ct) { await admin.StateAsync(Actor, id, request.Activo, Correlation, ct); return NoContent(); }
    [HttpPut("api/usuarios/{id:long}/roles"), ProducesResponseType(204)]
    public async Task<IActionResult> Roles(long id, RoleAssignment request, CancellationToken ct) { await admin.RolesAsync(Actor, id, request.Ids, Correlation, ct); return NoContent(); }
    [HttpPut("api/usuarios/{id:long}/negocios"), ProducesResponseType(204)]
    public async Task<IActionResult> Businesses(long id, ScopeAssignment request, CancellationToken ct) { await admin.BusinessesAsync(Actor, id, request.Ids, Correlation, ct); return NoContent(); }
    [HttpPut("api/usuarios/{id:long}/mercados"), ProducesResponseType(204)]
    public async Task<IActionResult> Markets(long id, ScopeAssignment request, CancellationToken ct) { await admin.MarketsAsync(Actor, id, request.Ids, Correlation, ct); return NoContent(); }
    [HttpGet("api/roles"), ProducesResponseType(typeof(RoleDto[]), 200)]
    public async Task<IActionResult> Roles(CancellationToken ct)
    { (await admin.AccessAsync(Actor, ct)).Demand(Permissions.ViewRoles); return Ok((await admin.CatalogAsync(Actor, ct)).Roles); }
    [HttpGet("api/permisos"), ProducesResponseType(typeof(PermissionDto[]), 200)]
    public async Task<IActionResult> PermissionsList(CancellationToken ct)
    { (await admin.AccessAsync(Actor, ct)).Demand(Permissions.ViewPermissions); return Ok((await admin.CatalogAsync(Actor, ct)).Permisos); }
    [HttpPut("api/roles/{id:int}/permisos"), ProducesResponseType(204)]
    public async Task<IActionResult> RolePermissions(int id, PermissionAssignment request, CancellationToken ct) { await admin.PermissionsAsync(Actor, id, request.Ids, Correlation, ct); return NoContent(); }
    [HttpGet("api/administracion/catalogos"), ProducesResponseType(typeof(AdminCatalog), 200)]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Ok(await admin.CatalogAsync(Actor, ct));
    [HttpGet("api/negocios/contexto"), ProducesResponseType(typeof(BusinessContext), 200)]
    public async Task<IActionResult> Context(CancellationToken ct) => Ok(await admin.ContextAsync(Actor, User.FindFirstValue(PortalAccess.SessionClaim)!, "API", ct));
    [HttpPut("api/negocios/contexto"), ProducesResponseType(204)]
    public async Task<IActionResult> Select(ContextRequest request, CancellationToken ct)
    { await admin.SelectContextAsync(Actor, User.FindFirstValue(PortalAccess.SessionClaim)!, "API", request.IdNegocio, Correlation, ct); return NoContent(); }
}

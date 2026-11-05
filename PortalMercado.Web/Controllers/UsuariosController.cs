using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PortalMercado.Application.Administration;
namespace PortalMercado.Web.Controllers;

public sealed class AdministrationErrors : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessFailure error) return;
        context.Result = new ViewResult { ViewName = "~/Views/Administracion/Error.cshtml", StatusCode = error.Status,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState) { Model = error.Message } };
        context.ExceptionHandled = true;
    }
}
[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsuariosController(IUserAdministration admin) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Correlation => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
    public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default)
    { ViewBag.Access = await admin.AccessAsync(Actor, ct); return View(await admin.ListAsync(Actor, pagina, 20, ct)); }
    [HttpGet] public async Task<IActionResult> Crear(CancellationToken ct)
    { (await admin.AccessAsync(Actor, ct)).Demand(Permissions.Create); ViewBag.Catalog = await admin.CatalogAsync(Actor, ct); return View(new CreateUserRequest()); }
    [HttpPost] public async Task<IActionResult> Crear(CreateUserRequest request, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try { var result = await admin.CreateAsync(Actor, request, Correlation, ct); return RedirectToAction(nameof(Editar), new { id = result.IdUsuario }); }
            catch (AccessFailure e) when (e.Status is 400 or 409) { ModelState.AddModelError("", e.Message); }
        }
        ModelState.Remove(nameof(request.Password)); request.Password = "";
        ViewBag.Catalog = await admin.CatalogAsync(Actor, ct); return View(request);
    }
    [HttpGet] public async Task<IActionResult> Editar(long id, CancellationToken ct)
    { ViewBag.Catalog = await admin.CatalogAsync(Actor, ct); ViewBag.Access = await admin.AccessAsync(Actor, ct); return View(await admin.GetAsync(Actor, id, ct)); }
    [HttpPost] public async Task<IActionResult> Editar(long id, EditUserRequest request, CancellationToken ct)
    { if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa nombre y teléfono."); await admin.EditAsync(Actor, id, request, Correlation, ct); return RedirectToAction(nameof(Editar), new { id }); }
    [HttpPost] public async Task<IActionResult> Estado(long id, bool activo, CancellationToken ct)
    { await admin.StateAsync(Actor, id, activo, Correlation, ct); return RedirectToAction(nameof(Editar), new { id }); }
    [HttpPost] public async Task<IActionResult> Roles(long id, RoleAssignment request, CancellationToken ct)
    { if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la selección."); await admin.RolesAsync(Actor, id, request.Ids, Correlation, ct); return RedirectToAction(nameof(Editar), new { id }); }
    [HttpPost] public async Task<IActionResult> Negocios(long id, ScopeAssignment request, CancellationToken ct)
    { if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la selección."); await admin.BusinessesAsync(Actor, id, request.Ids, Correlation, ct); return RedirectToAction(nameof(Editar), new { id }); }
    [HttpPost] public async Task<IActionResult> Mercados(long id, ScopeAssignment request, CancellationToken ct)
    { if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la selección."); await admin.MarketsAsync(Actor, id, request.Ids, Correlation, ct); return RedirectToAction(nameof(Editar), new { id }); }
}
[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdministracionController(IUserAdministration admin) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public async Task<IActionResult> Permisos(CancellationToken ct)
    { (await admin.AccessAsync(Actor, ct)).Demand(Permissions.Manage); ViewBag.Access = await admin.AccessAsync(Actor, ct); return View(await admin.CatalogAsync(Actor, ct)); }
    [HttpPost] public async Task<IActionResult> Permisos(int id, PermissionAssignment request, CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new AccessFailure(400, "Revisa la selección.");
        await admin.PermissionsAsync(Actor, id, request.Ids, Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier, ct);
        return RedirectToAction(nameof(Permisos));
    }
}
[Authorize, AutoValidateAntiforgeryToken, TypeFilter(typeof(AdministrationErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NegociosController(IUserAdministration admin) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public async Task<IActionResult> Index(CancellationToken ct) => View(await admin.ContextAsync(Actor, User.FindFirstValue(PortalMercado.Infrastructure.Security.PortalAccess.SessionClaim)!, "MVC", ct));
    [HttpPost] public async Task<IActionResult> Seleccionar(ContextRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new AccessFailure(400, "Selecciona un negocio válido.");
        await admin.SelectContextAsync(Actor, User.FindFirstValue(PortalMercado.Infrastructure.Security.PortalAccess.SessionClaim)!, "MVC", request.IdNegocio,
            Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier, ct);
        return RedirectToAction(nameof(Index));
    }
}

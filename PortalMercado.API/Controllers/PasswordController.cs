using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.API.Controllers;
public sealed class PasswordErrors : IExceptionFilter
{
    public void OnException(ExceptionContext c)
    {
        if (c.Exception is not PasswordFailure e) return;
        c.Result = new ObjectResult(new ProblemDetails { Status = e.Status, Title = e.Message,
            Extensions = { ["traceId"] = Activity.Current?.TraceId.ToString() ?? c.HttpContext.TraceIdentifier } }) { StatusCode = e.Status };
        c.ExceptionHandled = true;
    }
}
[ApiController, Route("api/auth"), ServiceFilter(typeof(ApiAntiforgeryFilter)), TypeFilter(typeof(PasswordErrors))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(typeof(ProblemDetails), 400), ProducesResponseType(typeof(ProblemDetails), 429)]
public sealed class PasswordController(IPasswordService passwords) : ControllerBase
{
    private string Correlation => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("recovery")]
    [ProducesResponseType(typeof(RecoveryRequested), 202), ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken ct)
    { await passwords.RequestAsync(request, Correlation, ct); return Accepted(new RecoveryRequested(PasswordMessages.Requested)); }
    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth"), ProducesResponseType(204)]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        await passwords.ResetAsync(request, Correlation, ct);
        await HttpContext.SignOutAsync(PortalAccess.Scheme); return NoContent();
    }
    [HttpPost("change-password"), Authorize, EnableRateLimiting("auth")]
    [ProducesResponseType(204), ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Change(ChangePasswordRequest request, CancellationToken ct)
    {
        await passwords.ChangeAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), User.FindFirstValue(PortalAccess.SessionClaim)!, "API", request, Correlation, ct);
        await HttpContext.SignOutAsync(PortalAccess.Scheme); return NoContent();
    }
}
public sealed record RecoveryRequested(string Message);

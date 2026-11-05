using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.API.Controllers;

[ApiController, Route("api/auth"), ServiceFilter(typeof(ApiAntiforgeryFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService auth, IMfaService mfa) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous, EnableRateLimiting("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken, headerName = "X-CSRF-TOKEN" });
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthProfile), 200)]
    [ProducesResponseType(typeof(MfaChallenge), 202)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 429)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var outcome = await auth.LoginAsync(request, "API", CorrelationId, ct);
        if (outcome is null) return Problem(statusCode: 401, title: "No se pudo iniciar sesión",
            detail: "Revisa tus credenciales o inténtalo más tarde.");
        if (outcome.Mfa is not null) return Accepted(outcome.Mfa);
        var session = outcome.Session!;
        await HttpContext.SignInAsync(PortalAccess.Scheme,
            PortalAccess.Principal(session.Profile, session.Credential), PortalAccess.Properties(session));
        return Ok(session.Profile);
    }

    [HttpPost("logout"), Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(User.FindFirst(PortalAccess.SessionClaim)!.Value, "API", CorrelationId, ct);
        await HttpContext.SignOutAsync(PortalAccess.Scheme);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    [ProducesResponseType(typeof(AuthProfile), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var profile = await auth.ValidateAsync(User.FindFirst(PortalAccess.SessionClaim)!.Value, "API", ct);
        return profile is null ? Unauthorized() : Ok(profile);
    }
    [HttpPost("mfa/setup"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(MfaEnrollment), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> SetupMfa(MfaChallengeRequest request, CancellationToken ct)
    {
        var enrollment = await mfa.GetEnrollmentAsync(request.Challenge, "API", ct);
        return enrollment is null ? Problem(statusCode: 401, title: "Verificación no disponible") : Ok(enrollment);
    }

    [HttpPost("mfa/verify"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(MfaLoginResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> VerifyMfa(MfaVerifyRequest request, CancellationToken ct)
    {
        var result = await mfa.CompleteAsync(request.Challenge, request.Code, "API", CorrelationId, ct, request.RegenerateRecoveryCodes);
        if (result is null) return Problem(statusCode: 401, title: "No se pudo completar la verificación",
            detail: "Revisa el código o vuelve a iniciar sesión.");
        await HttpContext.SignInAsync(PortalAccess.Scheme,
            PortalAccess.Principal(result.Session.Profile, result.Session.Credential), PortalAccess.Properties(result.Session));
        return Ok(new MfaLoginResponse(result.Session.Profile, result.RecoveryCodes));
    }

    private string CorrelationId => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
}

public sealed record MfaLoginResponse(AuthProfile Profile, string[] RecoveryCodes);

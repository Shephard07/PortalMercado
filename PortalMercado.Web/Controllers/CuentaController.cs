using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Web.Controllers;

[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CuentaController(IAuthService auth, IMfaService mfa) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login() => View(new LoginRequest());

    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) { request.Password = ""; return View(request); }
        var outcome = await auth.LoginAsync(request, "MVC", CorrelationId, ct);
        if (outcome is null)
        {
            ModelState.AddModelError("", "No se pudo iniciar sesión. Revisa tus credenciales o inténtalo más tarde.");
            ModelState.Remove(nameof(request.Password)); request.Password = "";
            return View(request);
        }
        if (outcome.Mfa is not null)
        {
            Response.Cookies.Append(MfaCookie, outcome.Mfa.Challenge, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/",
                Expires = new DateTimeOffset(DateTime.SpecifyKind(outcome.Mfa.ExpiresUtc, DateTimeKind.Utc))
            });
            return RedirectToAction(outcome.Mfa.EnrollmentRequired ? nameof(ConfigurarMfa) : nameof(VerificarMfa));
        }
        var session = outcome.Session!;
        await HttpContext.SignInAsync(PortalAccess.Scheme,
            PortalAccess.Principal(session.Profile, session.Credential), PortalAccess.Properties(session));
        return RedirectToAction(nameof(Perfil));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(User.FindFirst(PortalAccess.SessionClaim)!.Value, "MVC", CorrelationId, ct);
        await HttpContext.SignOutAsync(PortalAccess.Scheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> Perfil(CancellationToken ct)
    {
        var profile = await auth.ValidateAsync(User.FindFirst(PortalAccess.SessionClaim)!.Value, "MVC", ct);
        return profile is null ? Challenge() : View(profile);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccesoDenegado() { Response.StatusCode = 403; return View(); }
    [HttpGet, AllowAnonymous]
    public IActionResult SesionExpirada() { Response.StatusCode = 401; return View(); }
    private const string MfaCookie = "__Host-PortalMercado.Mfa.MVC";

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ConfigurarMfa(CancellationToken ct)
    {
        var challenge = Request.Cookies[MfaCookie];
        var enrollment = challenge is null ? null : await mfa.GetEnrollmentAsync(challenge, "MVC", ct);
        return enrollment is null ? RedirectToAction(nameof(Login)) : View(enrollment);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult VerificarMfa() => Request.Cookies.ContainsKey(MfaCookie)
        ? View(new MfaCodeRequest()) : RedirectToAction(nameof(Login));

    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmarMfa(MfaCodeRequest request, CancellationToken ct)
    {
        var challenge = Request.Cookies[MfaCookie];
        if (challenge is null) return RedirectToAction(nameof(Login));
        var result = ModelState.IsValid ? await mfa.CompleteAsync(challenge, request.Code, "MVC", CorrelationId, ct, request.RegenerateRecoveryCodes) : null;
        if (result is null)
        {
            // Si el reto sigue siendo de enrolamiento, volver a mostrar la clave propia.
            var enrollment = await mfa.GetEnrollmentAsync(challenge, "MVC", ct);
            ModelState.AddModelError("", "No se pudo verificar el código. Si el proceso venció, inicia sesión nuevamente.");
            ModelState.Remove(nameof(request.Code)); request.Code = "";
            return enrollment is null ? View("VerificarMfa", request) : View("ConfigurarMfa", enrollment);
        }
        Response.Cookies.Delete(MfaCookie, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
        await HttpContext.SignInAsync(PortalAccess.Scheme,
            PortalAccess.Principal(result.Session.Profile, result.Session.Credential), PortalAccess.Properties(result.Session));
        if (result.RecoveryCodes.Length > 0) return View("CodigosRecuperacion", result.RecoveryCodes);
        return RedirectToAction(nameof(Perfil));
    }

    private string CorrelationId => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
}

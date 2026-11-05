using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.Web.Controllers;

[AutoValidateAntiforgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordController(IPasswordService passwords, IConfiguration config, IWebHostEnvironment environment) : Controller
{
    private string Correlation => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
    [HttpGet("Cuenta/Recuperar"), AllowAnonymous]
    public IActionResult Recuperar() => View(new ForgotPasswordRequest());
    [HttpPost("Cuenta/Recuperar"), AllowAnonymous, EnableRateLimiting("recovery")]
    public async Task<IActionResult> Recuperar(ForgotPasswordRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(request);
        try { await passwords.RequestAsync(request, Correlation, ct); }
        catch (PasswordFailure e) { Response.StatusCode = e.Status; ModelState.AddModelError("", e.Message); return View(request); }
        ViewBag.Pickup = environment.IsDevelopment() && config["Recovery:Provider"] == "Pickup";
        return View("SolicitudRecibida");
    }
    [HttpGet("Cuenta/Restablecer"), AllowAnonymous]
    public IActionResult Restablecer(string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!MfaCryptography.IsCredential(token)) { Response.StatusCode = 400; return View("EnlaceInvalido"); }
        return View(new ResetPasswordRequest { Token = token! });
    }
    [HttpPost("Cuenta/Restablecer"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Restablecer(ResetPasswordRequest request, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (ModelState.IsValid)
        {
            try
            {
                await passwords.ResetAsync(request, Correlation, ct);
                await HttpContext.SignOutAsync(PortalAccess.Scheme); return View("PasswordActualizada");
            }
            catch (PasswordFailure e) { Response.StatusCode = e.Status; ModelState.AddModelError("", e.Message); }
        }
        if (!ModelState.IsValid) ModelState.AddModelError("", PasswordMessages.Policy);
        ClearPasswords(); return View(request);
    }
    [HttpGet("Cuenta/CambiarPassword"), Authorize]
    public IActionResult CambiarPassword() => View(new ChangePasswordRequest());
    [HttpPost("Cuenta/CambiarPassword"), Authorize, EnableRateLimiting("auth")]
    public async Task<IActionResult> CambiarPassword(ChangePasswordRequest request, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await passwords.ChangeAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), User.FindFirstValue(PortalAccess.SessionClaim)!, "MVC", request, Correlation, ct);
                await HttpContext.SignOutAsync(PortalAccess.Scheme); return View("PasswordActualizada");
            }
            catch (PasswordFailure e) { Response.StatusCode = e.Status; ModelState.AddModelError("", e.Message); }
        }
        if (!ModelState.IsValid) ModelState.AddModelError("", PasswordMessages.Policy);
        ClearPasswords(); return View(request);
    }
    private void ClearPasswords()
    {
        ModelState.Remove("CurrentPassword"); ModelState.Remove("NewPassword"); ModelState.Remove("ConfirmPassword");
        // Las vistas usan inputs password sin valor ni asp-for: nunca reflejan los valores recibidos.
    }
}

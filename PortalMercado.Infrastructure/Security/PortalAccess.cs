using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Security;

public sealed record AccessHost(string Name);

public static class PortalAccess
{
    public const string Scheme = "PortalMercado";
    public const string SessionClaim = "pm_session";
    public static IServiceCollection AddPortalAccess(this IServiceCollection services, string host, IConfiguration configuration, IHostEnvironment environment)
    {
        if (host is not ("API" or "MVC")) throw new ArgumentException("Host no válido.", nameof(host));
        var keyPath = configuration["Security:DataProtectionKeyPath"];
        if (string.IsNullOrWhiteSpace(keyPath))
            keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortalMercado", "Keys", environment.EnvironmentName);
        var protection = services.AddDataProtection().SetApplicationName("PortalMercado.Security")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        services.AddSingleton(new AccessHost(host));
        services.AddScoped<MfaCryptography>();
        services.AddScoped<IMfaService, SqlMfaService>();
        services.AddScoped<IPasswordService, SqlPasswordService>();
        services.AddScoped<IRecoveryDelivery, RecoveryDelivery>();
        services.AddSingleton(TimeProvider.System);
        services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<IAuthService, SqlAuthService>();
        services.AddScoped<IAdminBootstrap, AdminBootstrap>();
        services.AddScoped<PortalMercado.Application.Administration.IUserAdministration, PortalMercado.Infrastructure.Administration.SqlUserAdministration>();
        services.AddScoped<PortalCookieEvents>();
        services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "__Host-PortalMercado." + host;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.LoginPath = "/Cuenta/Login";
            options.AccessDeniedPath = "/Cuenta/AccesoDenegado";
            options.EventsType = typeof(PortalCookieEvents);
        });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-PortalMercado.Csrf." + host;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("recovery", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 3, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy("csrf", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 429, Title = "Demasiados intentos", Detail = "Espera antes de volver a intentarlo."
                }, ct);
            };
        });
        return services;
    }

    public static ClaimsPrincipal Principal(AuthProfile profile, string credential) => new(new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.NameIdentifier, profile.IdUsuario.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, profile.Nombre), new Claim(SessionClaim, credential)
        }.Concat(profile.Roles.Select(r => new Claim(ClaimTypes.Role, r)))
        .Concat(profile.Permisos.Select(p => new Claim("pm_permission", p))), Scheme));

    public static AuthenticationProperties Properties(AuthSession session) => new()
    {
        IsPersistent = false, AllowRefresh = false,
        ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(session.ExpiresUtc, DateTimeKind.Utc))
    };
}

public sealed class PortalCookieEvents(IAuthService auth, AccessHost host) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var credential = context.Principal?.FindFirst(PortalAccess.SessionClaim)?.Value;
        var profile = credential is null ? null : await auth.ValidateAsync(credential, host.Name, context.HttpContext.RequestAborted);
        if (profile is null)
        {
            context.HttpContext.Items["pm_session_invalid"] = true;
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(PortalAccess.Scheme);
            return;
        }
        // Reconsulta roles y pertenencias en cada solicitud. No confiar en claims antiguos.
        context.ReplacePrincipal(PortalAccess.Principal(profile, credential!));
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (host.Name == "API") return Error(context, 401, "Autenticación requerida");
        if (context.HttpContext.Items.ContainsKey("pm_session_invalid") ||
            context.Request.Cookies.ContainsKey("__Host-PortalMercado.MVC"))
        {
            context.Response.Redirect("/Cuenta/SesionExpirada");
            return Task.CompletedTask;
        }
        return base.RedirectToLogin(context);
    }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        host.Name == "API" ? Error(context, 403, "Acceso denegado") : base.RedirectToAccessDenied(context);
    private static Task Error(RedirectContext<CookieAuthenticationOptions> context, int status, string title)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title });
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;
namespace PortalMercado.Tests;
public sealed class PasswordHttpTests
{
    [Fact] public async Task RecoveryRequiresCsrfBeforeCallingService()
    {
        await using var f=new Factory(); var c=f.Client();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="v@example.invalid" })).StatusCode);
        Assert.Equal(0,f.Passwords.Requests);
    }
    [Fact] public async Task RecoveryResponsesAreGenericAndContainNoToken()
    {
        await using var f=new Factory(); var c=f.Client(); await Csrf(c);
        var a=await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="v@example.invalid" });
        var b=await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="desconocido@example.invalid" });
        Assert.Equal(HttpStatusCode.Accepted,a.StatusCode); Assert.Equal(a.StatusCode,b.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(),await b.Content.ReadAsStringAsync());
        Assert.DoesNotContain("token",await a.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task ProviderFailureReturns503Safely()
    {
        await using var f=new Factory(); f.Passwords.FailDelivery=true; var c=f.Client(); await Csrf(c);
        var r=await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="v@example.invalid" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode); Assert.Contains(PasswordMessages.Unavailable,await r.Content.ReadAsStringAsync());
    }
    [Fact] public async Task ChangeRequiresAuthenticatedSession() { await using var f=new Factory(); Assert.Equal(HttpStatusCode.Unauthorized,(await f.Client().PostAsJsonAsync("/api/auth/change-password",new ChangePasswordRequest())).StatusCode); }
    [Fact] public async Task ChangeUsesServerIdentityAndClearsAuthenticationCookie()
    {
        await using var f=new Factory(); var c=f.Client(); await Csrf(c);
        await c.PostAsJsonAsync("/api/auth/login",new LoginRequest { Correo="v@example.invalid",Password="PruebaSegura123!" }); await Csrf(c);
        var r=await c.PostAsJsonAsync("/api/auth/change-password",new ChangePasswordRequest { CurrentPassword="PruebaSegura123!",NewPassword="NuevaSegura123!",ConfirmPassword="NuevaSegura123!" });
        Assert.Equal(HttpStatusCode.NoContent,r.StatusCode); Assert.Equal(77,f.Passwords.Actor); Assert.Equal("API",f.Passwords.Host);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact] public async Task ResetDoesNotSignInAutomatically()
    {
        await using var f=new Factory(); var c=f.Client(); await Csrf(c);
        var r=await c.PostAsJsonAsync("/api/auth/reset-password",new ResetPasswordRequest { Token=new string('a',43),NewPassword="NuevaSegura123!",ConfirmPassword="NuevaSegura123!" });
        Assert.Equal(HttpStatusCode.NoContent,r.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact] public async Task RecoveryRateLimiterReturns429()
    {
        await using var f=new Factory(); var c=f.Client(); await Csrf(c);
        for(var i=0;i<3;i++) Assert.Equal(HttpStatusCode.Accepted,(await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="v@example.invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await c.PostAsJsonAsync("/api/auth/forgot-password",new ForgotPasswordRequest { Correo="v@example.invalid" })).StatusCode);
    }
    private static async Task Csrf(HttpClient c)
    { var j=await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",j.GetProperty("requestToken").GetString()); }
    private sealed class Factory : WebApplicationFactory<PortalMercado.API.Controllers.PasswordController>
    {
        public ControlledPasswords Passwords { get; }=new();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development"); b.UseSetting("ConnectionStrings:PortalMercado","Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.ConfigureTestServices(s=> { s.RemoveAll<IAuthService>();s.AddSingleton<IAuthService>(new ControlledAuth()); s.RemoveAll<IPasswordService>();s.AddSingleton<IPasswordService>(Passwords); });
        }
        public HttpClient Client()=>CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false });
    }
}
public sealed class ControlledPasswords : IPasswordService
{
    public int Requests { get; private set; } public bool FailDelivery { get; set; } public bool FailReset { get; set; }
    public long Actor { get; private set; } public string? Host { get; private set; }
    public Task RequestAsync(ForgotPasswordRequest request,string correlation,CancellationToken ct)
    { Requests++;if(FailDelivery) throw new PasswordFailure(503,PasswordMessages.Unavailable);return Task.CompletedTask; }
    public Task ResetAsync(ResetPasswordRequest request,string correlation,CancellationToken ct)
    { if(FailReset) throw new PasswordFailure(400,PasswordMessages.Invalid);return Task.CompletedTask; }
    public Task ChangeAsync(long actor,string credential,string host,ChangePasswordRequest request,string correlation,CancellationToken ct)
    { Actor=actor;Host=host;return Task.CompletedTask; }
}

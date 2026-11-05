using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;
namespace PortalMercado.Tests;
public sealed class PasswordWebTests
{
    [Fact] public async Task RecoveryHasAntiforgeryAndShowsLocalDeliveryMode()
    {
        await using var f=new Factory();var c=f.Client();var html=await c.GetStringAsync("/Cuenta/Recuperar");
        var r=await c.PostAsync("/Cuenta/Recuperar",Form(Token(html),("Correo","v@example.invalid")));
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("Modo de pruebas",await r.Content.ReadAsStringAsync());
    }
    [Fact] public async Task ResetPageDisablesCachingAndReferrers()
    {
        await using var f=new Factory();var r=await f.Client().GetAsync("/Cuenta/Restablecer?token="+new string('a',43));
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains("no-store",r.Headers.CacheControl!.ToString());Assert.Equal("no-referrer",Assert.Single(r.Headers.GetValues("Referrer-Policy")));
    }
    [Fact] public async Task InvalidResetPostNeverEchoesPasswords()
    {
        await using var f=new Factory();f.Passwords.FailReset=true;var c=f.Client();var html=await c.GetStringAsync("/Cuenta/Restablecer?token="+new string('a',43));
        var r=await c.PostAsync("/Cuenta/Restablecer",Form(Token(html),("Token",new string('a',43)),("NewPassword","NoRepetirEsta123!"),("ConfirmPassword","NoRepetirEsta123!")));
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.DoesNotContain("NoRepetirEsta123!",await r.Content.ReadAsStringAsync());
    }
    [Fact] public async Task MissingTokenShowsSafeInvalidLinkPage()
    { await using var f=new Factory();var r=await f.Client().GetAsync("/Cuenta/Restablecer");Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode); }
    [Fact] public async Task ChangePageRedirectsAnonymousUserToLogin()
    { await using var f=new Factory();Assert.Equal(HttpStatusCode.Redirect,(await f.Client().GetAsync("/Cuenta/CambiarPassword")).StatusCode); }
    private static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static FormUrlEncodedContent Form(string token,params (string Key,string Value)[] values)=>new(values.Select(x=>new KeyValuePair<string,string>(x.Key,x.Value)).Append(new("__RequestVerificationToken",token)));
    private sealed class Factory : WebApplicationFactory<PortalMercado.Web.Controllers.PasswordController>
    {
        public ControlledPasswords Passwords { get; }=new();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");b.UseSetting("ConnectionStrings:PortalMercado","Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
            b.ConfigureTestServices(s=> { s.RemoveAll<IAuthService>();s.AddSingleton<IAuthService>(new ControlledAuth());s.RemoveAll<IPasswordService>();s.AddSingleton<IPasswordService>(Passwords); });
        }
        public HttpClient Client()=>CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false });
    }
}

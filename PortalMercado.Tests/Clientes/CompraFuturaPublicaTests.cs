using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

public sealed class CompraFuturaPublicaTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/explorar")]
    [InlineData("/explorar/buscar?q=Mango&tipo=productos&orden=precio-asc&tamano=1&pagina=2")]
    [InlineData("/tiendas/fruteria-el-sol?q=Tomate&orden=precio-asc&tamano=1&pagina=1")]
    public async Task Boton_publico_existe_y_exige_login_sin_crear_una_compra(string ruta)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var html = await c.GetStringAsync(ruta);
        var destino = EnlaceCompra(html);
        Assert.Contains("Comprar (pr", WebUtility.HtmlDecode(html));
        Assert.Contains("/Compras/Preparar", destino);
        var r = await c.GetAsync(destino);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Cuenta/Login", r.Headers.Location!.OriginalString);
        Assert.Equal(0, f.Auth.LoginCalls);
    }
    [Theory]
    [InlineData("/explorar/buscar?q=Mango&tipo=productos&orden=precio-asc&tamano=1&pagina=2", false)]
    [InlineData("/tiendas/fruteria-el-sol?q=Tomate&orden=precio-asc&tamano=1&pagina=1", false)]
    [InlineData("/explorar/buscar?q=Mango&tipo=productos&orden=precio-asc&tamano=1&pagina=2", true)]
    public async Task Clic_login_y_MFA_conservan_consulta_filtros_y_pagina(string ruta, bool mfa)
    {
        await using var f = new Factory(mfa); using var c = f.Cliente();
        var preparacion = EnlaceCompra(await c.GetStringAsync(ruta));
        var challenge = await c.GetAsync(preparacion);
        var loginHtml = await c.GetStringAsync(challenge.Headers.Location!);
        var returnUrl = Campo(loginHtml, "returnUrl");
        var login = await c.PostAsync("/Cuenta/Login", Form(Campo(loginHtml, "__RequestVerificationToken"),
            ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!"), ("returnUrl", returnUrl)));
        if (mfa)
        {
            var setup = await c.GetStringAsync(login.Headers.Location!);
            login = await c.PostAsync("/Cuenta/ConfirmarMfa", Form(Campo(setup, "__RequestVerificationToken"), ("Code", "123456")));
        }
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(preparacion, login.Headers.Location!.OriginalString);
        var aviso = await c.GetAsync(login.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, aviso.StatusCode);
        var html = WebUtility.HtmlDecode(await aviso.Content.ReadAsStringAsync());
        Assert.Contains("Las compras estarán disponibles próximamente", html);
        Assert.Contains("href=\"" + ruta + (ruta.StartsWith("/tiendas/") ? "#catalogo" : "") + "\"", html);
        Assert.DoesNotContain("name=\"cantidad\"", html);
    }
    [Fact]
    public async Task Boton_autenticado_llega_al_aviso_sin_pedir_otro_login_y_carrito_sigue_deshabilitado()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var loginHtml = await c.GetStringAsync("/Cuenta/Login");
        await c.PostAsync("/Cuenta/Login", Form(Campo(loginHtml, "__RequestVerificationToken"),
            ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!")));
        var html = await c.GetStringAsync("/explorar/buscar?tipo=productos");
        var carrito = Regex.Matches(html, "<button[^>]*disabled[^>]*>Agregar al carrito</button>");
        Assert.NotEmpty(carrito);
        var r = await c.GetAsync(EnlaceCompra(html));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(1, f.Auth.LoginCalls);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
    }
    private static string EnlaceCompra(string html)
    {
        var enlaces = Regex.Matches(html, "<a\\b[^>]*data-compra-futura[^>]*>");
        var enlace = Assert.Single(enlaces.Cast<Match>()).Value;
        var href = Regex.Match(enlace, "href=\"([^\"]+)\"");
        Assert.True(href.Success); return WebUtility.HtmlDecode(href.Groups[1].Value);
    }
    private static string Campo(string html, string nombre)
    {
        var match = Regex.Match(html, "name=\"" + Regex.Escape(nombre) + "\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string token, params (string Clave, string Valor)[] campos) =>
        new(campos.Select(x => new KeyValuePair<string,string>(x.Clave, x.Valor))
            .Append(new KeyValuePair<string,string>("__RequestVerificationToken", token)));
    private sealed class Factory(bool mfa = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public ControlledAuth Auth { get; } = new() { RequireMfa = mfa };
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "true");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(Auth);
                s.RemoveAll<IMfaService>(); s.AddSingleton<IMfaService>(new ControlledMfa(Auth));
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

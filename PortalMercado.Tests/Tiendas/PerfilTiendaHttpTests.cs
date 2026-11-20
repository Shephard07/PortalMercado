using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class PerfilTiendaHttpTests
{
    [Fact]
    public async Task Mvc_exige_login_y_formulario_con_token_real()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        var anonimo = await client.GetAsync("/mis-tiendas/1/perfil");
        Assert.Equal(HttpStatusCode.Redirect, anonimo.StatusCode);
        Assert.Contains("/Cuenta/Login", anonimo.Headers.Location!.ToString());
        await LoginMvc(client);
        var html = await client.GetStringAsync("/mis-tiendas/1/perfil");
        Assert.NotEmpty(Token(html));
        Assert.Contains("Frutería El Sol", WebUtility.HtmlDecode(html));
        Assert.Contains("name=\"Version\"", html);
        Assert.DoesNotContain("name=\"Slug\"", html);
        Assert.Contains("/mis-tiendas/1/perfil", await client.GetStringAsync("/mis-tiendas"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/mis-tiendas/1/perfil",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Descripcion"] = "Sin token" }))).StatusCode);
    }

    [Fact]
    public async Task Mvc_guarda_con_csrf_y_codifica_descripcion_en_la_ficha_publica()
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        await LoginMvc(client);
        var html = await client.GetStringAsync("/mis-tiendas/1/perfil");
        var version = Regex.Match(html, "name=\"Version\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var respuesta = await client.PostAsync("/mis-tiendas/1/perfil", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Version"] = version,
            ["Descripcion"] = "<script>PRUEBA</script>", ["Horario"] = "Horario actualizado",
            ["Slug"] = "slug-ajeno", ["IdMercado"] = "999", ["Estado"] = "BAJA"
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var publico = await client.GetStringAsync("/tiendas/fruteria-el-sol");
        Assert.Contains("&lt;script&gt;PRUEBA&lt;/script&gt;", publico);
        Assert.DoesNotContain("<script>PRUEBA</script>", publico);
        Assert.Contains("Horario actualizado", publico);
        Assert.DoesNotContain("slug-ajeno", publico);
        Assert.Contains("Perfil comercial actualizado", await client.GetStringAsync(respuesta.Headers.Location!));
    }

    [Theory]
    [InlineData("/mis-tiendas/90/perfil", HttpStatusCode.Forbidden)]
    [InlineData("/mis-tiendas/1001/perfil", HttpStatusCode.Forbidden)]
    public async Task Mvc_no_confunde_id_publico_con_negocio_autorizado(string ruta, HttpStatusCode estado)
    {
        await using var factory = new WebFactory();
        using var client = Cliente(factory);
        await LoginMvc(client);
        Assert.Equal(estado, (await client.GetAsync(ruta)).StatusCode);
    }

    [Fact]
    public async Task Api_exige_sesion_y_csrf_para_la_mutacion()
    {
        await using var factory = new ApiFactory();
        using var client = Cliente(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/mis-tiendas/1/perfil")).StatusCode);
        await LoginApi(client);
        var datos = (await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil"))!.Datos;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/mis-tiendas/1/perfil", datos)).StatusCode);
    }

    [Fact]
    public async Task Api_guarda_y_rechaza_version_antigua_sin_sobrescribir()
    {
        await using var factory = new ApiFactory();
        using var client = Cliente(factory);
        await LoginApi(client);
        var antes = (await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil"))!;
        antes.Datos.Descripcion = "Cambio desde API";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/mis-tiendas/1/perfil", antes.Datos)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/mis-tiendas/1/perfil", antes.Datos)).StatusCode);
        var publico = await client.GetFromJsonAsync<TiendaPublica>("/api/tiendas/fruteria-el-sol");
        Assert.Equal("Cambio desde API", publico!.Descripcion);
        Assert.Null(publico.Correo);
        var despues = await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil");
        Assert.Equal(77, despues!.UltimoCambio!.Actor);
        Assert.Equal(1, despues.UltimoCambio.IdNegocio);
        Assert.NotEqual(antes.Datos.Version, despues.Datos.Version);
    }

    [Theory]
    [InlineData("slug", "cambiado")]
    [InlineData("estado", "BAJA")]
    [InlineData("ruc", "12345678901")]
    [InlineData("telefonoPublico", true)]
    public async Task Api_rechaza_propiedades_fuera_del_contrato(string propiedad, object valor)
    {
        await using var factory = new ApiFactory();
        using var client = Cliente(factory);
        await LoginApi(client);
        var antes = await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil");
        var cuerpo = new Dictionary<string, object?>
        {
            ["version"] = antes!.Datos.Version, ["descripcion"] = "Cambio inválido", [propiedad] = valor
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/mis-tiendas/1/perfil", cuerpo)).StatusCode);
        var despues = await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil");
        Assert.Equal(antes.Datos.Version, despues!.Datos.Version);
        Assert.Null(despues.UltimoCambio);
    }

    [Fact]
    public async Task Api_reconsulta_permiso_y_pertenencia_en_cada_peticion()
    {
        await using var factory = new ApiFactory();
        using var client = Cliente(factory);
        await LoginApi(client);
        var perfil = (await client.GetFromJsonAsync<PerfilTienda>("/api/mis-tiendas/1/perfil"))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/mis-tiendas/90/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/mis-tiendas/90/perfil", perfil.Datos)).StatusCode);
        factory.Usuarios.Habilitado = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/mis-tiendas/1/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/mis-tiendas/1/perfil", perfil.Datos)).StatusCode);
    }

    private static HttpClient Cliente<T>(WebApplicationFactory<T> f) where T : class =>
        f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task LoginMvc(HttpClient c)
    {
        var html = await c.GetStringAsync("/Cuenta/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Correo"] = "v@example.invalid", ["Password"] = "PruebaSegura123!"
        }))).StatusCode);
    }
    private static async Task Csrf(HttpClient c)
    {
        var json = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task LoginApi(HttpClient c)
    {
        await Csrf(c);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" })).StatusCode);
        await Csrf(c);
    }
    private sealed class WebFactory : WebApplicationFactory<PortalMercado.Web.Controllers.PerfilTiendaController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => Configurar(builder, new());
    }
    private sealed class ApiFactory : WebApplicationFactory<PortalMercado.API.Controllers.PerfilTiendaController>
    {
        public TiendasUsuariosPrueba Usuarios { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => Configurar(builder, Usuarios);
    }
    private static void Configurar(IWebHostBuilder b, TiendasUsuariosPrueba usuarios)
    {
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;Connect Timeout=1;");
        b.UseSetting("Tiendas:Demostracion", "true");
        b.UseSetting("Tiendas:NegocioLocalDemostracion", "1");
        b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(new ControlledAuth());
            s.RemoveAll<IUserAdministration>(); s.AddSingleton<IUserAdministration>(usuarios);
        });
    }
}

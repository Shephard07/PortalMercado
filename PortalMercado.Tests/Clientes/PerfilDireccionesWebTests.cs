using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

public sealed class PerfilDireccionesWebTests
{
    [Theory]
    [InlineData("/Cliente/Perfil")]
    [InlineData("/Cliente/Direcciones")]
    [InlineData("/Cliente/Nueva")]
    public async Task Paginas_privadas_exigen_sesion(string ruta)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Cuenta/Login", r.Headers.Location!.OriginalString);
        Assert.Contains("ReturnUrl", r.Headers.Location.OriginalString);
    }
    [Fact]
    public async Task Perfil_muestra_datos_propios_y_no_permite_editar_correo()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/Cliente/Perfil"); var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("name=\"Datos.Apellidos\"", html); Assert.Contains("name=\"Datos.Celular\"", html);
        Assert.DoesNotContain("name=\"Correo\"", html); Assert.DoesNotContain("name=\"IdCliente\"", html);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
    }
    [Fact]
    public async Task Perfil_guarda_solo_campos_permitidos_con_usuario_de_sesion()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var token = Token(await c.GetStringAsync("/Cliente/Perfil"));
        var r = await c.PostAsync("/Cliente/Perfil", Form(token, ("Datos.Apellidos", " Cotrina "),
            ("Datos.Celular", "999000111"), ("IdUsuario", "999"), ("Correo", "otro@example.invalid")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(77, f.Perfiles.UsuarioGuardado);
        Assert.Equal("Cotrina", f.Perfiles.Datos!.Apellidos);
    }
    [Fact]
    public async Task Documento_incompleto_se_devuelve_con_error_sin_guardar()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsync("/Cliente/Perfil", Form(Token(await c.GetStringAsync("/Cliente/Perfil")),
            ("Datos.TipoDocumento", "DNI")));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Null(f.Perfiles.UsuarioGuardado);
        Assert.Contains("value=\"DNI\"", await r.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Listado_vacio_y_formulario_de_nueva_direccion_son_accesibles()
    {
        await using var f = new Factory(); f.Repo.Datos = []; using var c = await Entrar(f);
        Assert.Contains("Todav", await c.GetStringAsync("/Cliente/Direcciones"));
        var html = await c.GetStringAsync("/Cliente/Nueva");
        Assert.Contains("name=\"Direccion\"", html); Assert.Contains("name=\"Latitud\"", html);
        Assert.DoesNotContain("name=\"Principal\"", html); Assert.DoesNotContain("name=\"IdUsuario\"", html);
    }
    [Fact]
    public async Task Crear_direccion_usa_datos_validos_y_actor_autenticado()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsync("/Cliente/Guardar", Form(Token(await c.GetStringAsync("/Cliente/Nueva")),
            ("Direccion", " Calle 100 "), ("Etiqueta", "Casa"), ("IdUsuario", "999"), ("Principal", "true")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(1, f.Repo.Escrituras); Assert.Equal(77, f.Repo.Usuario);
        Assert.Equal("Calle 100", f.Repo.Guardado!.Direccion);
        Assert.Equal("crear", f.Repo.Accion);
    }
    [Theory]
    [InlineData("Direccion", "")]
    [InlineData("Latitud", "abc")]
    [InlineData("Latitud", "91")]
    public async Task Direccion_invalida_no_se_guarda(string campo, string valor)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsync("/Cliente/Guardar", Form(Token(await c.GetStringAsync("/Cliente/Nueva")),
            ("Direccion", "Calle 100"), (campo, valor)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Repo.Escrituras);
    }
    [Theory]
    [InlineData("/Cliente/Editar?id=999")]
    [InlineData("/Cliente/Eliminar?id=999")]
    public async Task Direccion_ajena_no_se_muestra(string ruta)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("Calle de prueba 123", await r.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("Principal")]
    [InlineData("ConfirmarEliminar")]
    [InlineData("Guardar")]
    public async Task Operacion_con_id_ajeno_no_modifica_direcciones(string accion)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var token = Token(await c.GetStringAsync("/Cliente/Nueva"));
        var r = await c.PostAsync("/Cliente/" + accion, Form(token, ("id", "999"), ("Direccion", "Calle ajena")));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); Assert.Equal(0, f.Repo.Escrituras);
    }
    [Theory]
    [InlineData("Principal")]
    [InlineData("ConfirmarEliminar")]
    public async Task Mutaciones_propias_usan_POST_y_csrf(string accion)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var token = Token(await c.GetStringAsync("/Cliente/Editar?id=1"));
        var sinToken = await c.PostAsync("/Cliente/" + accion, Form(null, ("id", "1")));
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode); Assert.Equal(0, f.Repo.Escrituras);
        var r = await c.PostAsync("/Cliente/" + accion, Form(token, ("id", "1")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.Equal(1, f.Repo.Escrituras);
        Assert.Equal(77, f.Repo.Usuario);
    }
    [Fact]
    public async Task Editar_direccion_propia_conserva_id_y_actualiza_solo_sus_datos()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var html = await c.GetStringAsync("/Cliente/Editar?id=1");
        var r = await c.PostAsync("/Cliente/Guardar", Form(Token(html), ("id", "1"), ("Direccion", "Nueva dirección")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); Assert.Equal("actualizar", f.Repo.Accion);
        Assert.Equal(1, f.Repo.Id); Assert.Equal(77, f.Repo.Usuario);
    }
    [Fact]
    public async Task Eliminar_GET_solo_muestra_confirmacion_y_no_escribe()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Cliente/Eliminar?id=1"));
        Assert.Contains("¿Deseas eliminar", html); Assert.Equal(0, f.Repo.Escrituras);
    }
    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    public async Task Sin_cliente_o_proveedor_no_muestra_datos_ficticios(bool pendiente, HttpStatusCode esperado)
    {
        await using var f = new Factory(pendiente: pendiente); f.Perfiles.Perfil = null; using var c = await Entrar(f);
        var r = await c.GetAsync("/Cliente/Perfil");
        Assert.Equal(esperado, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync(); Assert.DoesNotContain("ConnectionStrings", html);
        Assert.DoesNotContain("name=\"Datos.Apellidos\"", html);
    }
    private static async Task<HttpClient> Entrar(Factory f)
    {
        var c = f.Cliente();
        var login = await c.PostAsync("/Cuenta/Login", Form(Token(await c.GetStringAsync("/Cuenta/Login")),
            ("Correo", "v@example.invalid"), ("Password", "PruebaSegura123!")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return c;
    }
    private static string Token(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success); return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(string? token, params (string Clave, string Valor)[] campos)
    {
        var datos = new Dictionary<string,string>();
        foreach (var (clave, valor) in campos) datos[clave] = valor;
        if (token is not null) datos["__RequestVerificationToken"] = token;
        return new(datos);
    }
    private sealed class Factory(bool pendiente = false) : WebApplicationFactory<PortalMercado.Web.Controllers.HomeController>
    {
        public PerfilClientesPrueba Perfiles { get; } = new() { Perfil = ClientesPrueba.Perfil(usuario: 77) };
        public DireccionesClientePrueba Repo { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "false");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(new ControlledAuth());
                if (!pendiente)
                {
                    s.RemoveAll<IRepositorioPerfilCliente>(); s.AddSingleton<IRepositorioPerfilCliente>(Perfiles);
                    s.RemoveAll<IRepositorioDireccionesCliente>(); s.AddSingleton<IRepositorioDireccionesCliente>(Repo);
                }
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

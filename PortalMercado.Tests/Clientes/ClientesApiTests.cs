using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class ClientesApiTests
{
    [Theory]
    [InlineData("/api/mi-perfil")]
    [InlineData("/api/mis-direcciones")]
    [InlineData("/api/mis-direcciones/1")]
    [InlineData("/api/mis-consentimientos")]
    public async Task Lecturas_privadas_sin_sesion_responden_401_sin_redireccion(string ruta)
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); Assert.Null(r.Headers.Location);
        Assert.Equal(0, f.Direcciones.Lecturas); Assert.Equal(0, f.Consentimientos.Lecturas);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registro_publico_crea_mediante_contrato_sin_iniciar_sesion(bool confirmar)
    {
        await using var f = new Factory(); f.Registro.Resultado = new(EstadoRegistroCliente.Creado, confirmar);
        using var c = f.Cliente(); await Csrf(c);
        var r = await c.PostAsJsonAsync("/api/clientes/registro", ClientesPrueba.Registro());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(confirmar, json.GetProperty("requiereConfirmacionCorreo").GetBoolean());
        Assert.Equal(1, f.Registro.Llamadas); Assert.Equal(0, f.Registro.Alta!.Operacion.IdUsuario);
        Assert.DoesNotContain("Compra-Prueba2026!", await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Registro_sin_csrf_no_llega_a_persistencia()
    {
        await using var f = new Factory(); using var c = f.Cliente();
        var r = await c.PostAsJsonAsync("/api/clientes/registro", ClientesPrueba.Registro());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Registro.Llamadas);
    }
    [Fact]
    public async Task Correo_repetido_recibe_rechazo_generico_sin_secretos()
    {
        await using var f = new Factory(); f.Registro.Resultado = new(EstadoRegistroCliente.NoDisponible);
        using var c = f.Cliente(); await Csrf(c);
        var r = await c.PostAsJsonAsync("/api/clientes/registro", ClientesPrueba.Registro());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("comprador@example.invalid", html); Assert.DoesNotContain("Compra-Prueba2026!", html);
        Assert.Contains("traceId", html);
    }
    [Fact]
    public async Task Registro_no_acepta_rol_o_propietario_en_JSON()
    {
        await using var f = new Factory(); using var c = f.Cliente(); await Csrf(c);
        var datos = JsonSerializer.SerializeToNode(ClientesPrueba.Registro())!;
        datos["IdUsuario"] = 999; datos["Rol"] = "ADMIN_GENERAL";
        var r = await c.PostAsJsonAsync("/api/clientes/registro", datos);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Registro.Llamadas);
    }
    [Fact]
    public async Task Perfil_privado_se_consulta_sin_campos_de_seguridad()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/api/mi-perfil");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(77, f.Perfiles.UsuarioConsultado);
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
    }
    [Fact]
    public async Task Perfil_se_actualiza_por_usuario_de_sesion()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PutAsJsonAsync("/api/mi-perfil", new ClientePerfilDatos { Apellidos = " Cotrina " });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode); Assert.Equal(77, f.Perfiles.UsuarioGuardado);
        Assert.Equal("Cotrina", f.Perfiles.Datos!.Apellidos);
    }
    [Theory]
    [InlineData("correo")]
    [InlineData("idUsuario")]
    [InlineData("idCliente")]
    public async Task Perfil_no_admite_campos_fuera_del_DTO(string campo)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PutAsJsonAsync("/api/mi-perfil", new Dictionary<string,object?> { [campo] = "999" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Null(f.Perfiles.UsuarioGuardado);
    }
    [Fact]
    public async Task Crear_direccion_devuelve_201_y_ubicacion_de_recurso_propio()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsJsonAsync("/api/mis-direcciones", ClientesPrueba.Direccion());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.EndsWith("/api/mis-direcciones/2", r.Headers.Location!.OriginalString);
        Assert.Equal(77, f.Direcciones.Usuario); Assert.Equal("crear", f.Direcciones.Accion);
    }
    [Fact]
    public async Task Direccion_propia_puede_consultarse_por_id()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/api/mis-direcciones/1");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idDireccion").GetInt64());
    }
    [Fact]
    public async Task Direccion_ajena_no_aparece_en_la_respuesta()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/api/mis-direcciones/999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("Calle de prueba", await r.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("PUT", "/api/mis-direcciones/999")]
    [InlineData("DELETE", "/api/mis-direcciones/999")]
    [InlineData("PUT", "/api/mis-direcciones/999/principal")]
    public async Task Escritura_sobre_direccion_ajena_se_rechaza(string metodo, string ruta)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await Enviar(c, metodo, ruta, ClientesPrueba.Direccion());
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); Assert.Equal(0, f.Direcciones.Escrituras);
    }
    [Theory]
    [InlineData("PUT", "/api/mis-direcciones/1", "actualizar")]
    [InlineData("DELETE", "/api/mis-direcciones/1", "eliminar")]
    [InlineData("PUT", "/api/mis-direcciones/1/principal", "principal")]
    public async Task Escrituras_propias_delegan_una_operacion_con_actor_del_servidor(string metodo, string ruta, string accion)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await Enviar(c, metodo, ruta, ClientesPrueba.Direccion());
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode); Assert.Equal(1, f.Direcciones.Escrituras);
        Assert.Equal(77, f.Direcciones.Operacion!.IdUsuario); Assert.Equal(accion, f.Direcciones.Accion);
    }
    [Theory]
    [InlineData("PUT", "/api/mi-perfil")]
    [InlineData("POST", "/api/mis-direcciones")]
    [InlineData("PUT", "/api/mis-direcciones/1")]
    [InlineData("DELETE", "/api/mis-direcciones/1")]
    [InlineData("PUT", "/api/mis-direcciones/1/principal")]
    [InlineData("POST", "/api/mis-consentimientos")]
    public async Task Mutaciones_sin_csrf_son_rechazadas_antes_de_guardar(string metodo, string ruta)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var r = await Enviar(c, metodo, ruta, new { });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Vuelve a preparar", await r.Content.ReadAsStringAsync());
        Assert.Equal(0, f.Direcciones.Escrituras); Assert.Equal(0, f.Consentimientos.Escrituras);
        Assert.Null(f.Perfiles.UsuarioGuardado);
    }
    [Fact]
    public async Task Consentimientos_se_listan_sin_IP_ni_propietarios_de_otros_clientes()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.GetAsync("/api/mis-consentimientos");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.Equal(77, f.Consentimientos.Usuario);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("127.0.0.1", texto); Assert.DoesNotContain("idCliente", texto);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Marketing_se_registra_como_decision_explicita(bool aceptado)
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsJsonAsync("/api/mis-consentimientos", new ClienteConsentimientoDatos
            { Tipo = "PROMOCIONES", VersionDocumento = "test-promociones-1", Aceptado = aceptado });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Equal(77, f.Consentimientos.Usuario); Assert.Equal(aceptado, f.Consentimientos.Guardado!.Aceptado);
    }
    [Fact]
    public async Task Consentimiento_sin_decision_JSON_no_se_infiere_como_retiro()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsJsonAsync("/api/mis-consentimientos", new { tipo = "PROMOCIONES", versionDocumento = "test-promociones-1" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Consentimientos.Escrituras);
    }
    [Fact]
    public async Task Cambio_de_marketing_no_altera_la_aceptacion_de_privacidad()
    {
        await using var f = new Factory(); using var c = await Entrar(f);
        var r = await c.PostAsJsonAsync("/api/mis-consentimientos", new ClienteConsentimientoDatos
            { Tipo = "PRIVACIDAD", VersionDocumento = "test-privacidad-1", Aceptado = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal(0, f.Consentimientos.Escrituras);
    }
    [Fact]
    public async Task Cuenta_sin_cliente_no_hereda_perfil_por_tener_rol_de_acceso()
    {
        await using var f = new Factory(); f.Perfiles.Perfil = null; using var c = await Entrar(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/mi-perfil")).StatusCode);
    }
    [Fact]
    public async Task Sesion_revocada_no_sigue_leyendo_datos_privados()
    {
        await using var f = new Factory(); using var c = await Entrar(f); f.Auth.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/mi-perfil")).StatusCode);
        Assert.Null(f.Perfiles.UsuarioConsultado);
    }
    [Fact]
    public async Task Sin_proveedores_registro_y_perfil_responden_503_controlado()
    {
        await using var f = new Factory(pendiente: true); using var c = await Entrar(f);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/api/mi-perfil")).StatusCode);
        var r = await c.PostAsJsonAsync("/api/clientes/registro", ClientesPrueba.Registro());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.DoesNotContain("ConnectionStrings", await r.Content.ReadAsStringAsync());
    }
    private static async Task<HttpClient> Entrar(Factory f)
    {
        var c = f.Cliente(); await Csrf(c);
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest { Correo = "v@example.invalid", Password = "PruebaSegura123!" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        await Csrf(c); // El token anónimo no sirve después de autenticar otra identidad.
        return c;
    }
    private static async Task Csrf(HttpClient c)
    {
        var json = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static Task<HttpResponseMessage> Enviar(HttpClient c, string metodo, string ruta, object datos) =>
        c.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta) { Content = JsonContent.Create(datos) });
    private sealed class Factory(bool pendiente = false) : WebApplicationFactory<PortalMercado.API.Controllers.AuthController>
    {
        public ControlledAuth Auth { get; } = new();
        public RegistroClientesPrueba Registro { get; } = new();
        public PerfilClientesPrueba Perfiles { get; } = new() { Perfil = ClientesPrueba.Perfil(usuario: 77) };
        public DireccionesClientePrueba Direcciones { get; } = new();
        public ConsentimientosClientePrueba Consentimientos { get; } = new()
        { Datos = [new("PROMOCIONES", "test-promociones-1", true, RelojClientes.Fecha)] };
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PortalMercado", "Server=127.0.0.1;Database=Ficticia;Integrated Security=True;");
            b.UseSetting("Tiendas:SqlLocal", "false"); b.UseSetting("Tiendas:Demostracion", "false");
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IAuthService>(); s.AddSingleton<IAuthService>(Auth);
                s.RemoveAll<DocumentacionRegistro>(); s.RemoveAll<DocumentosClienteVigentes>();
                var terminos = new DocumentoRegistro("test-terminos-1", "Texto de prueba");
                var privacidad = new DocumentoRegistro("test-privacidad-1", "Texto de prueba");
                var promos = new DocumentoRegistro("test-promociones-1", "Texto de prueba");
                s.AddSingleton(new DocumentacionRegistro(terminos, privacidad, promos)); s.AddSingleton(ClientesPrueba.Documentos());
                if (!pendiente)
                {
                    s.RemoveAll<IRepositorioRegistroCliente>(); s.AddSingleton<IRepositorioRegistroCliente>(Registro);
                    s.RemoveAll<IRepositorioPerfilCliente>(); s.AddSingleton<IRepositorioPerfilCliente>(Perfiles);
                    s.RemoveAll<IRepositorioDireccionesCliente>(); s.AddSingleton<IRepositorioDireccionesCliente>(Direcciones);
                    s.RemoveAll<IRepositorioConsentimientosCliente>(); s.AddSingleton<IRepositorioConsentimientosCliente>(Consentimientos);
                }
            });
        }
        public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
}

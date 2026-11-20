using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Clientes.Demostracion;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortalMercado.Tests.Clientes;

public sealed class SeguridadClienteDetallesTests
{
    private const string Inicial = "Compra-Prueba2026!", Nueva = "Nueva-Prueba2026!";
    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = RelojClientes.Fecha;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
    private sealed class Entrega : IRecoveryDelivery, IAccountDelivery
    {
        public readonly List<(string Correo, Uri Enlace)> Recuperaciones = [];
        public readonly List<(string Correo, string Cuerpo)> Cambios = [];
        public bool Fallar { get; set; }
        public void CheckConfiguration() { }
        public Task DeliverAsync(string recipient, Uri resetLink, CancellationToken ct)
        {
            Recuperaciones.Add((recipient, resetLink));
            if (Fallar) throw new PasswordFailure(503, PasswordMessages.Unavailable);
            return Task.CompletedTask;
        }
        public Task DeliverMessageAsync(string recipient, string subject, string body, CancellationToken ct)
        {
            Cambios.Add((recipient, body));
            if (Fallar) throw new PasswordFailure(503, PasswordMessages.Unavailable);
            return Task.CompletedTask;
        }
        public string TokenPassword => Recuperaciones.Last().Enlace.Query.Split('=')[1];
        public string TokenCorreo => Regex.Match(Cambios.Last().Cuerpo, @"token=([A-Za-z0-9_-]{43})").Groups[1].Value;
    }
    private sealed class Hash(IPasswordHasher<Usuario> h) : IHashPasswordCliente
    { public string Crear(string nombre, string correo, string password) => h.HashPassword(new Usuario(), password); }
    private sealed class Caso
    {
        public readonly Reloj Reloj = new();
        public readonly Entrega Entrega = new();
        public readonly ClientesDemostracion Datos;
        public readonly SeguridadClientesDemostracion Seguridad;
        private readonly IPasswordHasher<Usuario> hasher;
        public Caso(string provider = "Pickup", string origen = "https://localhost:7275/")
        {
            var opciones = Options.Create(new PasswordHasherOptions { IterationCount = 210_000 });
            hasher = new PasswordHasher<Usuario>(opciones); Datos = new(Reloj, opciones);
            Seguridad = new(Datos, hasher, Entrega, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                { ["Recovery:Provider"] = provider, ["Recovery:WebBaseUrl"] = origen }).Build(), Entrega);
        }
        public async Task<AuthSession> Alta(string correo = "comprador@example.invalid")
        {
            var s = ClientesPrueba.Registro(); s.Correo = correo;
            await new RegistroClienteServicio(Datos, new Hash(hasher), ClientesPrueba.Documentos(), Reloj).RegistrarAsync(s, ClientesPrueba.Contexto(), default);
            return (await Login(correo))!.Session!;
        }
        public Task<LoginOutcome?> Login(string correo = "comprador@example.invalid", string password = Inicial, string host = "MVC") =>
            Seguridad.LoginAsync(new() { Correo = correo, Password = password }, host, "prueba", default);
        public Task Pedir() => Seguridad.RequestAsync(new() { Correo = "comprador@example.invalid" }, "prueba", default);
        public Task Reset(string token) => Seguridad.ResetAsync(new() { Token = token, NewPassword = Nueva, ConfirmPassword = Nueva }, "prueba", default);
        public Task Cambiar(AuthSession s, string actual = Inicial, long? actor = null, string host = "MVC") =>
            Seguridad.ChangeAsync(actor ?? s.Profile.IdUsuario, s.Credential, host, new() { CurrentPassword = actual, NewPassword = Nueva, ConfirmPassword = Nueva }, "prueba", default);
        public Task PedirCorreo(AuthSession s, string correo = "nuevo@example.invalid", string password = Inicial) =>
            Seguridad.SolicitarAsync(s.Credential, "MVC", new() { Correo = correo, ConfirmarCorreo = correo, PasswordActual = password }, "prueba", default);
        public Task Confirmar(AuthSession s, string token) => Seguridad.ConfirmarAsync(s.Credential, "MVC", new() { Token = token }, "prueba", default);
    }
    [Fact]
    public async Task Recuperacion_cambia_hash_revoca_ambos_hosts_y_no_se_reutiliza()
    {
        var c = new Caso(); var mvc = await c.Alta(); var api = (await c.Login(host: "API"))!.Session!;
        await c.Pedir(); var token = c.Entrega.TokenPassword; await c.Reset(token);
        Assert.Null(await c.Login()); Assert.NotNull(await c.Login(password: Nueva));
        Assert.Null(await c.Seguridad.ValidateAsync(mvc.Credential, "MVC", default));
        Assert.Null(await c.Seguridad.ValidateAsync(api.Credential, "API", default));
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(token))).Status);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(c.Datos.Eventos()));
        Assert.DoesNotContain(Nueva, JsonSerializer.Serialize(c.Datos.Eventos()));
    }
    [Fact]
    public async Task Cuenta_desconocida_recibe_mismo_tipo_de_entrega_sin_token_utilizable()
    {
        var c = new Caso(); await c.Pedir(); var falso = c.Entrega.TokenPassword;
        Assert.Equal("comprador@example.invalid", Assert.Single(c.Entrega.Recuperaciones).Correo);
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(falso))).Status);
        await c.Alta(); await c.Pedir(); Assert.Equal(2, c.Entrega.Recuperaciones.Count);
    }
    [Fact]
    public async Task Solicitud_mas_reciente_invalida_enlace_anterior()
    {
        var c = new Caso(); await c.Alta(); await c.Pedir(); var anterior = c.Entrega.TokenPassword; await c.Pedir();
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(anterior))).Status);
        await c.Reset(c.Entrega.TokenPassword);
    }
    [Fact]
    public async Task Enlace_caducado_no_cambia_password()
    {
        var c = new Caso(); await c.Alta(); await c.Pedir(); c.Reloj.Ahora = c.Reloj.Ahora.AddMinutes(15);
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(c.Entrega.TokenPassword))).Status);
        Assert.NotNull(await c.Login());
    }
    [Fact]
    public async Task Fallo_de_entrega_revoca_el_enlace_generado()
    {
        var c = new Caso(); await c.Alta(); c.Entrega.Fallar = true;
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Pedir());
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(c.Entrega.TokenPassword))).Status);
        Assert.NotNull(await c.Login());
    }
    [Fact]
    public async Task Peticiones_por_correo_se_limitan_en_memoria_y_reabren_ventana()
    {
        var c = new Caso(); await c.Alta(); for (int i = 0; i < 4; i++) await c.Pedir();
        Assert.Equal(3, c.Entrega.Recuperaciones.Count);
        c.Reloj.Ahora = c.Reloj.Ahora.AddMinutes(16); await c.Pedir(); Assert.Equal(4, c.Entrega.Recuperaciones.Count);
    }
    [Theory]
    [InlineData("Smtp", "https://localhost:7275/")]
    [InlineData("Pickup", "http://localhost:7275/")]
    [InlineData("Pickup", "https://localhost:7275/ruta")]
    [InlineData("Pickup", "https://usuario:clave@localhost:7275/")]
    [InlineData("Pickup", "https://localhost:7275/?host=externo")]
    public async Task Configuracion_insegura_no_genera_ni_entrega_enlace(string provider, string origen)
    {
        var c = new Caso(provider, origen); await c.Alta();
        Assert.Equal(503, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Pedir())).Status); Assert.Empty(c.Entrega.Recuperaciones);
    }
    [Fact]
    public async Task Dos_consumos_simultaneos_solo_cambian_una_vez()
    {
        var c = new Caso(); await c.Alta(); await c.Pedir(); var token = c.Entrega.TokenPassword;
        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        { try { await c.Reset(token); return true; } catch (PasswordFailure) { return false; } })));
        Assert.Single(resultados, x => x);
    }
    [Fact]
    public async Task Cambio_autenticado_revoca_tokens_y_sesiones_sin_borrar_perfil()
    {
        var c = new Caso(); var s = await c.Alta(); await c.Pedir(); var token = c.Entrega.TokenPassword;
        await c.Cambiar(s); Assert.NotNull(await c.Login(password: Nueva)); Assert.Null(await c.Login());
        Assert.NotNull(await c.Datos.ObtenerPorUsuarioAsync(s.Profile.IdUsuario, default));
        Assert.Null(await c.Seguridad.ValidateAsync(s.Credential, "MVC", default));
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(token));
    }
    [Theory]
    [InlineData(true, "MVC")]
    [InlineData(false, "API")]
    public async Task Cambio_rechaza_actor_o_host_ajeno(bool otroActor, string host)
    {
        var c = new Caso(); var s = await c.Alta();
        Assert.Equal(401, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Cambiar(s, actor: otroActor ? 7L : s.Profile.IdUsuario, host: host))).Status);
        Assert.NotNull(await c.Login());
    }
    [Fact]
    public async Task Cinco_passwords_actuales_incorrectos_revocan_sesion()
    {
        var c = new Caso(); var s = await c.Alta();
        for (int i = 0; i < 5; i++) await Assert.ThrowsAsync<PasswordFailure>(() => c.Cambiar(s, "Incorrecta!2026"));
        Assert.Null(await c.Seguridad.ValidateAsync(s.Credential, "MVC", default)); Assert.Null(await c.Login());
    }
    [Fact]
    public async Task Correo_no_cambia_hasta_confirmar_y_conserva_cliente_direcciones_consentimientos()
    {
        var c = new Caso(); var s = await c.Alta(); var id = s.Profile.IdUsuario;
        await c.Datos.CrearPorUsuarioAsync(id, ClientesPrueba.Direccion(), new(id, c.Reloj.Ahora, "prueba", null), default);
        await c.PedirCorreo(s); Assert.Equal("comprador@example.invalid", (await c.Datos.ObtenerPorUsuarioAsync(id, default))!.Correo);
        await c.Confirmar(s, c.Entrega.TokenCorreo);
        Assert.Null(await c.Login()); var nueva = (await c.Login("nuevo@example.invalid"))!.Session!;
        Assert.Equal(id, nueva.Profile.IdUsuario); Assert.Single(await c.Datos.ListarPorUsuarioAsync(id, default));
        Assert.Equal(3, (await ((IRepositorioConsentimientosCliente)c.Datos).ListarPorUsuarioAsync(id, default)).Length);
        Assert.Null(await c.Seguridad.ValidateAsync(s.Credential, "MVC", default));
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(nueva, c.Entrega.TokenCorreo));
    }
    [Fact]
    public async Task Otra_cuenta_no_confirma_enlace_de_cambio_ajeno()
    {
        var c = new Caso(); var a = await c.Alta(); var b = await c.Alta("otra@example.invalid"); await c.PedirCorreo(a);
        Assert.Equal(400, (await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(b, c.Entrega.TokenCorreo))).Status);
        await c.Confirmar(a, c.Entrega.TokenCorreo);
    }
    [Fact]
    public async Task Cambio_correo_caducado_o_con_password_incorrecta_no_modifica_identidad()
    {
        var c = new Caso(); var a = await c.Alta();
        await Assert.ThrowsAsync<PasswordFailure>(() => c.PedirCorreo(a, password: "Incorrecta2026!")); Assert.Empty(c.Entrega.Cambios);
        await c.PedirCorreo(a); c.Reloj.Ahora = c.Reloj.Ahora.AddMinutes(16);
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(a, c.Entrega.TokenCorreo)); Assert.NotNull(await c.Login());
    }
    [Fact]
    public async Task Token_de_password_no_confirma_correo_y_token_correo_no_restablece_password()
    {
        var c = new Caso(); var s = await c.Alta(); await c.Pedir(); var password = c.Entrega.TokenPassword; await c.PedirCorreo(s);
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Reset(c.Entrega.TokenCorreo));
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(s, password));
    }
    [Fact]
    public async Task Dos_cuentas_compitiendo_por_correo_solo_una_lo_confirma()
    {
        var c = new Caso(); var a = await c.Alta(); var b = await c.Alta("otra@example.invalid");
        await c.PedirCorreo(a); var ta = c.Entrega.TokenCorreo; await c.PedirCorreo(b); var tb = c.Entrega.TokenCorreo;
        var resultados = await Task.WhenAll(new[] { (a, ta), (b, tb) }.Select(par => Task.Run(async () =>
        { try { await c.Confirmar(par.Item1, par.Item2); return true; } catch (PasswordFailure) { return false; } })));
        Assert.Single(resultados, x => x);
    }
    [Fact]
    public async Task Fallo_entrega_correo_y_cambio_password_invalidan_sus_enlaces()
    {
        var c = new Caso(); var s = await c.Alta(); c.Entrega.Fallar = true;
        await Assert.ThrowsAsync<PasswordFailure>(() => c.PedirCorreo(s)); var fallido = c.Entrega.TokenCorreo;
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(s, fallido));
        c.Entrega.Fallar = false; await c.PedirCorreo(s); var token = c.Entrega.TokenCorreo; await c.Cambiar(s);
        var nueva = (await c.Login(password: Nueva))!.Session!; await Assert.ThrowsAsync<PasswordFailure>(() => c.Confirmar(nueva, token));
    }
    [Theory]
    [InlineData("persona@example.com")]
    [InlineData("comprador@example.invalid")]
    [InlineData("invalido")]
    public async Task Correo_invalido_real_o_ya_ocupado_no_genera_enlace(string correo)
    {
        var c = new Caso(); var s = await c.Alta();
        await Assert.ThrowsAsync<PasswordFailure>(() => c.PedirCorreo(s, correo)); Assert.Empty(c.Entrega.Cambios);
    }
    [Fact]
    public async Task Password_debil_cancelacion_y_nueva_instancia_no_permiten_reset()
    {
        var c = new Caso(); await c.Alta(); await c.Pedir(); var token = c.Entrega.TokenPassword;
        await Assert.ThrowsAsync<PasswordFailure>(() => c.Seguridad.ResetAsync(new() { Token = token, NewPassword = "debil", ConfirmPassword = "debil" }, "prueba", default));
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => c.Seguridad.ResetAsync(new() { Token = token, NewPassword = Nueva, ConfirmPassword = Nueva }, "prueba", ct.Token));
        await Assert.ThrowsAsync<PasswordFailure>(() => new Caso().Reset(token)); Assert.NotNull(await c.Login());
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Clientes;
using PortalMercado.Infrastructure.Clientes.Demostracion;

namespace PortalMercado.Tests.Clientes;

public sealed class ClientesDemostracionTests
{
    private static readonly IOptions<PasswordHasherOptions> Opciones = Options.Create(new PasswordHasherOptions { IterationCount = 210_000 });
    private static ClientesDemostracion Almacen() => new(new RelojClientes(), Opciones);
    private static RegistroClienteServicio Registro(ClientesDemostracion datos) => new(datos,
        new HashDemo(new PasswordHasher<Usuario>(Opciones)), ClientesPrueba.Documentos(), new RelojClientes());
    private static async Task<long> Alta(ClientesDemostracion datos, string correo = "cliente1@example.invalid")
    {
        var solicitud = ClientesPrueba.Registro(); solicitud.Correo = correo;
        await Registro(datos).RegistrarAsync(solicitud, ClientesPrueba.Contexto(), default);
        return (await Login(datos, correo))!.Session!.Profile.IdUsuario;
    }
    private static Task<LoginOutcome?> Login(ClientesDemostracion datos, string correo = "cliente1@example.invalid", string password = "Compra-Prueba2026!", string host = "MVC") =>
        datos.LoginAsync(new() { Correo = correo, Password = password }, host, "demo-prueba", new PasswordHasher<Usuario>(Opciones), default);
    private static ClienteOperacion Operacion(long actor) => new(actor, RelojClientes.Fecha, "demo-prueba", "127.0.0.1");

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Activacion_fuera_de_Development_se_rechaza(string ambiente)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Clientes:Demostracion"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRegistroClientes(config, new Ambiente(ambiente)));
    }
    [Fact]
    public void Modo_desactivado_conserva_proveedores_pendientes()
    {
        var s = new ServiceCollection(); s.AddRegistroClientes(new ConfigurationBuilder().Build());
        using var p = s.BuildServiceProvider();
        Assert.False(p.GetRequiredService<ClientesModo>().Demostracion);
        Assert.IsType<RegistroClientePendiente>(p.GetRequiredService<IRepositorioRegistroCliente>());
    }
    [Fact]
    public async Task Registro_permite_entrar_y_no_concede_roles_permisos_o_negocios()
    {
        var datos = Almacen(); var id = await Alta(datos); var login = (await Login(datos))!;
        Assert.True(id > 0); Assert.NotNull(login.Session); Assert.Null(login.Mfa);
        Assert.Empty(login.Session.Profile.Roles); Assert.Empty(login.Session.Profile.Permisos); Assert.Empty(login.Session.Profile.Negocios);
        Assert.NotNull(await datos.ObtenerPorUsuarioAsync(id, default));
        Assert.DoesNotContain("Compra-Prueba2026!", System.Text.Json.JsonSerializer.Serialize(datos.Eventos()));
    }
    [Fact]
    public async Task Correo_real_no_se_registra()
    {
        var datos = Almacen(); var solicitud = ClientesPrueba.Registro(); solicitud.Correo = "persona@example.com";
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => Registro(datos).RegistrarAsync(solicitud, ClientesPrueba.Contexto(), default))).Status);
        Assert.Empty(datos.Eventos());
    }
    [Fact]
    public async Task Altas_simultaneas_del_mismo_correo_solo_crean_una_cuenta_en_memoria()
    {
        var datos = Almacen();
        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            try { await Registro(datos).RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default); return true; }
            catch (AccessFailure ex) when (ex.Status == 400) { return false; }
        })));
        Assert.Single(resultados, x => x); Assert.Single(datos.Eventos(), x => x.Accion == "ALTA_DEMOSTRACION");
    }
    [Fact]
    public async Task Sesion_pertenece_al_host_y_logout_la_revoca()
    {
        var datos = Almacen(); await Alta(datos); var token = (await Login(datos))!.Session!.Credential;
        Assert.Null(await datos.ValidateAsync(token, "API", default));
        Assert.NotNull(await datos.ValidateAsync(token, "MVC", default));
        await datos.LogoutAsync(token, "MVC", "demo-prueba", default);
        Assert.Null(await datos.ValidateAsync(token, "MVC", default));
    }
    [Fact]
    public async Task Cinco_intentos_incorrectos_bloquean_temporalmente_la_cuenta()
    {
        var datos = Almacen(); await Alta(datos);
        for (var i = 0; i < 5; i++) Assert.Null(await Login(datos, password: "Incorrecta2026!"));
        Assert.Null(await Login(datos));
    }
    [Fact]
    public async Task Nueva_instancia_no_conserva_clientes_ni_sesiones()
    {
        var anterior = Almacen(); var id = await Alta(anterior); var token = (await Login(anterior))!.Session!.Credential;
        var nuevo = Almacen(); Assert.Null(await nuevo.ObtenerPorUsuarioAsync(id, default));
        Assert.Null(await nuevo.ValidateAsync(token, "MVC", default));
    }
    [Fact]
    public async Task Perfil_y_direcciones_se_copian_sin_referencias_mutables_hacia_almacen()
    {
        var datos = Almacen(); var id = await Alta(datos); var perfil = new ClientePerfilDatos { Apellidos = "Prueba" };
        await datos.ActualizarPorUsuarioAsync(id, perfil, Operacion(id), default); perfil.Apellidos = "Cambio externo";
        var leido = (await datos.ObtenerPorUsuarioAsync(id, default))!; Assert.Equal("Prueba", leido.Datos.Apellidos);
        leido.Datos.Apellidos = "Otro cambio";
        Assert.Equal("Prueba", (await datos.ObtenerPorUsuarioAsync(id, default))!.Datos.Apellidos);
        var creada = await datos.CrearPorUsuarioAsync(id, ClientesPrueba.Direccion(), Operacion(id), default);
        creada.Datos.Direccion = "Mutación externa";
        Assert.Equal("Calle de prueba 123", Assert.Single(await datos.ListarPorUsuarioAsync(id, default)).Datos.Direccion);
    }
    [Fact]
    public async Task Direccion_ajena_no_puede_editarse_ni_marcarse_como_principal()
    {
        var datos = Almacen(); var a = await Alta(datos); var b = await Alta(datos, "cliente2@example.invalid");
        var dir = await datos.CrearPorUsuarioAsync(a, ClientesPrueba.Direccion(), Operacion(a), default);
        Assert.Equal(404, (await Assert.ThrowsAsync<AccessFailure>(() => datos.ActualizarPorUsuarioAsync(b, dir.IdDireccion, ClientesPrueba.Direccion(), Operacion(b), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<AccessFailure>(() => datos.MarcarPrincipalPorUsuarioAsync(b, dir.IdDireccion, Operacion(b), default))).Status);
        Assert.Empty(await datos.ListarPorUsuarioAsync(b, default));
    }
    [Fact]
    public async Task Marcados_simultaneos_dejan_una_sola_principal_en_la_simulacion()
    {
        var datos = Almacen(); var id = await Alta(datos);
        var a = await datos.CrearPorUsuarioAsync(id, ClientesPrueba.Direccion(), Operacion(id), default);
        var b = await datos.CrearPorUsuarioAsync(id, ClientesPrueba.Direccion(), Operacion(id), default);
        await Task.WhenAll(Task.Run(() => datos.MarcarPrincipalPorUsuarioAsync(id, a.IdDireccion, Operacion(id), default)),
            Task.Run(() => datos.MarcarPrincipalPorUsuarioAsync(id, b.IdDireccion, Operacion(id), default)));
        Assert.Single(await datos.ListarPorUsuarioAsync(id, default), x => x.Principal);
    }
    [Fact]
    public async Task Eliminar_principal_no_selecciona_otra_y_no_reactiva_la_baja()
    {
        var datos = Almacen(); var id = await Alta(datos);
        var a = await datos.CrearPorUsuarioAsync(id, ClientesPrueba.Direccion(), Operacion(id), default);
        await datos.MarcarPrincipalPorUsuarioAsync(id, a.IdDireccion, Operacion(id), default);
        await datos.EliminarPorUsuarioAsync(id, a.IdDireccion, Operacion(id), default);
        Assert.Empty(await datos.ListarPorUsuarioAsync(id, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<AccessFailure>(() => datos.MarcarPrincipalPorUsuarioAsync(id, a.IdDireccion, Operacion(id), default))).Status);
    }
    [Fact]
    public async Task Historial_preserva_decisiones_y_desempata_instantes_iguales()
    {
        var datos = Almacen(); var id = await Alta(datos); var repo = (IRepositorioConsentimientosCliente)datos;
        var inicial = await repo.ListarPorUsuarioAsync(id, default);
        foreach (var aceptado in new[] { true, false })
            await repo.RegistrarPorUsuarioAsync(id, new() { Tipo = "PROMOCIONES", VersionDocumento = "test-promociones-1", Aceptado = aceptado }, Operacion(id), default);
        var historial = await repo.ListarPorUsuarioAsync(id, default);
        Assert.Equal(inicial.Length + 2, historial.Length); Assert.False(historial[0].Aceptado);
        Assert.True(historial[0].RegistradoEn > historial[1].RegistradoEn);
    }
    [Fact]
    public async Task Metadatos_de_actor_incorrecto_se_rechazan_al_escribir()
    {
        var datos = Almacen(); var id = await Alta(datos);
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => datos.ActualizarPorUsuarioAsync(id, new ClientePerfilDatos(), Operacion(99), default))).Status);
    }
    [Fact]
    public async Task Recuperacion_no_envia_correo_ni_accede_a_SQL_en_demostracion()
    {
        var seguridad = new SeguridadClientesDemostracion(Almacen(), new PasswordHasher<Usuario>(Opciones));
        Assert.Equal(503, (await Assert.ThrowsAsync<PasswordFailure>(() => seguridad.RequestAsync(new() { Correo = "cliente1@example.invalid" }, "demo-prueba", default))).Status);
        Assert.Null(await seguridad.CompleteAsync("inexistente", "123456", "MVC", "demo-prueba", default));
    }
    [Fact]
    public async Task Sesion_expirada_no_sigue_validandose()
    {
        var reloj = new RelojMovil(); var datos = new ClientesDemostracion(reloj, Opciones);
        await Alta(datos); var token = (await Login(datos))!.Session!.Credential;
        reloj.Ahora = reloj.Ahora.AddMinutes(31);
        Assert.Null(await datos.ValidateAsync(token, "MVC", default));
    }
    private sealed class RelojMovil : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = RelojClientes.Fecha;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
    private sealed class HashDemo(IPasswordHasher<Usuario> hasher) : IHashPasswordCliente
    { public string Crear(string nombre, string correo, string password) => hasher.HashPassword(new Usuario { Nombre = nombre, Correo = correo }, password); }
    private sealed class Ambiente(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Prueba";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

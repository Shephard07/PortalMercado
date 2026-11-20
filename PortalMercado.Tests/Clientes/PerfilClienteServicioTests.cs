using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

public sealed class PerfilClienteServicioTests
{
    private static (PerfilClienteServicio Servicio, AuthClientesPrueba Auth, PerfilClientesPrueba Repo) Crear()
    {
        var auth = new AuthClientesPrueba(); var repo = new PerfilClientesPrueba();
        return (new(auth, repo, new RelojClientes()), auth, repo);
    }
    [Theory]
    [InlineData("API")]
    [InlineData("MVC")]
    public async Task Consulta_el_perfil_del_usuario_de_la_sesion(string host)
    {
        var (servicio, auth, repo) = Crear();
        var perfil = await servicio.ObtenerAsync("credencial-servidor", host, default);
        Assert.Equal(7, perfil.IdUsuario); Assert.Equal(21, perfil.IdCliente);
        Assert.Equal(7L, repo.UsuarioConsultado); Assert.Equal(1, auth.Llamadas);
    }
    [Fact]
    public async Task Actualiza_solo_datos_del_cliente_con_actor_verificado()
    {
        var (servicio, _, repo) = Crear();
        var datos = new ClientePerfilDatos { Apellidos = " Cotrina Ortiz ", Celular = " 999000111 ", TipoDocumento = "DNI", NumeroDocumento = " 12345678 " };
        await servicio.ActualizarAsync("sesion", "MVC", datos, ClientesPrueba.Contexto(), default);
        Assert.Equal(7L, repo.UsuarioGuardado); Assert.Equal(7, repo.Operacion!.IdUsuario);
        Assert.Equal(RelojClientes.Fecha, repo.Operacion.FechaUtc);
        Assert.Equal("Cotrina Ortiz", repo.Datos!.Apellidos); Assert.Equal("DNI", repo.Datos.TipoDocumento);
        Assert.Equal("12345678", repo.Datos.NumeroDocumento); Assert.Equal("999000111", repo.Datos.Celular);
        Assert.Equal(" Cotrina Ortiz ", datos.Apellidos); // No mutar el DTO recibido.
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sesion_invalida_no_lee_ni_actualiza_datos(bool escribir)
    {
        var (servicio, auth, repo) = Crear(); auth.Perfil = null;
        Task Accion() => escribir ? servicio.ActualizarAsync("sesion", "MVC", new(), ClientesPrueba.Contexto(), default)
            : servicio.ObtenerAsync("sesion", "MVC", default);
        Assert.Equal(401, (await Assert.ThrowsAsync<AccessFailure>(Accion)).Status);
        Assert.Null(repo.UsuarioConsultado); Assert.Null(repo.UsuarioGuardado);
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Sin_credencial_no_invoca_autenticacion(string credencial)
    {
        var (servicio, auth, repo) = Crear();
        Assert.Equal(401, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.ObtenerAsync(credencial, "MVC", default))).Status);
        Assert.Equal(0, auth.Llamadas); Assert.Null(repo.UsuarioConsultado);
    }
    [Theory]
    [InlineData("OTRO")]
    [InlineData("")]
    public async Task Rechaza_host_no_reconocido(string host)
    {
        var (servicio, auth, _) = Crear();
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ObtenerAsync("sesion", host, default));
        Assert.Equal(0, auth.Llamadas);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cuenta_sin_cliente_no_obtiene_perfil_por_ser_administrador(bool escribir)
    {
        var (servicio, auth, repo) = Crear(); repo.Perfil = null;
        auth.Perfil = new AuthProfile(7, "admin@example.invalid", "Admin prueba", ["ADMIN_GENERAL"], [], []);
        Task Accion() => escribir ? servicio.ActualizarAsync("sesion", "API", new(), ClientesPrueba.Contexto(), default)
            : servicio.ObtenerAsync("sesion", "API", default);
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(Accion)).Status);
        Assert.Null(repo.UsuarioGuardado);
    }
    [Theory]
    [InlineData(99, 21)]
    [InlineData(7, 0)]
    public async Task Rechaza_un_perfil_de_otro_usuario_o_sin_identidad_valida(long usuario, long cliente)
    {
        var (servicio, _, repo) = Crear(); repo.Perfil = ClientesPrueba.Perfil(usuario, cliente);
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.ActualizarAsync("sesion", "MVC", new(), ClientesPrueba.Contexto(), default))).Status);
        Assert.Null(repo.UsuarioGuardado);
    }
    [Fact]
    public async Task Datos_invalidos_no_se_guardan()
    {
        var (servicio, _, repo) = Crear();
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.ActualizarAsync("sesion", "MVC", new() { Celular = new('1', 31) }, ClientesPrueba.Contexto(), default))).Status);
        Assert.Null(repo.UsuarioGuardado);
    }
}

using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class DireccionClienteServicioTests
{
    private static (DireccionClienteServicio Servicio, DireccionesClientePrueba Repo, AuthClientesPrueba Auth, PerfilClientesPrueba Perfiles) Crear()
    {
        var auth = new AuthClientesPrueba(); var perfiles = new PerfilClientesPrueba(); var repo = new DireccionesClientePrueba();
        return (new(new PerfilClienteServicio(auth, perfiles), repo, new RelojClientes()), repo, auth, perfiles);
    }
    [Fact]
    public async Task Listado_resuelve_usuario_autenticado_y_excluye_inactivas()
    {
        var (s, repo, _, _) = Crear();
        repo.Datos = [Direccion(1, true), Direccion(2, false)];
        Assert.Equal(1, Assert.Single(await s.ListarAsync("sesion", "MVC", default)).IdDireccion);
        Assert.Equal(7, repo.Usuario);
    }
    [Fact]
    public async Task Crear_limpia_datos_y_usa_actor_y_fecha_del_servidor()
    {
        var (s, repo, _, _) = Crear();
        await s.CrearAsync("sesion", "MVC", ClientesPrueba.Direccion(), ClientesPrueba.Contexto(), default);
        Assert.Equal("Calle de prueba 123", repo.Guardado!.Direccion);
        Assert.Equal(7, repo.Operacion!.IdUsuario); Assert.Equal(RelojClientes.Fecha, repo.Operacion.FechaUtc);
        Assert.Equal(1, repo.Escrituras); Assert.Equal("crear", repo.Accion);
    }
    [Theory]
    [InlineData("actualizar")]
    [InlineData("eliminar")]
    [InlineData("principal")]
    public async Task Mutacion_propia_hace_una_sola_llamada_por_usuario(string accion)
    {
        var (s, repo, _, _) = Crear();
        await Ejecutar(s, accion, 1);
        Assert.Equal(1, repo.Escrituras); Assert.Equal(7, repo.Usuario);
        Assert.Equal(accion, repo.Accion); Assert.Equal(1, repo.Id);
        Assert.Equal(7, repo.Operacion!.IdUsuario);
    }
    [Theory]
    [InlineData("actualizar")]
    [InlineData("eliminar")]
    [InlineData("principal")]
    public async Task Direccion_ajena_no_se_modifica_ni_revela_su_existencia(string accion)
    {
        var (s, repo, _, _) = Crear();
        var ex = await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, accion, 999));
        Assert.Equal(404, ex.Status); Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("actualizar")]
    [InlineData("eliminar")]
    [InlineData("principal")]
    public async Task Direccion_inactiva_no_puede_reutilizarse(string accion)
    {
        var (s, repo, _, _) = Crear(); repo.Datos = [Direccion(1, false)];
        Assert.Equal(404, (await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, accion, 1))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("actualizar", 0)]
    [InlineData("principal", -1)]
    [InlineData("eliminar", 0)]
    public async Task Identificador_invalido_no_se_envia_a_escritura(string accion, long id)
    {
        var (s, repo, _, _) = Crear();
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, accion, id))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("listar")]
    [InlineData("crear")]
    [InlineData("actualizar")]
    [InlineData("eliminar")]
    [InlineData("principal")]
    public async Task Sesion_invalida_no_consulta_direcciones(string accion)
    {
        var (s, repo, auth, _) = Crear(); auth.Perfil = null;
        Assert.Equal(401, (await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, accion, 1))).Status);
        Assert.Equal(0, repo.Lecturas); Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Perfil_ausente_o_ajeno_bloquea_incluso_operacion_de_principal(bool ajeno)
    {
        var (s, repo, _, perfiles) = Crear(); perfiles.Perfil = ajeno ? ClientesPrueba.Perfil(usuario: 99) : null;
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, "principal", 1))).Status);
        Assert.Equal(0, repo.Lecturas); Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("doble-principal")]
    [InlineData("duplicada")]
    [InlineData("id-invalido")]
    [InlineData("principal-inactiva")]
    public async Task Listado_inconsistente_no_se_presenta_como_correcto(string caso)
    {
        var (s, repo, _, _) = Crear();
        repo.Datos = caso switch
        {
            "doble-principal" => [Direccion(1, true, true), Direccion(2, true, true)],
            "duplicada" => [Direccion(1, true), Direccion(1, true)],
            "id-invalido" => [Direccion(0, true)],
            _ => [Direccion(1, false, true)]
        };
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => s.ListarAsync("sesion", "MVC", default))).Status);
    }
    [Fact]
    public async Task Conflicto_transaccional_se_propaga_sin_informar_exito()
    {
        var (s, repo, _, _) = Crear(); repo.Error = new AccessFailure(409, "Conflicto de prueba");
        Assert.Equal(409, (await Assert.ThrowsAsync<AccessFailure>(() => Ejecutar(s, "principal", 1))).Status);
        Assert.Equal(1, repo.Escrituras);
    }
    [Fact]
    public async Task Datos_invalidos_no_alcanzan_creacion()
    {
        var (s, repo, _, _) = Crear(); var datos = ClientesPrueba.Direccion(); datos.Direccion = " ";
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => s.CrearAsync("sesion", "MVC", datos, ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    private static ClienteDireccion Direccion(long id, bool activa, bool principal = false) => new(id, ClientesPrueba.Direccion(), principal, activa);
    private static async Task Ejecutar(IDireccionService s, string accion, long id)
    {
        switch (accion)
        {
            case "listar": await s.ListarAsync("sesion", "MVC", default); break;
            case "crear": await s.CrearAsync("sesion", "MVC", ClientesPrueba.Direccion(), ClientesPrueba.Contexto(), default); break;
            case "actualizar": await s.ActualizarAsync("sesion", "MVC", id, ClientesPrueba.Direccion(), ClientesPrueba.Contexto(), default); break;
            case "eliminar": await s.EliminarAsync("sesion", "MVC", id, ClientesPrueba.Contexto(), default); break;
            case "principal": await s.MarcarPrincipalAsync("sesion", "MVC", id, ClientesPrueba.Contexto(), default); break;
        }
    }
}
internal sealed class DireccionesClientePrueba : IRepositorioDireccionesCliente
{
    public ClienteDireccion[] Datos { get; set; } = [new(1, ClientesPrueba.Direccion(), false, true)];
    public int Lecturas { get; private set; }
    public int Escrituras { get; private set; }
    public long Usuario { get; private set; }
    public long Id { get; private set; }
    public string? Accion { get; private set; }
    public ClienteDireccionDatos? Guardado { get; private set; }
    public ClienteOperacion? Operacion { get; private set; }
    public Exception? Error { get; set; }
    public Task<ClienteDireccion[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct)
    { Lecturas++; Usuario = idUsuario; return Task.FromResult(Datos); }
    private void Escribir(long usuario, long id, string accion, ClienteOperacion op)
    { Escrituras++; Usuario = usuario; Id = id; Accion = accion; Operacion = op; if (Error is not null) throw Error; }
    public Task<ClienteDireccion> CrearPorUsuarioAsync(long idUsuario, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct)
    { Escribir(idUsuario, 2, "crear", operacion); Guardado = datos; return Task.FromResult(new ClienteDireccion(2, datos, false, true)); }
    public Task ActualizarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct)
    { Escribir(idUsuario, idDireccion, "actualizar", operacion); Guardado = datos; return Task.CompletedTask; }
    public Task EliminarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct)
    { Escribir(idUsuario, idDireccion, "eliminar", operacion); return Task.CompletedTask; }
    public Task MarcarPrincipalPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct)
    { Escribir(idUsuario, idDireccion, "principal", operacion); return Task.CompletedTask; }
}

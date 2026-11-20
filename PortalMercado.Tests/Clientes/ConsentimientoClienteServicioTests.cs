using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class ConsentimientoClienteServicioTests
{
    private static (ConsentimientoClienteServicio Servicio, ConsentimientosClientePrueba Repo, AuthClientesPrueba Auth, PerfilClientesPrueba Perfiles) Crear(DocumentosClienteVigentes? docs = null)
    {
        var auth = new AuthClientesPrueba(); var perfiles = new PerfilClientesPrueba(); var repo = new ConsentimientosClientePrueba();
        return (new(new PerfilClienteServicio(auth, perfiles), repo, docs ?? ClientesPrueba.Documentos(), new RelojClientes()), repo, auth, perfiles);
    }
    [Fact]
    public async Task Historial_se_obtiene_por_usuario_y_se_ordena_por_fecha()
    {
        var (s, repo, _, _) = Crear();
        repo.Datos = [Evento(false, "v1", -1), Evento(true, "v2", 0)];
        var historial = await s.ListarAsync("sesion", "MVC", default);
        Assert.Equal("v2", historial[0].VersionDocumento); Assert.Equal(7, repo.Usuario);
        Assert.Equal(2, historial.Length);
    }
    [Fact]
    public async Task Autorizar_registra_version_y_evidencia_del_servidor_sin_sobrescribir_historial()
    {
        var (s, repo, _, _) = Crear(); var previos = repo.Datos.ToArray();
        await s.RegistrarAsync("sesion", "MVC", Solicitud(true), ClientesPrueba.Contexto(), default);
        Assert.Equal(1, repo.Escrituras); Assert.Equal(7, repo.Usuario);
        Assert.Equal("test-promociones-1", repo.Guardado!.VersionDocumento); Assert.True(repo.Guardado.Aceptado);
        Assert.Equal(RelojClientes.Fecha, repo.Operacion!.FechaUtc); Assert.Equal("127.0.0.1", repo.Operacion.Ip);
        Assert.Equal(previos, repo.Datos);
    }
    [Theory]
    [InlineData("TERMINOS")]
    [InlineData("PRIVACIDAD")]
    [InlineData("promociones")]
    [InlineData("OTRO")]
    public async Task Preferencia_comercial_no_altera_otros_consentimientos(string tipo)
    {
        var (s, repo, _, _) = Crear(); var datos = Solicitud(true); datos.Tipo = tipo;
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", datos, ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("anterior")]
    [InlineData("")]
    public async Task Autorizacion_exige_version_vigente(string version)
    {
        var (s, repo, _, _) = Crear(); var datos = Solicitud(true); datos.VersionDocumento = version;
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", datos, ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Fact]
    public async Task Retiro_de_version_anterior_sigue_disponible_sin_documento_promocional_vigente()
    {
        var (s, repo, _, _) = Crear(new("test-terminos-1", "test-privacidad-1"));
        repo.Datos = [Evento(true, "anterior", -1)];
        var solicitud = Solicitud(false); solicitud.VersionDocumento = "anterior";
        await s.RegistrarAsync("sesion", "MVC", solicitud, ClientesPrueba.Contexto(), default);
        Assert.False(repo.Guardado!.Aceptado); Assert.Equal("anterior", repo.Guardado.VersionDocumento);
    }
    [Fact]
    public async Task No_inventa_retiro_para_documento_que_no_existe_en_historial()
    {
        var (s, repo, _, _) = Crear(); repo.Datos = [];
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", Solicitud(false), ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Fact]
    public async Task Sin_documento_promocional_no_autoriza()
    {
        var (s, repo, _, _) = Crear(new("test-terminos-1", "test-privacidad-1"));
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", Solicitud(true), ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Escrituras);
    }
    [Fact]
    public async Task Sesion_invalida_no_lee_ni_registra_consentimientos()
    {
        var (s, repo, auth, _) = Crear(); auth.Perfil = null;
        Assert.Equal(401, (await Assert.ThrowsAsync<AccessFailure>(() => s.ListarAsync("sesion", "MVC", default))).Status);
        Assert.Equal(0, repo.Lecturas); Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Perfil_ausente_o_de_otro_usuario_bloquea_registro(bool ajeno)
    {
        var (s, repo, _, perfiles) = Crear(); perfiles.Perfil = ajeno ? ClientesPrueba.Perfil(usuario: 99) : null;
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", Solicitud(true), ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Lecturas); Assert.Equal(0, repo.Escrituras);
    }
    [Theory]
    [InlineData("tipo")]
    [InlineData("version")]
    [InlineData("fecha")]
    [InlineData("ambiguo")]
    public async Task Historial_invalido_no_genera_preferencia_inventada(string caso)
    {
        var (s, repo, _, _) = Crear(); var evento = Evento(true, "v1", 0);
        repo.Datos = caso switch
        {
            "tipo" => [evento with { Tipo = "DESCONOCIDO" }],
            "version" => [evento with { VersionDocumento = "\n" }],
            "fecha" => [evento with { RegistradoEn = default }],
            _ => [evento, evento with { Aceptado = false }]
        };
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => s.ListarAsync("sesion", "MVC", default))).Status);
    }
    [Fact]
    public async Task Error_de_persistencia_no_se_presenta_como_autorizacion_correcta()
    {
        var (s, repo, _, _) = Crear(); repo.Error = new AccessFailure(503, "Fallo controlado de prueba");
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => s.RegistrarAsync("sesion", "MVC", Solicitud(true), ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(1, repo.Escrituras);
    }
    private static ClienteConsentimientoDatos Solicitud(bool acepta) => new()
        { Tipo = "PROMOCIONES", VersionDocumento = "test-promociones-1", Aceptado = acepta };
    private static ClienteConsentimiento Evento(bool acepta, string version, int horas) =>
        new("PROMOCIONES", version, acepta, RelojClientes.Fecha.AddHours(horas));
}
internal sealed class ConsentimientosClientePrueba : IRepositorioConsentimientosCliente
{
    public ClienteConsentimiento[] Datos { get; set; } = [];
    public int Lecturas { get; private set; }
    public int Escrituras { get; private set; }
    public long Usuario { get; private set; }
    public ClienteConsentimientoDatos? Guardado { get; private set; }
    public ClienteOperacion? Operacion { get; private set; }
    public Exception? Error { get; set; }
    public Task<ClienteConsentimiento[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct)
    { Usuario = idUsuario; Lecturas++; return Task.FromResult(Datos); }
    public Task RegistrarPorUsuarioAsync(long idUsuario, ClienteConsentimientoDatos datos, ClienteOperacion operacion, CancellationToken ct)
    {
        Usuario = idUsuario; Escrituras++; Guardado = datos; Operacion = operacion;
        if (Error is not null) throw Error;
        return Task.CompletedTask;
    }
}

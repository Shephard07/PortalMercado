using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class RegistroClienteServicioTests
{
    private static (RegistroClienteServicio Servicio, RegistroClientesPrueba Repo, HashClientesPrueba Hash) Crear(DocumentosClienteVigentes? docs = null)
    {
        var repo = new RegistroClientesPrueba(); var hash = new HashClientesPrueba();
        return (new(repo, hash, docs ?? ClientesPrueba.Documentos(), new RelojClientes()), repo, hash);
    }
    [Fact]
    public async Task Prepara_un_alta_unica_con_hash_y_metadatos_del_servidor()
    {
        var (servicio, repo, hash) = Crear();
        var resultado = await servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default);
        Assert.False(resultado.RequiereConfirmacionCorreo);
        Assert.Equal(1, repo.Llamadas); Assert.Equal(1, hash.Llamadas);
        var alta = Assert.IsType<RegistroClienteAlta>(repo.Alta);
        Assert.Equal("Comprador de prueba", alta.Nombre);
        Assert.Equal("comprador@example.invalid", alta.Correo);
        Assert.Equal("COMPRADOR@EXAMPLE.INVALID", alta.CorreoNormalizado);
        Assert.Equal("999000111", alta.Telefono);
        Assert.Equal(hash.Hash, alta.PasswordHash);
        Assert.Equal(0, alta.Operacion.IdUsuario);
        Assert.Equal(RelojClientes.Fecha, alta.Operacion.FechaUtc);
        Assert.Equal("127.0.0.1", alta.Operacion.Ip);
        Assert.Equal("ciclo3-prueba-01", alta.Operacion.Correlacion);
    }
    [Fact]
    public async Task Marketing_es_opcional_y_no_impide_registrarse()
    {
        var (servicio, repo, _) = Crear(); var solicitud = ClientesPrueba.Registro();
        Assert.False(solicitud.AceptaPromociones);
        await servicio.RegistrarAsync(solicitud, ClientesPrueba.Contexto(), default);
        Assert.Equal(3, repo.Alta!.Consentimientos.Length);
        Assert.All(repo.Alta.Consentimientos.Where(x => x.Tipo != "PROMOCIONES"), x => Assert.True(x.Aceptado));
        var marketing = Assert.Single(repo.Alta!.Consentimientos, x => x.Tipo == "PROMOCIONES");
        Assert.False(marketing.Aceptado); Assert.Equal("test-promociones-1", marketing.VersionDocumento);
    }
    [Fact]
    public async Task Aceptacion_promocional_exige_version_vigente()
    {
        var (servicio, repo, _) = Crear();
        var solicitud = ClientesPrueba.Registro();

        solicitud.AceptaPromociones = true;
        solicitud.VersionPromociones = "test-promociones-1";

        await servicio.RegistrarAsync(
            solicitud, ClientesPrueba.Contexto(), default);

        var promocional = Assert.Single(
            repo.Alta!.Consentimientos,
            x => x.Tipo == "PROMOCIONES");

        Assert.True(promocional.Aceptado);
    }
    [Fact]
    public async Task Registro_sin_documento_promocional_no_inventa_una_aceptacion()
    {
        var (servicio, repo, _) = Crear(new("test-terminos-1", "test-privacidad-1"));
        await servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default);
        Assert.Equal(2, repo.Alta!.Consentimientos.Length);
        Assert.DoesNotContain(repo.Alta.Consentimientos, x => x.Tipo == "PROMOCIONES");
    }
    [Theory]
    [InlineData("nombre")]
    [InlineData("nombre-largo")]
    [InlineData("nombre-control")]
    [InlineData("correo")]
    [InlineData("correo-unicode")]
    [InlineData("password")]
    [InlineData("confirmacion")]
    [InlineData("telefono-largo")]
    [InlineData("telefono-control")]
    [InlineData("telefono-unicode")]
    [InlineData("terminos")]
    [InlineData("privacidad")]
    [InlineData("terminos-version")]
    [InlineData("privacidad-version")]
    [InlineData("version-vacia")]
    [InlineData("promociones-version")]
    [InlineData("promociones-version-falsa")]
    public async Task Entrada_invalida_no_alcanza_hash_ni_persistencia(string caso)
    {
        var (servicio, repo, hash) = Crear(); var s = ClientesPrueba.Registro();
        switch (caso)
        {
            case "nombre": s.Nombre = " "; break;
            case "nombre-largo": s.Nombre = new('x', 151); break;
            case "nombre-control": s.Nombre = "Nombre\nPrueba"; break;
            case "correo": s.Correo = "invalido"; break;
            case "correo-unicode": s.Correo = "compradór@example.invalid"; break;
            case "password": s.Password = s.ConfirmarPassword = "solominusculas"; break;
            case "confirmacion": s.ConfirmarPassword = "Otra-Prueba2026!"; break;
            case "telefono-largo": s.Telefono = new('1', 31); break;
            case "telefono-control": s.Telefono = "999\n111"; break;
            case "telefono-unicode": s.Telefono = "☎999"; break;
            case "terminos": s.AceptaTerminos = false; break;
            case "privacidad": s.AceptaPrivacidad = false; break;
            case "terminos-version": s.VersionTerminos = "anterior"; break;
            case "privacidad-version": s.VersionPrivacidad = "anterior"; break;
            case "version-vacia": s.VersionTerminos = ""; break;
            case "promociones-version": s.AceptaPromociones = true; break;
            case "promociones-version-falsa": s.VersionPromociones = "falsa"; break;
        }
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.RegistrarAsync(s, ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, hash.Llamadas); Assert.Equal(0, repo.Llamadas);
    }
    [Fact]
    public async Task No_acepta_promociones_sin_documento_disponible()
    {
        var (servicio, repo, _) = Crear(new("test-terminos-1", "test-privacidad-1"));
        var s = ClientesPrueba.Registro(); s.AceptaPromociones = true;
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.RegistrarAsync(s, ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Llamadas);
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("version\n1")]
    [InlineData("versión")]
    public async Task Documentos_no_configurados_bloquean_el_registro(string version)
    {
        var (servicio, repo, hash) = Crear(new(version, "test-privacidad-1"));
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default))).Status);
        Assert.Equal(0, repo.Llamadas); Assert.Equal(0, hash.Llamadas);
    }
    [Fact]
    public async Task Correo_no_disponible_no_revela_la_cuenta_existente()
    {
        var (servicio, repo, _) = Crear(); repo.Resultado = new(EstadoRegistroCliente.NoDisponible);
        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default));
        Assert.Equal(400, error.Status); Assert.Equal(RegistroClienteServicio.RechazoGenerico, error.Message);
        Assert.DoesNotContain("example.invalid", error.Message); Assert.Equal(1, repo.Llamadas);
    }
    [Fact]
    public async Task Propaga_confirmacion_requerida_sin_crear_una_sesion()
    {
        var (servicio, repo, _) = Crear(); repo.Resultado = new(EstadoRegistroCliente.Creado, true);
        Assert.True((await servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default)).RequiereConfirmacionCorreo);
    }
    [Theory]
    [InlineData("")]
    [InlineData("Compra-Prueba2026!")]
    public async Task Rechaza_un_proveedor_que_no_produce_hash(string valor)
    {
        var (servicio, repo, hash) = Crear(); hash.Hash = valor;
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default));
        Assert.Equal(0, repo.Llamadas);
    }
    [Fact]
    public async Task Solicitud_cancelada_no_inicia_el_alta()
    {
        var (servicio, repo, hash) = Crear(); using var cancelacion = new CancellationTokenSource(); cancelacion.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), cancelacion.Token));
        Assert.Equal(0, repo.Llamadas); Assert.Equal(0, hash.Llamadas);
    }
    [Fact]
    public async Task Un_fallo_del_repositorio_no_se_presenta_como_registro_correcto()
    {
        var (servicio, repo, _) = Crear(); repo.Error = new InvalidOperationException("Fallo de prueba");
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.RegistrarAsync(ClientesPrueba.Registro(), ClientesPrueba.Contexto(), default));
        Assert.Equal(1, repo.Llamadas);
        // La atomicidad SQL se comprobará contra el proveedor aprobado, no con este doble.
    }
    [Fact]
    public void Representacion_textual_de_solicitud_no_expone_password()
    {
        var solicitud = ClientesPrueba.Registro();
        Assert.Equal(nameof(RegistroClienteSolicitud), solicitud.ToString());
        Assert.DoesNotContain(solicitud.Password, solicitud.ToString());
    }
}

using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;

namespace PortalMercado.Tests.Clientes;

internal static class ClientesPrueba
{
    public static RegistroClienteSolicitud Registro() => new()
    {
        Nombre = " Comprador de prueba ", Correo = "comprador@example.invalid",
        Password = "Compra-Prueba2026!", ConfirmarPassword = "Compra-Prueba2026!", Telefono = " 999000111 ",
        AceptaTerminos = true, AceptaPrivacidad = true,
        VersionTerminos = "test-terminos-1", VersionPrivacidad = "test-privacidad-1"
    };
    public static DocumentosClienteVigentes Documentos() => new("test-terminos-1", "test-privacidad-1", "test-promociones-1");
    public static ClienteOperacionContexto Contexto() => new("ciclo3-prueba-01", "127.0.0.1");
    public static ClientePerfil Perfil(long usuario = 7, long cliente = 21) =>
        new(cliente, usuario, "Comprador de prueba", "comprador@example.invalid", new());
    public static ClienteDireccionDatos Direccion() => new()
    {
        Etiqueta = " Casa ", Direccion = " Calle de prueba 123 ", Referencia = " Puerta azul ",
        Distrito = " Puente Piedra ", Provincia = " Lima ", Departamento = " Lima ",
        Latitud = -11.8666667m, Longitud = -77.0666667m
    };
}
internal sealed class RelojClientes : TimeProvider
{
    public static readonly DateTimeOffset Fecha = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Fecha;
}
internal sealed class HashClientesPrueba : IHashPasswordCliente
{
    public int Llamadas { get; private set; }
    public string Hash { get; set; } = "HASH-SOLO-PRUEBA";
    public string Crear(string nombre, string correo, string password) { Llamadas++; return Hash; }
}
internal sealed class RegistroClientesPrueba : IRepositorioRegistroCliente
{
    public int Llamadas { get; private set; }
    public RegistroClienteAlta? Alta { get; private set; }
    public RegistroClientePersistido Resultado { get; set; } = new(EstadoRegistroCliente.Creado);
    public Exception? Error { get; set; }
    public Task<RegistroClientePersistido> RegistrarAsync(RegistroClienteAlta alta, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Llamadas++; Alta = alta;
        return Error is null ? Task.FromResult(Resultado) : Task.FromException<RegistroClientePersistido>(Error);
    }
}
internal sealed class AuthClientesPrueba : IAuthService
{
    public AuthProfile? Perfil { get; set; } = new(7, "comprador@example.invalid", "Comprador de prueba", [], [], []);
    public int Llamadas { get; private set; }
    public Task<AuthProfile?> ValidateAsync(string credential, string host, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Llamadas++; return Task.FromResult(Perfil); }
    public Task<LoginOutcome?> LoginAsync(LoginRequest request, string host, string correlationId, CancellationToken ct) => throw new NotSupportedException();
    public Task LogoutAsync(string credential, string host, string correlationId, CancellationToken ct) => throw new NotSupportedException();
}
internal sealed class PerfilClientesPrueba : IRepositorioPerfilCliente
{
    public ClientePerfil? Perfil { get; set; } = ClientesPrueba.Perfil();
    public long? UsuarioConsultado { get; private set; }
    public long? UsuarioGuardado { get; private set; }
    public ClientePerfilDatos? Datos { get; private set; }
    public ClienteOperacion? Operacion { get; private set; }
    public Task<ClientePerfil?> ObtenerPorUsuarioAsync(long idUsuario, CancellationToken ct)
    { UsuarioConsultado = idUsuario; return Task.FromResult(Perfil); }
    public Task ActualizarPorUsuarioAsync(long idUsuario, ClientePerfilDatos datos, ClienteOperacion operacion, CancellationToken ct)
    { UsuarioGuardado = idUsuario; Datos = datos; Operacion = operacion; return Task.CompletedTask; }
}

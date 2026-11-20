using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Infrastructure.Clientes;

// Contratos aprobados de lectura/escritura pendientes. Sin consultas o DDL supuestos.
public sealed class PerfilDireccionesPendiente : IRepositorioPerfilCliente, IRepositorioDireccionesCliente
{
    private static AccessFailure Pendiente(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return new(503, "Tu perfil de cliente no está disponible en este momento.");
    }
    public Task<ClientePerfil?> ObtenerPorUsuarioAsync(long idUsuario, CancellationToken ct) => throw Pendiente(ct);
    public Task ActualizarPorUsuarioAsync(long idUsuario, ClientePerfilDatos datos, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
    public Task<ClienteDireccion[]> ListarPorUsuarioAsync(long idUsuario, CancellationToken ct) => throw Pendiente(ct);
    public Task<ClienteDireccion> CrearPorUsuarioAsync(long idUsuario, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
    public Task ActualizarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteDireccionDatos datos, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
    public Task EliminarPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
    public Task MarcarPrincipalPorUsuarioAsync(long idUsuario, long idDireccion, ClienteOperacion operacion, CancellationToken ct) => throw Pendiente(ct);
}

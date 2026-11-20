using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;

namespace PortalMercado.Application.Clientes;

public sealed class PerfilClienteServicio(IAuthService auth, IRepositorioPerfilCliente perfiles,
    TimeProvider? reloj = null) : IClienteService
{
    public async Task<ClientePerfil> ObtenerAsync(string credencial, string host, CancellationToken ct)
    {
        var idUsuario = await UsuarioAsync(credencial, host, ct);
        return await PerfilPropioAsync(idUsuario, ct);
    }
    public async Task ActualizarAsync(string credencial, string host, ClientePerfilDatos datos,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var idUsuario = await UsuarioAsync(credencial, host, ct);
        await PerfilPropioAsync(idUsuario, ct);
        var limpio = ClientesValidacion.Perfil(datos);
        var operacion = ClientesValidacion.Operacion(idUsuario, contexto, reloj ?? TimeProvider.System);
        await perfiles.ActualizarPorUsuarioAsync(idUsuario, limpio, operacion, ct);
    }
    private async Task<long> UsuarioAsync(string credencial, string host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (host is not ("API" or "MVC")) throw new ArgumentException("Host no válido.", nameof(host));
        if (string.IsNullOrWhiteSpace(credencial)) throw new AccessFailure(401, "Inicia sesión para continuar.");
        var perfil = await auth.ValidateAsync(credencial, host, ct);
        return perfil is { IdUsuario: > 0 } ? perfil.IdUsuario
            : throw new AccessFailure(401, "La sesión ya no está disponible.");
    }
    private async Task<ClientePerfil> PerfilPropioAsync(long idUsuario, CancellationToken ct)
    {
        var perfil = await perfiles.ObtenerPorUsuarioAsync(idUsuario, ct);
        if (perfil is null) throw new AccessFailure(403, "Tu cuenta no tiene un perfil de cliente disponible.");
        if (perfil.IdUsuario != idUsuario || perfil.IdCliente <= 0)
            throw new AccessFailure(403, "No tienes acceso a este perfil.");
        return perfil;
    }
}

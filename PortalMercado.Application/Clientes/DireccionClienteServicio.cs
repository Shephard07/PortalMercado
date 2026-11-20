using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Clientes;

public sealed class DireccionClienteServicio(IClienteService clientes, IRepositorioDireccionesCliente direcciones,
    TimeProvider? reloj = null) : IDireccionService
{
    public async Task<ClienteDireccion[]> ListarAsync(string credencial, string host, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        return (await LeerAsync(perfil.IdUsuario, ct)).Where(x => x.Activa).ToArray();
    }
    public async Task<ClienteDireccion> CrearAsync(string credencial, string host, ClienteDireccionDatos datos,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        var limpio = ClientesValidacion.Direccion(datos);
        var operacion = ClientesValidacion.Operacion(perfil.IdUsuario, contexto, reloj ?? TimeProvider.System);
        var creada = await direcciones.CrearPorUsuarioAsync(perfil.IdUsuario, limpio, operacion, ct);
        if (creada is null || creada.IdDireccion <= 0 || !creada.Activa)
            throw new AccessFailure(503, "No pudimos confirmar la dirección. Recarga la lista antes de volver a intentarlo.");
        return creada;
    }
    public async Task ActualizarAsync(string credencial, string host, long idDireccion, ClienteDireccionDatos datos,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        await ExigirPropiaAsync(perfil.IdUsuario, idDireccion, ct);
        var limpio = ClientesValidacion.Direccion(datos);
        var operacion = ClientesValidacion.Operacion(perfil.IdUsuario, contexto, reloj ?? TimeProvider.System);
        await direcciones.ActualizarPorUsuarioAsync(perfil.IdUsuario, idDireccion, limpio, operacion, ct);
    }
    public async Task EliminarAsync(string credencial, string host, long idDireccion,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        await ExigirPropiaAsync(perfil.IdUsuario, idDireccion, ct);
        var operacion = ClientesValidacion.Operacion(perfil.IdUsuario, contexto, reloj ?? TimeProvider.System);
        await direcciones.EliminarPorUsuarioAsync(perfil.IdUsuario, idDireccion, operacion, ct);
    }
    public async Task MarcarPrincipalAsync(string credencial, string host, long idDireccion,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        await ExigirPropiaAsync(perfil.IdUsuario, idDireccion, ct);
        var operacion = ClientesValidacion.Operacion(perfil.IdUsuario, contexto, reloj ?? TimeProvider.System);
        // No desmarcar/marcar en llamadas separadas. El repositorio debe resolverlo
        // con bloqueo por cliente, revalidación de propiedad y una sola transacción.
        await direcciones.MarcarPrincipalPorUsuarioAsync(perfil.IdUsuario, idDireccion, operacion, ct);
    }
    private async Task ExigirPropiaAsync(long usuario, long id, CancellationToken ct)
    {
        if (id <= 0) throw new AccessFailure(400, "Revisa la dirección seleccionada.");
        if (!(await LeerAsync(usuario, ct)).Any(x => x.IdDireccion == id && x.Activa))
            throw new AccessFailure(404, "La dirección no está disponible.");
    }
    private async Task<ClienteDireccion[]> LeerAsync(long usuario, CancellationToken ct)
    {
        var lista = await direcciones.ListarPorUsuarioAsync(usuario, ct);
        if (lista is null || lista.Any(x => x is null || x.IdDireccion <= 0 || (x.Principal && !x.Activa)) ||
            lista.Select(x => x.IdDireccion).Distinct().Count() != lista.Length || lista.Count(x => x.Principal) > 1)
            throw new AccessFailure(503, "No podemos consultar tus direcciones en este momento.");
        return lista;
    }
}

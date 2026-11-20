using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

public sealed record TiendasPerfilOpciones(long? NegocioLocalDemostracion);

// El vínculo explícito no concede permisos: el servicio autoriza el ID local antes de consultar este adaptador.
public sealed class RepositorioPerfilDemostracion(RepositorioTiendasDemostracion tiendas,
    TiendasPerfilOpciones opciones) : IRepositorioPerfilTienda
{
    public Task<PerfilTienda?> ObtenerAsync(long idNegocio, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(idNegocio == opciones.NegocioLocalDemostracion
            ? tiendas.Perfil(idNegocio) : null);
    }

    public Task GuardarAsync(long idNegocio, PerfilTiendaDatos datos, AuditoriaPerfilTienda auditoria, CancellationToken ct)
    {
        if (idNegocio != opciones.NegocioLocalDemostracion)
            throw new AccessFailure(404, "No se encontró un perfil de tienda para ese negocio.");
        tiendas.GuardarPerfil(idNegocio, datos, auditoria, ct);
        return Task.CompletedTask;
    }
}

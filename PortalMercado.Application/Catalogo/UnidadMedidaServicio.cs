using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Catalogo;

public sealed class UnidadMedidaServicio(IUserAdministration usuarios, IRepositorioUnidadesMedida unidades)
{
    public async Task<UnidadMedidaDto[]> ListarAsync(long actor, CancellationToken ct)
    {
        var acceso = await usuarios.AccessAsync(actor, ct);
        acceso.Demand(PermisosCatalogo.ConsultarUnidades);
        return await unidades.ListarAsync(ct);
    }
}

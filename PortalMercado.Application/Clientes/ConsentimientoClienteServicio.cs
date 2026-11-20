using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Clientes;

public sealed class ConsentimientoClienteServicio(IClienteService clientes, IRepositorioConsentimientosCliente consentimientos,
    DocumentosClienteVigentes documentos, TimeProvider? reloj = null) : IConsentimientoService
{
    public async Task<ClienteConsentimiento[]> ListarAsync(string credencial, string host, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        return await HistorialAsync(perfil.IdUsuario, ct);
    }
    public async Task RegistrarAsync(string credencial, string host, ClienteConsentimientoDatos datos,
        ClienteOperacionContexto contexto, CancellationToken ct)
    {
        var perfil = await clientes.ObtenerAsync(credencial, host, ct);
        ClientesValidacion.ValidarModelo(datos);
        // Los documentos obligatorios se aceptan en el alta. No alterar su historial
        // mediante la preferencia comercial ni condicionar la consulta del portal a marketing.
        if (datos.Tipo != "PROMOCIONES") throw new AccessFailure(400, "Esta opción solo cambia la preferencia de promociones.");
        var historial = await HistorialAsync(perfil.IdUsuario, ct);
        if (datos.Aceptado)
        {
            documentos.ExigirConfiguracion();
            if (documentos.Promociones is null || datos.VersionDocumento != documentos.Promociones)
                throw new AccessFailure(400, "Revisa las condiciones vigentes de promociones antes de autorizar.");
        }
        else if (!historial.Any(x => x.Tipo == "PROMOCIONES" && x.VersionDocumento == datos.VersionDocumento))
            throw new AccessFailure(400, "No encontramos esa versión en tu historial de promociones.");
        // Retirar una autorización previa no exige que su texto siga siendo la versión vigente.
        var operacion = ClientesValidacion.Operacion(perfil.IdUsuario, contexto, reloj ?? TimeProvider.System);
        await consentimientos.RegistrarPorUsuarioAsync(perfil.IdUsuario, new()
        { Tipo = "PROMOCIONES", VersionDocumento = datos.VersionDocumento, Aceptado = datos.Aceptado }, operacion, ct);
    }
    private async Task<ClienteConsentimiento[]> HistorialAsync(long usuario, CancellationToken ct)
    {
        var historial = await consentimientos.ListarPorUsuarioAsync(usuario, ct);
        if (historial is null || historial.Any(x => x is null ||
            x.Tipo is not ("TERMINOS" or "PRIVACIDAD" or "PROMOCIONES") ||
            string.IsNullOrWhiteSpace(x.VersionDocumento) || x.VersionDocumento.Length > 40 ||
            !ClientesValidacion.Ascii(x.VersionDocumento) || x.VersionDocumento != x.VersionDocumento.Trim() ||
            x.RegistradoEn == default) ||
            historial.GroupBy(x => (x.Tipo, x.RegistradoEn)).Any(g => g.Select(x => (x.VersionDocumento, x.Aceptado)).Distinct().Count() > 1))
            throw new AccessFailure(503, "No podemos consultar tus consentimientos en este momento.");
        return historial.OrderByDescending(x => x.RegistradoEn).ToArray();
    }
}

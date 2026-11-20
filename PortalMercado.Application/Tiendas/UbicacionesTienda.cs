namespace PortalMercado.Application.Tiendas;

public sealed record UbicacionTienda(string Mercado, string Sector, string Puesto, string? Referencia);

// PuestoHabilitado representa la regla de estado que deberá aprobar A/DBA.
public sealed record UbicacionTiendaLectura(long IdNegocio, long IdMercado, long IdPuesto,
    string Mercado, string Sector, string Puesto, string? Referencia,
    bool MercadoActivo, bool SectorActivo, bool PuestoHabilitado, DateOnly Desde, DateOnly? Hasta);

public interface IRepositorioUbicacionesTienda
{
    // Proyección de asignaciones físicas del negocio, nunca DirecciónFiscal.
    // Fecha comercial calculada en servidor. El adaptador puede prefiltrar vigencia.
    Task<UbicacionTiendaLectura[]> ConsultarAsync(long idNegocio, DateOnly fecha, CancellationToken ct);
}

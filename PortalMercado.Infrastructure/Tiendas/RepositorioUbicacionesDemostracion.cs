using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

public sealed class RepositorioUbicacionesDemostracion : IRepositorioUbicacionesTienda
{
    public Task<UbicacionTiendaLectura[]> ConsultarAsync(long idNegocio, DateOnly fecha, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (idNegocio == 1001)
        {
            // Fechas relativas para que los escenarios ficticios se mantengan reproducibles.
            return Task.FromResult<UbicacionTiendaLectura[]>(
            [
                new(1001, 100, 501, "Mercado de demostración Centro", "Frutas y verduras", "A-101",
                    "Ingreso por la puerta principal.", true, true, true, fecha.AddDays(-30), null),
                new(1001, 100, 502, "Mercado de demostración Centro", "Frutas y verduras", "A-102",
                    "Junto al puesto A-101.", true, true, true, fecha, fecha),
                new(1001, 100, 503, "Mercado de demostración Centro", "Sector vencido", "VENCIDO",
                    null, true, true, true, fecha.AddDays(-30), fecha.AddDays(-1)),
                new(1001, 100, 504, "Mercado de demostración Centro", "Sector futuro", "FUTURO",
                    null, true, true, true, fecha.AddDays(1), null),
                new(1001, 100, 505, "Mercado de demostración Centro", "Sector inactivo", "INACTIVO",
                    null, true, false, true, fecha.AddDays(-30), null),
                new(1001, 100, 506, "Mercado de demostración Centro", "Sector cerrado", "NO-HABILITADO",
                    null, true, true, false, fecha.AddDays(-30), null)
            ]);
        }

        // Abarrotes permite comprobar el estado vacío sin inventar una dirección.
        return Task.FromResult<UbicacionTiendaLectura[]>([]);
    }
}

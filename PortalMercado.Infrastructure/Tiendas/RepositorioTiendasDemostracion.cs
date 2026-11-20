using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

// Datos ficticios para desarrollo y pruebas. El registro en los hosts será explícito
// y exclusivo del modo de demostración. El adaptador SQL depende del contrato DBA.
public sealed class RepositorioTiendasDemostracion : IRepositorioTiendas
{
    private readonly object puerta = new();
    private Guid version = Guid.NewGuid();
    private AuditoriaPerfilTienda? ultimoCambio;
    private readonly TiendaLectura[] tiendas =
    [
        new(1001, "fruteria-el-sol", "Frutería El Sol", 100, "Mercado de demostración Centro",
            "ACTIVO", true, true, "Frutas seleccionadas. Datos ficticios.", null,
            "Lunes a sábado de 08:00 a 17:00", "000000001", false, "sol@example.invalid", false, [1, 3]),
        new(1002, "abarrotes-lima", "Abarrotes Lima", 101, "Mercado de demostración Norte",
            "ACTIVO", true, true, "Abarrotes para tu hogar. Datos ficticios.", null,
            "Lunes a viernes de 09:00 a 18:00", "000000002", true, "abarrotes@example.invalid", true, [2]),
        new(1003, "tienda-pendiente", "Tienda pendiente", 100, "Mercado de demostración Centro",
            "PENDIENTE", true, true, null, null, null, null, false, null, false, [1]),
        new(1004, "tienda-suspendida", "Tienda suspendida", 100, "Mercado de demostración Centro",
            "SUSPENDIDO", true, true, null, null, null, null, false, null, false, [1]),
        new(1005, "tienda-baja", "Tienda de baja", 100, "Mercado de demostración Centro",
            "BAJA", true, true, null, null, null, null, false, null, false, [1]),
        new(1006, "tienda-oculta", "Tienda sin visibilidad aprobada", 100, "Mercado de demostración Centro",
            "ACTIVO", true, false, null, null, null, null, false, null, false, [1]),
        new(1007, "mercado-inactivo", "Tienda de mercado inactivo", 102, "Mercado inactivo de demostración",
            "ACTIVO", false, true, null, null, null, null, false, null, false, [1])
    ];

    public Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var visibles = Instantanea().Where(t => t.Estado == "ACTIVO" && t.MercadoActivo && t.VisibilidadAprobada).ToArray();
        var mercados = visibles.Select(t => new NamedId(t.IdMercado, t.Mercado)).DistinctBy(m => m.Id)
            .OrderBy(m => m.Nombre, StringComparer.OrdinalIgnoreCase).ToArray();
        var categorias = new[] { new NamedId(1, "Frutas"), new NamedId(2, "Abarrotes"), new NamedId(3, "Verduras") }
            .Where(c => visibles.Any(t => t.CategoriasPublicadas.Contains(c.Id))).ToArray();
        return Task.FromResult(new TiendaOpciones(mercados, categorias));
    }

    public Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var coincidencias = Instantanea().Where(t => t.Estado == "ACTIVO" && t.MercadoActivo && t.VisibilidadAprobada)
            .Where(t => filtro.MercadoId is null || t.IdMercado == filtro.MercadoId)
            .Where(t => filtro.CategoriaId is null || t.CategoriasPublicadas.Contains(filtro.CategoriaId.Value))
            .Where(t => filtro.Buscar is null || t.NombreComercial.Contains(filtro.Buscar, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.NombreComercial, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.IdNegocio)
            .ToArray();
        var items = coincidencias.Skip((filtro.Pagina - 1) * filtro.Tamano).Take(filtro.Tamano).ToArray();
        return Task.FromResult(new Page<TiendaLectura>(items, coincidencias.Length, filtro.Pagina, filtro.Tamano));
    }

    public Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Instantanea().Where(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray());
    }
    private TiendaLectura[] Instantanea()
    {
        lock (puerta) return tiendas.ToArray();
    }

    internal PerfilTienda Perfil(long idNegocio)
    {
        lock (puerta)
        {
            var tienda = tiendas[0];
            return new(idNegocio, tienda.NombreComercial, tienda.Slug, tienda.TelefonoPublico, tienda.CorreoPublico,
                new PerfilTiendaDatos { Version = version, Descripcion = tienda.Descripcion, LogoUrl = tienda.LogoUrl,
                    Horario = tienda.Horario, Telefono = tienda.Telefono, Correo = tienda.Correo }, ultimoCambio);
        }
    }

    internal void GuardarPerfil(long idNegocio, PerfilTiendaDatos datos, AuditoriaPerfilTienda auditoria, CancellationToken ct)
    {
        lock (puerta)
        {
            ct.ThrowIfCancellationRequested();
            if (version != datos.Version)
                throw new AccessFailure(409, "El perfil cambió en otra ventana. Recarga la página antes de guardar.");
            tiendas[0] = tiendas[0] with { Descripcion = datos.Descripcion, LogoUrl = datos.LogoUrl,
                Horario = datos.Horario, Telefono = datos.Telefono, Correo = datos.Correo };
            version = Guid.NewGuid();
            ultimoCambio = auditoria;
        }
    }
}

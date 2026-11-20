using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

// Conjunto ficticio independiente de la BD y del catálogo privado del Ciclo 1.
public sealed class RepositorioCatalogoDemostracion : IRepositorioCatalogoPublico
{
    private readonly ProductoTiendaLectura[] productos =
    [
        Crear(100101, 1001, 1, "Frutas", "Palta Hass", 9.50m, true, 10.25m),
        Crear(100102, 1001, 3, "Verduras", "Tomate italiano", 4.80m, true, 0),
        Crear(100103, 1001, 3, "Verduras", "Zanahoria", 3.50m, true, 18),
        Crear(100104, 1001, 1, "Frutas", "Manzana roja", 7.90m, true, 8),
        Crear(100105, 1001, 1, "Frutas", "Plátano de seda", 5.20m, true, 15),
        Crear(100106, 1001, 1, "Frutas", "Mango", 6m, true, 12,
            imagen: "https://upload.wikimedia.org/wikipedia/commons/e/ee/Mango_and_cross_section_edit.jpg"),
        Crear(100107, 1001, 1, "Frutas", "Mandarina", 5m, true, 20),
        Crear(100108, 1001, 1, "Frutas", "Naranja", 5m, true, 25),
        Crear(100109, 1001, 1, "Frutas", "Papaya", 4.50m, true, 6),
        Crear(100110, 1001, 1, "Frutas", "Piña", 6.50m, true, 7),
        Crear(100111, 1001, 1, "Frutas", "Uva", 12m, true, 5),
        Crear(100112, 1001, 3, "Verduras", "Lechuga", 2.50m, false, null, unidad: "UND"),
        Crear(100113, 1001, 3, "Verduras", "Pepino", 3m, true, 11),
        Crear(100114, 1001, 3, "Verduras", "Cebolla roja", 3.80m, true, 14),
        Crear(100115, 1001, 1, "Frutas", "Sandía", 2m, true, 35),
        Crear(100116, 1001, 1, "Frutas", "Borrador reservado", 1m, false, null, estado: "BORRADOR"),
        Crear(100117, 1001, 1, "Frutas", "Agotado reservado", 1m, false, null, estado: "AGOTADO"),
        Crear(100118, 1001, 1, "Frutas", "Suspendido reservado", 1m, false, null, estado: "SUSPENDIDO"),
        Crear(200101, 1002, 2, "Abarrotes", "Arroz extra", 4.40m, true, 50),
        Crear(200102, 1002, 2, "Abarrotes", "Aceite vegetal", 9.80m, false, null, unidad: "LT"),
        Crear(200103, 1002, 2, "Abarrotes", "Azúcar rubia", 4m, true, 30),
        Crear(200104, 1002, 2, "Abarrotes", "Lenteja", 8.50m, true, 16),
        Crear(400101, 1004, 1, "Frutas", "Producto de tienda suspendida", 2m, false, null)
    ];

    public Task<CatalogoTiendaLectura> ConsultarAsync(long idNegocio, ProductoTiendaFiltro filtro, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var publicados = productos.Where(p => p.IdNegocio == idNegocio && p.Estado == "PUBLICADO").ToArray();
        var categorias = publicados.Select(p => new NamedId(p.Datos.IdCategoria, p.Datos.Categoria))
            .DistinctBy(c => c.Id).OrderBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase).ToArray();

        var filtrados = publicados.Where(p => filtro.CategoriaId is null || p.Datos.IdCategoria == filtro.CategoriaId)
            .Where(p => filtro.Buscar is null || p.Datos.Nombre.Contains(filtro.Buscar, StringComparison.OrdinalIgnoreCase));

        IOrderedEnumerable<ProductoTiendaLectura> ordenados = filtro.Orden switch
        {
            "precio-asc" => filtrados.OrderBy(p => p.Datos.Precio),
            "precio-desc" => filtrados.OrderByDescending(p => p.Datos.Precio),
            _ => filtrados.OrderBy(p => p.Datos.Nombre, StringComparer.OrdinalIgnoreCase)
        };
        var coincidencias = ordenados.ThenBy(p => p.Datos.Id).ToArray();
        var pagina = coincidencias.Skip((filtro.Pagina - 1) * filtro.Tamano).Take(filtro.Tamano).ToArray();
        return Task.FromResult(new CatalogoTiendaLectura(
            new Page<ProductoTiendaLectura>(pagina, coincidencias.Length, filtro.Pagina, filtro.Tamano), categorias));
    }

    private static ProductoTiendaLectura Crear(long id, long negocio, long categoria, string nombreCategoria,
        string nombre, decimal precio, bool controlaStock, decimal? disponible, string estado = "PUBLICADO",
        string unidad = "KG", string? imagen = null) =>
        new(negocio, estado, new ProductoPublico(id, categoria, nombreCategoria, nombre,
            "Producto de demostración.", precio, unidad, controlaStock, disponible, imagen));
}

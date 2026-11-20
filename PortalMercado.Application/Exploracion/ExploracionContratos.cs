using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Application.Exploracion;

public sealed record ExploracionFiltro(string? Buscar = null, long? MercadoId = null,
    long? CategoriaId = null, string Tipo = "todos", string Orden = "nombre", int Pagina = 1, int Tamano = 12);
public sealed record ProductoExploracion(long IdNegocio, ProductoPublico Producto);
// Solo proyecciones públicas. Nunca incluir Usuario, Cliente, teléfono, correo o datos fiscales.
public sealed record ExploracionLectura(TiendaResumen[] Tiendas, ProductoExploracion[] Productos);
public sealed record ExploracionResultado(string Tipo, long Id, string Nombre, string Destino,
    string? Tienda, string? Mercado, string? Categoria, string? Imagen,
    decimal? Precio = null, string? Unidad = null, bool ControlaStock = false, decimal? Disponible = null);
public sealed record ExploracionPagina(ExploracionFiltro Filtro, Page<ExploracionResultado> Resultados,
    NamedId[] Mercados, NamedId[] Categorias);
public sealed record ExploracionPortada(NamedId[] Mercados, NamedId[] Categorias,
    TiendaResumen[] Tiendas, ExploracionResultado[] Productos);

public interface IRepositorioExploracionPublica
{
    // Snapshot completo del conjunto que este adaptador soporta. Nunca truncar silenciosamente.
    // Solo tiendas visibles y productos PUBLICADO de esas tiendas, precio/stock del contrato público.
    Task<ExploracionLectura> LeerAsync(CancellationToken ct);
}

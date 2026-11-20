using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Tiendas;

public sealed record ProductoTiendaFiltro(long? CategoriaId = null, string? Buscar = null,
    string Orden = "nombre", int Pagina = 1, int Tamano = 12);

public sealed record ProductoPublico(long Id, long IdCategoria, string Categoria, string Nombre,
    string? Descripcion, decimal Precio, string Unidad, bool ControlaStock, decimal? Disponible,
    string? ImagenPrincipal);

public sealed record ProductoTiendaLectura(long IdNegocio, string Estado, ProductoPublico Datos);
public sealed record CatalogoTiendaLectura(Page<ProductoTiendaLectura> Productos, NamedId[] Categorias);
public sealed record CatalogoTiendaPublico(TiendaPublica Tienda, ProductoTiendaFiltro Filtro,
    Page<ProductoPublico> Productos, NamedId[] Categorias);

public interface IRepositorioCatalogoPublico
{
    // Filtrar IdNegocio y PUBLICADO antes de buscar, ordenar, contar o paginar.
    // Precio vigente según contrato DBA. Disponible = Existencia - Reservado si aplica.
    // Categorías derivadas solo de productos publicados de este negocio, sin paginar.
    // Todos los órdenes desempatan por IdProducto para mantener páginas estables.
    Task<CatalogoTiendaLectura> ConsultarAsync(long idNegocio, ProductoTiendaFiltro filtro, CancellationToken ct);
}

public sealed class CatalogoTiendaServicio(ITiendaService tiendas, IRepositorioCatalogoPublico catalogo)
{
    public async Task<CatalogoTiendaPublico> ConsultarAsync(string slug, ProductoTiendaFiltro filtro, CancellationToken ct)
    {
        // Resolver visibilidad e identidad desde el slug en el servidor.
        var tienda = await tiendas.ObtenerAsync(slug, ct);
        var buscar = filtro.Buscar?.Trim();
        var orden = filtro.Orden?.Trim().ToLowerInvariant();
        if (filtro.CategoriaId is < 1 || buscar is { Length: > 200 } ||
            filtro.Pagina is < 1 or > 100000 || filtro.Tamano is < 1 or > 100 ||
            orden is not ("nombre" or "precio-asc" or "precio-desc"))
            throw new AccessFailure(400, "Revisa los filtros y el orden del catálogo.");

        var normalizado = filtro with { Buscar = string.IsNullOrEmpty(buscar) ? null : buscar, Orden = orden };
        var lectura = await catalogo.ConsultarAsync(tienda.IdNegocio, normalizado, ct);
        var pagina = lectura.Productos;

        if (pagina.Pagina != filtro.Pagina || pagina.Tamano != filtro.Tamano || pagina.Total < 0 ||
            pagina.Items.Length > filtro.Tamano || pagina.Total < pagina.Items.Length ||
            pagina.Items.Any(p => p.IdNegocio != tienda.IdNegocio || p.Estado != "PUBLICADO" ||
                p.Datos.Precio < 0 || p.Datos.Id < 1 ||
                (filtro.CategoriaId is long categoria && p.Datos.IdCategoria != categoria)) ||
            pagina.Items.Select(p => p.Datos.Id).Distinct().Count() != pagina.Items.Length)
            throw new AccessFailure(503, "El catálogo no está disponible en este momento.");

        var productos = pagina.Items.Select(p => p.Datos with
        {
            ImagenPrincipal = TiendaServicio.ImagenSegura(p.Datos.ImagenPrincipal),
            Disponible = p.Datos.ControlaStock ? p.Datos.Disponible : null
        }).ToArray();
        return new CatalogoTiendaPublico(tienda, normalizado,
            new Page<ProductoPublico>(productos, pagina.Total, pagina.Pagina, pagina.Tamano), lectura.Categorias);
    }
}

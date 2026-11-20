using System.Globalization;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Application.Exploracion;

public sealed class ExploracionPublicaServicio(IRepositorioExploracionPublica repositorio)
{
    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("es-PE").CompareInfo;
    private static readonly StringComparer OrdenNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-PE"), true);
    public async Task<ExploracionPortada> PortadaAsync(CancellationToken ct)
    {
        var datos = await LeerAsync(ct);
        return new(Mercados(datos), Categorias(datos.Productos),
            datos.Tiendas.OrderBy(t => t.NombreComercial, OrdenNombre).ThenBy(t => t.IdNegocio).Take(6).Select(t => t with { LogoUrl = Imagen(t.LogoUrl) }).ToArray(),
            datos.Productos.OrderBy(p => p.Producto.Nombre, OrdenNombre).ThenBy(p => p.IdNegocio)
                .ThenBy(p => p.Producto.Id).Take(8).Select(p => Producto(p, datos.Tiendas)).ToArray());
    }
    public async Task<ExploracionPagina> BuscarAsync(ExploracionFiltro filtro, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var limpio = Normalizar(filtro); // Validar antes de consultar al proveedor.
        var datos = await LeerAsync(ct);
        var tiendas = datos.Tiendas.Where(t => limpio.MercadoId is null || t.IdMercado == limpio.MercadoId).ToArray();
        var ids = tiendas.Select(t => t.IdNegocio).ToHashSet();
        var productosMercado = datos.Productos.Where(p => ids.Contains(p.IdNegocio)).ToArray();
        var categorias = Categorias(productosMercado);
        var productos = productosMercado.Where(p => limpio.CategoriaId is null || p.Producto.IdCategoria == limpio.CategoriaId).ToArray();
        var resultados = new List<ExploracionResultado>();
        if (limpio.Tipo is "todos" or "productos")
            resultados.AddRange(productos.Where(p => Coincide(p.Producto.Nombre, limpio.Buscar) ||
                Coincide(p.Producto.Categoria, limpio.Buscar) || Coincide(tiendas.Single(t => t.IdNegocio == p.IdNegocio).NombreComercial, limpio.Buscar))
                .Select(p => Producto(p, tiendas)));
        if (limpio.Tipo is "todos" or "tiendas")
            resultados.AddRange(tiendas.Where(t => (limpio.CategoriaId is null || productos.Any(p => p.IdNegocio == t.IdNegocio)) &&
                (Coincide(t.NombreComercial, limpio.Buscar) || productos.Any(p => p.IdNegocio == t.IdNegocio && Coincide(p.Producto.Categoria, limpio.Buscar))))
                .Select(t => new ExploracionResultado("tienda", t.IdNegocio, t.NombreComercial,
                    "/tiendas/" + t.Slug + (limpio.CategoriaId is long id ? "?categoriaId=" + id.ToString(CultureInfo.InvariantCulture) : ""),
                    t.NombreComercial, t.Mercado, null, Imagen(t.LogoUrl))));
        if (limpio.Tipo is "todos" or "categorias")
            resultados.AddRange(categorias.Where(c => (limpio.CategoriaId is null || c.Id == limpio.CategoriaId) && Coincide(c.Nombre, limpio.Buscar))
                .Select(c => new ExploracionResultado("categoria", c.Id, c.Nombre,
                    "/explorar/buscar?tipo=productos&categoriaId=" + c.Id.ToString(CultureInfo.InvariantCulture) +
                    (limpio.MercadoId is long id ? "&mercadoId=" + id.ToString(CultureInfo.InvariantCulture) : ""),
                    null, null, c.Nombre, null)));
        var ordenados = resultados.OrderBy(r => r.Nombre, OrdenNombre).ThenBy(r => r.Tipo, StringComparer.Ordinal).ThenBy(r => r.Id);
        // Precio solo se aplica al modo productos: no mezclar valores nulos de tiendas/categorías.
        IEnumerable<ExploracionResultado> final = limpio.Orden switch
        {
            "precio-asc" => resultados.OrderBy(r => r.Precio).ThenBy(r => r.Nombre, OrdenNombre).ThenBy(r => r.Id),
            "precio-desc" => resultados.OrderByDescending(r => r.Precio).ThenBy(r => r.Nombre, OrdenNombre).ThenBy(r => r.Id),
            _ => ordenados
        };
        var filas = final.Skip((limpio.Pagina - 1) * limpio.Tamano).Take(limpio.Tamano).ToArray();
        return new(limpio, new(filas, resultados.Count, limpio.Pagina, limpio.Tamano), Mercados(datos), categorias);
    }
    private async Task<ExploracionLectura> LeerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var datos = await repositorio.LeerAsync(ct);
        if (datos.Tiendas.Any(t => t.IdNegocio < 1 || t.IdMercado < 1 || !SlugValido(t.Slug) ||
                string.IsNullOrWhiteSpace(t.NombreComercial) || string.IsNullOrWhiteSpace(t.Mercado)) ||
            datos.Tiendas.Select(t => t.IdNegocio).Distinct().Count() != datos.Tiendas.Length ||
            datos.Tiendas.Select(t => t.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != datos.Tiendas.Length)
            throw NoDisponible();
        var ids = datos.Tiendas.Select(t => t.IdNegocio).ToHashSet();
        if (datos.Productos.Any(p => !ids.Contains(p.IdNegocio) || p.Producto.Id < 1 || p.Producto.IdCategoria < 1 ||
                p.Producto.Precio < 0 || string.IsNullOrWhiteSpace(p.Producto.Nombre) || string.IsNullOrWhiteSpace(p.Producto.Categoria) ||
                string.IsNullOrWhiteSpace(p.Producto.Unidad)) ||
            datos.Productos.Select(p => p.Producto.Id).Distinct().Count() != datos.Productos.Length ||
            datos.Productos.GroupBy(p => p.Producto.IdCategoria).Any(g => g.Select(p => p.Producto.Categoria).Distinct(StringComparer.Ordinal).Count() != 1) ||
            datos.Tiendas.GroupBy(t => t.IdMercado).Any(g => g.Select(t => t.Mercado).Distinct(StringComparer.Ordinal).Count() != 1))
            throw NoDisponible();
        return datos;
    }
    private static ExploracionFiltro Normalizar(ExploracionFiltro f)
    {
        var tipo = f.Tipo?.Trim().ToLowerInvariant(); var orden = f.Orden?.Trim().ToLowerInvariant();
        if (f.Buscar is { Length: > 200 } || f.Buscar?.Any(char.IsControl) == true || f.MercadoId is < 1 ||
            f.CategoriaId is < 1 || f.Pagina is < 1 or > 100000 || f.Tamano is < 1 or > 100 ||
            tipo is not ("todos" or "productos" or "tiendas" or "categorias") ||
            orden is not ("nombre" or "precio-asc" or "precio-desc") || (orden != "nombre" && tipo != "productos"))
            throw new AccessFailure(400, "Revisa la búsqueda, los filtros y la página. El orden por precio requiere el tipo Productos.");
        return f with { Buscar = string.IsNullOrWhiteSpace(f.Buscar) ? null : f.Buscar.Trim(), Tipo = tipo, Orden = orden };
    }
    private static bool Coincide(string texto, string? buscar) => buscar is null ||
        Comparador.IndexOf(texto, buscar, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    private static NamedId[] Mercados(ExploracionLectura datos) => datos.Tiendas.Select(t => new NamedId(t.IdMercado, t.Mercado))
        .DistinctBy(x => x.Id).OrderBy(x => x.Nombre, OrdenNombre).ThenBy(x => x.Id).ToArray();
    private static NamedId[] Categorias(IEnumerable<ProductoExploracion> productos) => productos
        .Select(p => new NamedId(p.Producto.IdCategoria, p.Producto.Categoria)).DistinctBy(x => x.Id)
        .OrderBy(x => x.Nombre, OrdenNombre).ThenBy(x => x.Id).ToArray();
    private static ExploracionResultado Producto(ProductoExploracion p, TiendaResumen[] tiendas)
    {
        var t = tiendas.Single(t => t.IdNegocio == p.IdNegocio); var d = p.Producto;
        return new("producto", d.Id, d.Nombre, "/tiendas/" + t.Slug + "?q=" + Uri.EscapeDataString(d.Nombre) + "#catalogo",
            t.NombreComercial, t.Mercado, d.Categoria, Imagen(d.ImagenPrincipal), d.Precio, d.Unidad,
            d.ControlaStock, d.ControlaStock ? d.Disponible : null);
    }
    private static bool SlugValido(string? s) => s is { Length: > 0 and <= 160 } && s[0] != '-' && s[^1] != '-'
        && !s.Contains("--", StringComparison.Ordinal) && s.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    private static string? Imagen(string? url) => url is { Length: > 0 and <= 500 } && Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == "https" && u.UserInfo.Length == 0 && u.AbsoluteUri.Length <= 500 && u.IsWellFormedOriginalString() ? u.AbsoluteUri : null;
    private static AccessFailure NoDisponible() => new(503, "La búsqueda pública no está disponible en este momento.");
}

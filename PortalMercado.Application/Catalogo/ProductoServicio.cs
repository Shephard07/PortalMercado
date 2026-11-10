using System.ComponentModel.DataAnnotations;
using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Catalogo;

public sealed record ProductoFiltro(long? IdNegocio = null, long? IdCategoria = null, string? Estado = null,
    string? Buscar = null, int Pagina = 1, int Tamano = 20);

public sealed record ProductoResumen(long Id, long IdCategoria, string Categoria, string? Sku, string Nombre,
    decimal Precio, string Unidad, string Estado, bool ControlaStock, decimal? Disponible, string? ImagenPrincipal);

public sealed record ProductosConsulta(NamedId[] Negocios, long? IdNegocio, Page<ProductoResumen> Productos);

public sealed class ProductoDatos
{
    [Range(typeof(long), "1", "9223372036854775807")]
    public long IdCategoria { get; set; }

    [Range(1, int.MaxValue)]
    public int IdUnidad { get; set; }

    [Required, StringLength(200)]
    public string Nombre { get; set; } = "";

    [StringLength(70)]
    public string? Sku { get; set; }

    public string? Descripcion { get; set; }

    [Required]
    public decimal? Precio { get; set; }

    public bool ControlaStock { get; set; }
}

public sealed record ProductoFicha(long Id, long IdNegocio, string Estado, ProductoDatos Datos);

public sealed record PrecioProducto(decimal Precio, DateTime DesdeUtc, DateTime? HastaUtc);
public sealed record InventarioProducto(decimal Existencia, decimal Reservado)
{
    public decimal Disponible => Existencia - Reservado;
}

public sealed record ProductoDetalle(long Id, long IdNegocio, string Nombre, string? Sku, string Categoria,
    string Unidad, string Estado, decimal Precio, bool ControlaStock, InventarioProducto? Inventario,
    PrecioProducto[] Precios, int PaginaPrecios, bool HayMasPrecios);

public sealed record ImagenProducto(long Id, string Url, int Orden, bool Principal);
public sealed record ImagenesProducto(long IdProducto, long IdNegocio, string Producto, ImagenProducto[] Imagenes);
public sealed record ImagenProductoDatos(string Url, int Orden, bool Principal);

public sealed record ProductoEstadoDatos(string EstadoActual, string NuevoEstado);

public interface IRepositorioProductos
{
    Task<Page<ProductoResumen>> ListarAsync(long idNegocio, ProductoFiltro filtro, CancellationToken ct);
    Task<ProductoFicha?> ObtenerAsync(long idNegocio, long id, CancellationToken ct);
    Task<ProductoDetalle?> DetalleAsync(long idNegocio, long id, int pagina, CancellationToken ct);
    Task CambiarEstadoAsync(long idNegocio, long id, ProductoEstadoDatos datos, long actor, string correlacion,
        CancellationToken ct);
    Task<ImagenesProducto?> ImagenesAsync(long idNegocio, long id, CancellationToken ct);
    Task<long> GuardarImagenAsync(long idNegocio, long id, long? idImagen, ImagenProductoDatos datos, long actor,
        string correlacion, CancellationToken ct);
    Task<long> GuardarAsync(long idNegocio, long? id, ProductoDatos datos, long actor, string correlacion, CancellationToken ct);
}

public sealed class ProductoServicio(IUserAdministration usuarios, IRepositorioProductos productos)
{
    public const string PermisoConsultar = "PRODUCTO_VER";
    public const string PermisoGestionar = "PRODUCTO_GESTIONAR";
    public static IReadOnlyList<string> Estados { get; } =
        Array.AsReadOnly(new[] { "BORRADOR", "PUBLICADO", "AGOTADO", "SUSPENDIDO" });

    public async Task<ProductosConsulta> ListarAsync(long actor, string credencial, string host, ProductoFiltro filtro,
        CancellationToken ct)
    {
        (await usuarios.AccessAsync(actor, ct)).Demand(PermisoConsultar);

        var buscar = filtro.Buscar?.Trim();
        var estado = filtro.Estado?.Trim().ToUpperInvariant();
        if (filtro.IdNegocio is < 1 || filtro.IdCategoria is < 1 || buscar is { Length: > 200 } ||
            filtro.Pagina is < 1 or > 100000 || filtro.Tamano is < 1 or > 100 ||
            (!string.IsNullOrEmpty(estado) && !Estados.Contains(estado)))
            throw new AccessFailure(400, "Revisa los filtros y la paginación de productos.");

        var contexto = await usuarios.ContextAsync(actor, credencial, host, ct);
        var id = filtro.IdNegocio ?? contexto.IdNegocioActivo;
        if (id is null)
            return new ProductosConsulta(contexto.Disponibles, null,
                new Page<ProductoResumen>([], 0, filtro.Pagina, filtro.Tamano));

        ExigirNegocio(contexto, id.Value);

        var pagina = await productos.ListarAsync(id.Value, filtro with
        {
            Buscar = string.IsNullOrEmpty(buscar) ? null : buscar,
            Estado = string.IsNullOrEmpty(estado) ? null : estado
        }, ct);
        return new ProductosConsulta(contexto.Disponibles, id, pagina);
    }

    public async Task<NamedId> NegocioParaEdicionAsync(long actor, string credencial, string host, long idNegocio,
        CancellationToken ct)
    {
        var acceso = await usuarios.AccessAsync(actor, ct);
        acceso.Demand(PermisoConsultar);
        acceso.Demand(PermisoGestionar);
        var contexto = await usuarios.ContextAsync(actor, credencial, host, ct);
        return ExigirNegocio(contexto, idNegocio);
    }

    public async Task<ProductoFicha> ObtenerAsync(long actor, string credencial, string host, long idNegocio,
        long id, CancellationToken ct)
    {
        (await usuarios.AccessAsync(actor, ct)).Demand(PermisoConsultar);
        var contexto = await usuarios.ContextAsync(actor, credencial, host, ct);
        ExigirNegocio(contexto, idNegocio);
        return await productos.ObtenerAsync(idNegocio, id, ct)
            ?? throw new AccessFailure(404, "No se encontró el producto en ese negocio.");
    }

    public async Task<ProductoDetalle> DetalleAsync(long actor, string credencial, string host, long idNegocio,
        long id, int pagina, CancellationToken ct)
    {
        (await usuarios.AccessAsync(actor, ct)).Demand(PermisoConsultar);
        var contexto = await usuarios.ContextAsync(actor, credencial, host, ct);
        ExigirNegocio(contexto, idNegocio);
        if (id < 1 || pagina is < 1 or > 100000)
            throw new AccessFailure(400, "Revisa el producto y la página del historial.");

        return await productos.DetalleAsync(idNegocio, id, pagina, ct)
            ?? throw new AccessFailure(404, "No se encontró el producto en ese negocio.");
    }

    public async Task<long> GuardarAsync(long actor, string credencial, string host, long idNegocio, long? id,
        ProductoDatos datos, string correlacion, CancellationToken ct)
    {
        await NegocioParaEdicionAsync(actor, credencial, host, idNegocio, ct);

        var nombre = datos.Nombre?.Trim();
        var sku = datos.Sku?.Trim();
        if (id is < 1 || datos.IdCategoria < 1 || datos.IdUnidad < 1 || string.IsNullOrEmpty(nombre) ||
            nombre.Length > 200 || (sku is not null && (sku.Length > 70 || sku.Any(c => c is < ' ' or > '~'))) ||
            datos.Precio is not decimal precio || precio < 0 || precio > 9999999999999999.99m ||
            decimal.Round(precio, 2) != precio)
            throw new AccessFailure(400, "Revisa la categoría, la unidad, el nombre, el SKU y el precio.");

        return await productos.GuardarAsync(idNegocio, id, new ProductoDatos
        {
            IdCategoria = datos.IdCategoria,
            IdUnidad = datos.IdUnidad,
            Nombre = nombre,
            Sku = string.IsNullOrEmpty(sku) ? null : sku,
            Descripcion = string.IsNullOrWhiteSpace(datos.Descripcion) ? null : datos.Descripcion.Trim(),
            Precio = precio,
            ControlaStock = datos.ControlaStock
        }, actor, correlacion, ct);
    }

    public async Task<ImagenesProducto> ImagenesAsync(long actor, string credencial, string host, long idNegocio,
        long id, CancellationToken ct)
    {
        (await usuarios.AccessAsync(actor, ct)).Demand(PermisoConsultar);
        var contexto = await usuarios.ContextAsync(actor, credencial, host, ct);
        ExigirNegocio(contexto, idNegocio);

        return await productos.ImagenesAsync(idNegocio, id, ct)
            ?? throw new AccessFailure(404, "No se encontró el producto en ese negocio.");
    }

    public async Task<long> GuardarImagenAsync(long actor, string credencial, string host, long idNegocio,
        long id, long? idImagen, ImagenProductoDatos datos, string correlacion, CancellationToken ct)
    {
        await NegocioParaEdicionAsync(actor, credencial, host, idNegocio, ct);

        var url = datos.Url?.Trim();
        if (id < 1 || idImagen is < 1 || datos.Orden < 0 || url is not { Length: > 0 and <= 500 } ||
            !Uri.TryCreate(url, UriKind.Absolute, out var enlace) || enlace.Scheme != Uri.UriSchemeHttps ||
            !enlace.IsWellFormedOriginalString() || enlace.UserInfo.Length != 0 || enlace.AbsoluteUri.Length > 500)
            throw new AccessFailure(400, "Ingresa una URL HTTPS de hasta 500 caracteres y un orden igual o mayor que cero.");

        return await productos.GuardarImagenAsync(idNegocio, id, idImagen, datos with { Url = enlace.AbsoluteUri },
            actor, correlacion, ct);
    }

    public static IReadOnlyList<string> DestinosDeEstado(string estado) => estado.Trim().ToUpperInvariant() switch
    {
        "BORRADOR" => ["PUBLICADO"],
        "PUBLICADO" => ["AGOTADO", "SUSPENDIDO"],
        "AGOTADO" => ["PUBLICADO", "SUSPENDIDO"],
        "SUSPENDIDO" => ["BORRADOR"],
        _ => []
    };

    public async Task CambiarEstadoAsync(long actor, string credencial, string host, long idNegocio, long id,
        ProductoEstadoDatos datos, string correlacion, CancellationToken ct)
    {
        await NegocioParaEdicionAsync(actor, credencial, host, idNegocio, ct);

        var actual = datos.EstadoActual?.Trim().ToUpperInvariant();
        var destino = datos.NuevoEstado?.Trim().ToUpperInvariant();
        if (id < 1 || actual is null || destino is null || !Estados.Contains(actual) || !Estados.Contains(destino))
            throw new AccessFailure(400, "Selecciona estados válidos para el producto.");

        if (!DestinosDeEstado(actual).Contains(destino))
            throw new AccessFailure(409, "La transición solicitada no está permitida.");

        await productos.CambiarEstadoAsync(idNegocio, id, new(actual, destino), actor, correlacion, ct);
    }

    private static NamedId ExigirNegocio(BusinessContext contexto, long idNegocio) =>
        contexto.Disponibles.FirstOrDefault(negocio => negocio.Id == idNegocio)
        ?? throw new AccessFailure(403, "No tienes acceso al catálogo de ese negocio.");
}

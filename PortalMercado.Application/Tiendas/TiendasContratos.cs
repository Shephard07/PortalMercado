using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Tiendas;

public sealed record TiendaFiltro(long? MercadoId = null, long? CategoriaId = null,
    string? Buscar = null, int Pagina = 1, int Tamano = 12);

public sealed record TiendaOpciones(NamedId[] Mercados, NamedId[] Categorias);

public sealed record TiendaResumen(long IdNegocio, string Slug, string NombreComercial,
    long IdMercado, string Mercado, string? LogoUrl);

public sealed record TiendaPublica(long IdNegocio, string Slug, string NombreComercial,
    long IdMercado, string Mercado, string? Descripcion, string? LogoUrl, string? Horario,
    string? Telefono, string? Correo)
{
    public UbicacionTienda[] Ubicaciones { get; init; } = [];
}

// Proyección interna del contrato de lectura. No debe devolverse desde controladores.
// Visibilidad y consentimiento de contacto son decisiones explícitas del proveedor de datos.
public sealed record TiendaLectura(long IdNegocio, string Slug, string NombreComercial,
    long IdMercado, string Mercado, string Estado, bool MercadoActivo, bool VisibilidadAprobada,
    string? Descripcion, string? LogoUrl, string? Horario, string? Telefono, bool TelefonoPublico,
    string? Correo, bool CorreoPublico, long[] CategoriasPublicadas);

public interface IRepositorioTiendas
{
    // Opciones procedentes exclusivamente de tiendas visibles y sus productos publicados.
    Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct);

    // Aplicar visibilidad y filtros ANTES del total y de la paginación.
    // CategoriasPublicadas contiene categorías de productos PUBLICADO del negocio.
    // Orden estable por nombre comercial y, en empates, IdNegocio.
    Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct);

    // Devuelve hasta dos coincidencias, incluidas las ocultas, para detectar ambigüedad.
    // Comparar slug completo normalizado. Nunca resolver por la primera coincidencia.
    Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct);
}

public interface ITiendaService
{
    Task<TiendaOpciones> OpcionesAsync(CancellationToken ct);
    Task<Page<TiendaResumen>> ListarAsync(TiendaFiltro filtro, CancellationToken ct);
    Task<TiendaPublica> ObtenerAsync(string slug, CancellationToken ct);
}

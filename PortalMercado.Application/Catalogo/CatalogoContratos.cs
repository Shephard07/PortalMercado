using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Catalogo;

public static class PermisosCatalogo
{
    public const string ConsultarCategorias = "CATEGORIA_VER";
    public const string MantenerCategorias = "CATEGORIA_GESTIONAR";
    public const string ConsultarUnidades = "UNIDAD_MEDIDA_VER";
}

public sealed record CategoriaDto(long Id, long? IdPadre, string? NombrePadre, string Nombre, string Slug, bool Activa);
public sealed record UnidadMedidaDto(int Id, string Codigo, string Nombre, bool PermiteFraccion);
public sealed record CategoriaFiltro(string? Buscar = null, bool? Activa = null, int Pagina = 1, int Tamano = 20);
public sealed record CategoriaDatos(string Nombre, string Slug, long? IdPadre);

public interface IRepositorioCategorias
{
    // Orden estable por nombre e ID. Filtrado, conteo y paginación en el servidor.
    Task<Page<CategoriaDto>> ListarAsync(CategoriaFiltro filtro, CancellationToken ct);

    // Validar dentro de la misma transacción el slug global, la existencia del padre
    // y la ausencia de ciclos, incluso ante ediciones concurrentes.
    // Un ID nulo crea una categoría. La edición conserva su estado.
    // La escritura y su auditoría deben confirmarse juntas.
    Task<CategoriaDto> GuardarAsync(long? id, CategoriaDatos datos, long actor, string correlacion, CancellationToken ct);
    Task CambiarEstadoAsync(long id, bool activa, long actor, string correlacion, CancellationToken ct);
}

public interface IRepositorioUnidadesMedida
{
    Task<UnidadMedidaDto[]> ListarAsync(CancellationToken ct);
}

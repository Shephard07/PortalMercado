using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Catalogo;

public sealed class CategoriaServicio(IUserAdministration usuarios, IRepositorioCategorias categorias)
{
    public async Task<Page<CategoriaDto>> ListarAsync(long actor, CategoriaFiltro filtro, CancellationToken ct)
    {
        var acceso = await usuarios.AccessAsync(actor, ct);
        acceso.Demand(PermisosCatalogo.ConsultarCategorias);

        var buscar = filtro.Buscar?.Trim();
        if (buscar is { Length: > 160 } || filtro.Pagina is < 1 or > 100000 || filtro.Tamano is < 1 or > 100)
            throw new AccessFailure(400, "Revisa la búsqueda y los datos de paginación.");

        return await categorias.ListarAsync(filtro with { Buscar = string.IsNullOrEmpty(buscar) ? null : buscar }, ct);
    }

    public Task<CategoriaDto> CrearAsync(long actor, CategoriaDatos datos, string correlacion, CancellationToken ct) =>
        GuardarAsync(actor, null, datos, correlacion, ct);

    public Task<CategoriaDto> EditarAsync(long actor, long id, CategoriaDatos datos, string correlacion, CancellationToken ct) =>
        GuardarAsync(actor, id, datos, correlacion, ct);

    public async Task CambiarEstadoAsync(long actor, long id, bool activa, string correlacion, CancellationToken ct)
    {
        var acceso = await usuarios.AccessAsync(actor, ct);
        acceso.Demand(PermisosCatalogo.MantenerCategorias);

        if (id < 1)
            throw new AccessFailure(400, "Selecciona una categoría válida.");

        await categorias.CambiarEstadoAsync(id, activa, actor, correlacion, ct);
    }

    private async Task<CategoriaDto> GuardarAsync(long actor, long? id, CategoriaDatos datos, string correlacion, CancellationToken ct)
    {
        var acceso = await usuarios.AccessAsync(actor, ct);
        acceso.Demand(PermisosCatalogo.MantenerCategorias);

        if (id is < 1 || datos.IdPadre is < 1)
            throw new AccessFailure(400, "Selecciona una categoría válida.");
        if (id.HasValue && id == datos.IdPadre)
            throw new AccessFailure(400, "Una categoría no puede ser su propio padre.");

        var nombre = datos.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre) || nombre.Length > 130)
            throw new AccessFailure(400, "El nombre debe tener entre 1 y 130 caracteres.");

        var slug = datos.Slug?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(slug) || slug.Length > 160 || !SlugValido(slug))
            throw new AccessFailure(400, "El slug debe tener hasta 160 caracteres y usar letras de a-z, números y guiones separadores.");

        return await categorias.GuardarAsync(id, new CategoriaDatos(nombre, slug, datos.IdPadre), actor, correlacion, ct);
    }

    private static bool SlugValido(string slug) =>
        slug[0] != '-' && slug[^1] != '-' && !slug.Contains("--", StringComparison.Ordinal) &&
        slug.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}

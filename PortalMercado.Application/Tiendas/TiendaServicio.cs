using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Tiendas;

public sealed class TiendaServicio(IRepositorioTiendas tiendas,
    IRepositorioUbicacionesTienda? ubicaciones = null, TimeProvider? reloj = null) : ITiendaService
{
    public Task<TiendaOpciones> OpcionesAsync(CancellationToken ct) => tiendas.OpcionesPublicasAsync(ct);

    public async Task<Page<TiendaResumen>> ListarAsync(TiendaFiltro filtro, CancellationToken ct)
    {
        var buscar = filtro.Buscar?.Trim();
        if (filtro.MercadoId is < 1 || filtro.CategoriaId is < 1 || buscar is { Length: > 180 } ||
            filtro.Pagina is < 1 or > 100000 || filtro.Tamano is < 1 or > 100)
            throw new AccessFailure(400, "Revisa los filtros y la página del directorio.");

        var pagina = await tiendas.ListarPublicasAsync(filtro with
        {
            Buscar = string.IsNullOrEmpty(buscar) ? null : buscar
        }, ct);

        // Un adaptador que incumple el contrato no puede publicar filas privadas.
        if (pagina.Items.Any(t => !EsPublica(t)) || pagina.Total < 0 ||
            pagina.Items.Length > filtro.Tamano || pagina.Total < pagina.Items.Length ||
            pagina.Pagina != filtro.Pagina || pagina.Tamano != filtro.Tamano)
            throw new AccessFailure(503, "El directorio no está disponible en este momento.");

        return new Page<TiendaResumen>(pagina.Items.Select(t => new TiendaResumen(
            t.IdNegocio, t.Slug, t.NombreComercial, t.IdMercado, t.Mercado, ImagenSegura(t.LogoUrl))).ToArray(),
            pagina.Total, pagina.Pagina, pagina.Tamano);
    }

    public async Task<TiendaPublica> ObtenerAsync(string slug, CancellationToken ct)
    {
        var normalizado = slug?.Trim().ToLowerInvariant();
        if (!SlugValido(normalizado))
            throw NoEncontrada();

        var coincidencias = await tiendas.BuscarPorSlugAsync(normalizado!, ct);
        if (coincidencias.Length != 1)
            throw NoEncontrada();

        var tienda = coincidencias[0];
        if (!EsPublica(tienda) || !string.Equals(tienda.Slug, normalizado, StringComparison.OrdinalIgnoreCase))
            throw NoEncontrada();

        return new TiendaPublica(tienda.IdNegocio, tienda.Slug, tienda.NombreComercial,
            tienda.IdMercado, tienda.Mercado, tienda.Descripcion, ImagenSegura(tienda.LogoUrl), tienda.Horario,
            tienda.TelefonoPublico ? tienda.Telefono : null, tienda.CorreoPublico ? tienda.Correo : null)
        {
            Ubicaciones = await UbicacionesAsync(tienda, ct)
        };
    }

    private async Task<UbicacionTienda[]> UbicacionesAsync(TiendaLectura tienda, CancellationToken ct)
    {
        if (ubicaciones is null)
            return [];

        // Regla provisional de demostración. Confirmar zona y límites con A/DBA.
        var ahora = (reloj ?? TimeProvider.System).GetUtcNow();
        var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            ahora, TimeZoneInfo.FindSystemTimeZoneById("America/Lima")).DateTime);
        var filas = await ubicaciones.ConsultarAsync(tienda.IdNegocio, fecha, ct);
        if (filas.Any(u => u.IdNegocio != tienda.IdNegocio || u.IdMercado != tienda.IdMercado))
            throw new AccessFailure(503, "La información de la tienda no está disponible en este momento.");

        return filas.Where(u => u.MercadoActivo && u.SectorActivo && u.PuestoHabilitado &&
                u.Desde <= fecha && (u.Hasta is null || u.Hasta >= fecha))
            .DistinctBy(u => u.IdPuesto)
            .OrderBy(u => u.Sector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Puesto, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.IdPuesto)
            .Select(u => new UbicacionTienda(u.Mercado, u.Sector, u.Puesto, u.Referencia)).ToArray();
    }

    private static bool EsPublica(TiendaLectura tienda) =>
        tienda.Estado == "ACTIVO" && tienda.MercadoActivo && tienda.VisibilidadAprobada;

    private static bool SlugValido(string? slug) => slug is { Length: > 0 and <= 160 } &&
        slug[0] != '-' && slug[^1] != '-' && !slug.Contains("--", StringComparison.Ordinal) &&
        slug.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    internal static string? ImagenSegura(string? url)
    {
        if (url is not { Length: > 0 and <= 500 } ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsWellFormedOriginalString() || uri.UserInfo.Length != 0 || uri.AbsoluteUri.Length > 500)
            return null;
        return uri.AbsoluteUri;
    }

    private static AccessFailure NoEncontrada() => new(404, "No se encontró la tienda.");
}

using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;

namespace PortalMercado.Tests.Catalogo;

public sealed class CatalogoServicioTests
{
    [Fact]
    public async Task Crear_normaliza_los_datos_y_conserva_el_actor_para_auditoria()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new CategoriaServicio(new AccesoPrueba(PermisosCatalogo.MantenerCategorias), repositorio);

        var resultado = await servicio.CrearAsync(7, new("  Frutas  ", " FRUTAS-FRESCAS ", 3), "corr-7", default);

        Assert.Equal("Frutas", resultado.Nombre);
        Assert.Equal("frutas-frescas", resultado.Slug);
        Assert.Equal<long?>(3, resultado.IdPadre);
        Assert.Equal(7L, repositorio.Actor);
        Assert.Equal("corr-7", repositorio.Correlacion);
    }

    [Fact]
    public async Task Editar_rechaza_una_categoria_como_su_propio_padre()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new CategoriaServicio(new AccesoPrueba(PermisosCatalogo.MantenerCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.EditarAsync(7, 3, new("Frutas", "frutas", 3), "corr-7", default));

        Assert.Equal(400, error.Status);
        Assert.Equal(0, repositorio.Llamadas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("con espacios")]
    [InlineData("-frutas")]
    [InlineData("frutas--frescas")]
    public async Task Crear_rechaza_slugs_invalidos(string slug)
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new CategoriaServicio(new AccesoPrueba(PermisosCatalogo.MantenerCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.CrearAsync(7, new("Frutas", slug, null), "corr-7", default));

        Assert.Equal(400, error.Status);
        Assert.Equal(0, repositorio.Llamadas);
    }

    [Fact]
    public async Task El_permiso_de_lectura_no_permite_cambiar_el_estado()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new CategoriaServicio(new AccesoPrueba(PermisosCatalogo.ConsultarCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.CambiarEstadoAsync(7, 3, false, "corr-7", default));

        Assert.Equal(403, error.Status);
        Assert.Equal(0, repositorio.Llamadas);
    }

    [Fact]
    public async Task Listar_categorias_rechaza_paginacion_invalida()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new CategoriaServicio(new AccesoPrueba(PermisosCatalogo.ConsultarCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(7, new(Pagina: 0), default));

        Assert.Equal(400, error.Status);
        Assert.Equal(0, repositorio.Llamadas);
    }

    [Fact]
    public async Task Listar_unidades_exige_su_propio_permiso()
    {
        var repositorio = new RepositorioPrueba();
        var servicio = new UnidadMedidaServicio(new AccesoPrueba(PermisosCatalogo.ConsultarCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() => servicio.ListarAsync(7, default));

        Assert.Equal(403, error.Status);
        Assert.Equal(0, repositorio.Llamadas);
    }

    [Fact]
    public async Task Productos_rechaza_un_negocio_ajeno_aunque_exista_el_permiso()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(new AccesoPrueba(ProductoServicio.PermisoConsultar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(7, "sesion", "MVC", new(IdNegocio: 2), default));

        Assert.Equal(403, error.Status);
        Assert.Null(repositorio.Negocio);
    }

    [Fact]
    public async Task Productos_usa_el_negocio_activo_y_normaliza_los_filtros()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(new AccesoPrueba(ProductoServicio.PermisoConsultar), repositorio);

        var resultado = await servicio.ListarAsync(7, "sesion", "API", new(Estado: " borrador ", Buscar: " mango "), default);

        Assert.Equal<long?>(1, repositorio.Negocio);
        Assert.Equal<long?>(1, resultado.IdNegocio);
        Assert.Equal("BORRADOR", repositorio.Filtro?.Estado);
        Assert.Equal("mango", repositorio.Filtro?.Buscar);
    }

    [Fact]
    public async Task Productos_exige_el_permiso_de_consulta()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(new AccesoPrueba(PermisosCatalogo.ConsultarCategorias), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ListarAsync(7, "sesion", "API", new(), default));

        Assert.Equal(403, error.Status);
        Assert.Null(repositorio.Negocio);
    }

    [Fact]
    public async Task Productos_impide_guardar_en_un_negocio_ajeno()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(7, "sesion", "API", 2, 42, DatosProducto(2.50m), "correlacion", default));

        Assert.Equal(403, error.Status);
        Assert.Null(repositorio.Negocio);
    }

    [Fact]
    public async Task Productos_impide_guardar_con_solo_permiso_de_consulta()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(new AccesoPrueba(ProductoServicio.PermisoConsultar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(7, "sesion", "MVC", 1, null, DatosProducto(2.50m), "correlacion", default));

        Assert.Equal(403, error.Status);
        Assert.Null(repositorio.Negocio);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.125)]
    public async Task Productos_rechaza_precios_invalidos_sin_escribir(double precio)
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(7, "sesion", "MVC", 1, null, DatosProducto((decimal)precio), "correlacion", default));

        Assert.Equal(400, error.Status);
        Assert.Null(repositorio.Negocio);
    }

    [Fact]
    public async Task Productos_impide_ver_historial_de_un_negocio_ajeno()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(new AccesoPrueba(ProductoServicio.PermisoConsultar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.DetalleAsync(7, "sesion", "API", 2, 42, 1, default));

        Assert.Equal(403, error.Status);
    }

    [Theory]
    [InlineData("http://example.invalid/imagen.png")]
    [InlineData("https://usuario:clave@example.invalid/imagen.png")]
    public async Task Imagen_rechaza_urls_invalidas_sin_guardar(string url)
    {
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), new ProductosPrueba());

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarImagenAsync(7, "sesion", "MVC", 1, 42, null, new(url, 0, true), "correlacion", default));

        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Imagen_no_puede_guardarse_en_un_negocio_ajeno()
    {
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), new ProductosPrueba());

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarImagenAsync(7, "sesion", "API", 2, 42, null,
                new("https://example.invalid/imagen.png", 0, true), "correlacion", default));

        Assert.Equal(403, error.Status);
    }

    [Fact]
    public async Task Publicacion_rechaza_el_cambio_directo_de_suspendido_a_publicado()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.CambiarEstadoAsync(7, "sesion", "MVC", 1, 42, new("SUSPENDIDO", "PUBLICADO"), "correlacion", default));

        Assert.Equal(409, error.Status);
        Assert.Null(repositorio.EstadoSolicitado);
    }

    [Fact]
    public async Task Publicacion_rechaza_el_cambio_de_estado_en_otro_negocio()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), repositorio);

        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.CambiarEstadoAsync(7, "sesion", "API", 2, 42, new("BORRADOR", "PUBLICADO"), "correlacion", default));

        Assert.Equal(403, error.Status);
        Assert.Null(repositorio.EstadoSolicitado);
    }

    [Fact]
    public async Task Publicacion_normaliza_y_envia_una_transicion_permitida()
    {
        var repositorio = new ProductosPrueba();
        var servicio = new ProductoServicio(
            new AccesoPrueba(ProductoServicio.PermisoConsultar, ProductoServicio.PermisoGestionar), repositorio);

        await servicio.CambiarEstadoAsync(7, "sesion", "MVC", 1, 42,
            new(" borrador ", "publicado"), "correlacion", default);

        Assert.Equal(new ProductoEstadoDatos("BORRADOR", "PUBLICADO"), repositorio.EstadoSolicitado);
    }

    private static ProductoDatos DatosProducto(decimal precio) => new()
    {
        IdCategoria = 1, IdUnidad = 1, Nombre = "Mango de prueba", Precio = precio
    };

    private sealed class ProductosPrueba : IRepositorioProductos
    {
        public long? Negocio { get; private set; }
        public ProductoFiltro? Filtro { get; private set; }
        public ProductoEstadoDatos? EstadoSolicitado { get; private set; }

        public Task CambiarEstadoAsync(long idNegocio, long id, ProductoEstadoDatos datos, long actor,
            string correlacion, CancellationToken ct)
        {
            EstadoSolicitado = datos;
            return Task.CompletedTask;
        }


        public Task<ProductoFicha?> ObtenerAsync(long idNegocio, long id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ImagenesProducto?> ImagenesAsync(long idNegocio, long id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<long> GuardarImagenAsync(long idNegocio, long id, long? idImagen, ImagenProductoDatos datos,
            long actor, string correlacion, CancellationToken ct) =>
            throw new InvalidOperationException("El repositorio no debía recibir esta escritura.");

        public Task<ProductoDetalle?> DetalleAsync(long idNegocio, long id, int pagina, CancellationToken ct) =>
            throw new InvalidOperationException("El repositorio no debía recibir esta consulta.");

        public Task<long> GuardarAsync(long idNegocio, long? id, ProductoDatos datos, long actor, string correlacion,
            CancellationToken ct)
        {
            Negocio = idNegocio;
            return Task.FromResult(id ?? 42L);
        }

        public Task<Page<ProductoResumen>> ListarAsync(long idNegocio, ProductoFiltro filtro, CancellationToken ct)
        {
            Negocio = idNegocio;
            Filtro = filtro;
            return Task.FromResult(new Page<ProductoResumen>([], 0, filtro.Pagina, filtro.Tamano));
        }
    }

    private sealed class RepositorioPrueba : IRepositorioCategorias, IRepositorioUnidadesMedida
    {
        public int Llamadas { get; private set; }
        public long Actor { get; private set; }
        public string? Correlacion { get; private set; }

        public Task<CategoriaDto> GuardarAsync(long? id, CategoriaDatos datos, long actor, string correlacion, CancellationToken ct)
        {
            Llamadas++;
            Actor = actor;
            Correlacion = correlacion;
            return Task.FromResult(new CategoriaDto(id ?? 4, datos.IdPadre, null, datos.Nombre, datos.Slug, true));
        }

        public Task<Page<CategoriaDto>> ListarAsync(CategoriaFiltro filtro, CancellationToken ct) =>
            throw new InvalidOperationException("El repositorio no debía recibir esta consulta.");
        public Task CambiarEstadoAsync(long id, bool activa, long actor, string correlacion, CancellationToken ct) =>
            throw new InvalidOperationException("El repositorio no debía recibir este cambio.");
        public Task<UnidadMedidaDto[]> ListarAsync(CancellationToken ct) =>
            throw new InvalidOperationException("El repositorio no debía recibir esta consulta.");
    }

    private sealed class AccesoPrueba(params string[] permisos) : IUserAdministration
    {
        public Task<AccessSnapshot> AccessAsync(long actor, CancellationToken ct) =>
            Task.FromResult(new AccessSnapshot(actor, true, ["VENDEDOR"], permisos, [], [1]));
        public Task<Page<UserDto>> ListAsync(long actor, int page, int size, CancellationToken ct) => throw new NotSupportedException();
        public Task<UserDto> GetAsync(long actor, long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminCatalog> CatalogAsync(long actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<UserDto> CreateAsync(long actor, CreateUserRequest request, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task EditAsync(long actor, long id, EditUserRequest request, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task StateAsync(long actor, long id, bool active, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task RolesAsync(long actor, long id, int[] roles, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task BusinessesAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task MarketsAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task PermissionsAsync(long actor, int role, int[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task<BusinessContext> ContextAsync(long actor, string credential, string host, CancellationToken ct) =>
            Task.FromResult(new BusinessContext([new NamedId(1, "Negocio asignado")], 1));
        public Task SelectContextAsync(long actor, string credential, string host, long business, string correlation, CancellationToken ct) => throw new NotSupportedException();
    }
}

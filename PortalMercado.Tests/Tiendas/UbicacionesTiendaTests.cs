using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class UbicacionesTiendaTests
{
    private static readonly DateOnly Fecha = new(2026, 11, 12);

    [Theory]
    [InlineData(-30, null, true)]
    [InlineData(0, null, true)]
    [InlineData(0, 0, true)]
    [InlineData(-30, 0, true)]
    [InlineData(-30, -1, false)]
    [InlineData(1, null, false)]
    [InlineData(1, 2, false)]
    [InlineData(-1, -2, false)]
    public async Task Evalua_vigencia_con_limites_inclusivos(int inicio, int? fin, bool visible)
    {
        var fila = Fila() with { Desde = Fecha.AddDays(inicio), Hasta = fin is int dias ? Fecha.AddDays(dias) : null };
        var tienda = await Servicio(new RepositorioControlado([fila])).ObtenerAsync("fruteria-el-sol", default);
        Assert.Equal(visible ? 1 : 0, tienda.Ubicaciones.Length);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Excluye_mercado_sector_o_puesto_inhabilitado(bool mercado, bool sector, bool puesto)
    {
        var fila = Fila() with { MercadoActivo = mercado, SectorActivo = sector, PuestoHabilitado = puesto };
        var tienda = await Servicio(new RepositorioControlado([fila])).ObtenerAsync("fruteria-el-sol", default);
        Assert.Empty(tienda.Ubicaciones);
    }

    [Theory]
    [InlineData(1002, 100)]
    [InlineData(1001, 101)]
    public async Task Rechaza_una_ubicacion_de_otro_negocio_o_mercado(long negocio, long mercado)
    {
        var fila = Fila() with { IdNegocio = negocio, IdMercado = mercado };
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            Servicio(new RepositorioControlado([fila])).ObtenerAsync("fruteria-el-sol", default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain(fila.Puesto, error.Message);
    }

    [Fact]
    public async Task Ordena_y_evita_los_puestos_repetidos()
    {
        var primero = Fila() with { Puesto = "A-102" };
        var segundo = Fila() with { IdPuesto = 502, Puesto = "A-101" };
        var tienda = await Servicio(new RepositorioControlado([primero, segundo, primero]))
            .ObtenerAsync("fruteria-el-sol", default);
        Assert.Equal(new[] { "A-101", "A-102" }, tienda.Ubicaciones.Select(u => u.Puesto).ToArray());
    }

    [Theory]
    [InlineData("tienda-oculta")]
    [InlineData("tienda-suspendida")]
    [InlineData("inexistente")]
    public async Task Valida_visibilidad_antes_de_consultar_asignaciones(string slug)
    {
        var repositorio = new RepositorioControlado([Fila()]);
        var error = await Assert.ThrowsAsync<AccessFailure>(() => Servicio(repositorio).ObtenerAsync(slug, default));
        Assert.Equal(404, error.Status);
        Assert.Equal(0, repositorio.Consultas);
    }

    [Theory]
    [InlineData(2, 12)]
    [InlineData(6, 13)]
    public async Task Calcula_fecha_comercial_en_lima_cerca_del_cambio_de_dia(int horaUtc, int diaLocal)
    {
        var repositorio = new RepositorioControlado([]);
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 13, horaUtc, 0, 0, TimeSpan.Zero));
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion(), repositorio, reloj);
        await servicio.ObtenerAsync("fruteria-el-sol", default);
        Assert.Equal<DateOnly?>(new DateOnly(2026, 11, diaLocal), repositorio.FechaConsultada);
    }

    [Fact]
    public async Task La_demostracion_conserva_solo_los_dos_puestos_vigentes()
    {
        var servicio = new TiendaServicio(new RepositorioTiendasDemostracion(),
            new RepositorioUbicacionesDemostracion(), new RelojFijo(
                new DateTimeOffset(2026, 11, 12, 17, 0, 0, TimeSpan.Zero)));
        var fruteria = await servicio.ObtenerAsync("fruteria-el-sol", default);
        Assert.Equal(new[] { "A-101", "A-102" }, fruteria.Ubicaciones.Select(u => u.Puesto).ToArray());
        Assert.Empty((await servicio.ObtenerAsync("abarrotes-lima", default)).Ubicaciones);
    }

    private static TiendaServicio Servicio(IRepositorioUbicacionesTienda repositorio) =>
        new(new RepositorioTiendasDemostracion(), repositorio,
            new RelojFijo(new DateTimeOffset(2026, 11, 12, 17, 0, 0, TimeSpan.Zero)));

    private static UbicacionTiendaLectura Fila() =>
        new(1001, 100, 501, "Mercado de demostración Centro", "Frutas y verduras", "A-101",
            "Ingreso principal.", true, true, true, Fecha.AddDays(-1), null);

    private sealed class RelojFijo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }

    private sealed class RepositorioControlado(UbicacionTiendaLectura[] filas) : IRepositorioUbicacionesTienda
    {
        public int Consultas { get; private set; }
        public DateOnly? FechaConsultada { get; private set; }

        public Task<UbicacionTiendaLectura[]> ConsultarAsync(long idNegocio, DateOnly fecha, CancellationToken ct)
        {
            Consultas++;
            FechaConsultada = fecha;
            return Task.FromResult(filas);
        }
    }
}

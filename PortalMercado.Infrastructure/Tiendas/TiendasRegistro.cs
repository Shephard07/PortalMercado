using Microsoft.Extensions.Configuration;
using PortalMercado.Infrastructure.Exploracion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

public sealed record TiendasModo(bool Demostracion, bool SqlLocal = false);

public static class TiendasRegistro
{
    public static IServiceCollection AddTiendas(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        var demostracion = config.GetValue<bool>("Tiendas:Demostracion");
        var sqlLocal = config.GetValue<bool>("Tiendas:SqlLocal");
        if (sqlLocal && (demostracion || !environment.IsDevelopment()))
            throw new InvalidOperationException("Tiendas SQL local requiere Development y demostración desactivada.");
        if (sqlLocal)
            services.AddSingleton(TiendasSqlLocalOpciones.Crear(config.GetConnectionString("PortalMercado")));
        if (demostracion && !environment.IsDevelopment())
            throw new InvalidOperationException("El modo de demostración de tiendas requiere Development.");

        var negocioLocal = config.GetValue<long?>("Tiendas:NegocioLocalDemostracion");
        if (negocioLocal is <= 0 || (negocioLocal.HasValue && !demostracion))
            throw new InvalidOperationException("El vínculo local requiere demostración y un ID de negocio positivo.");
        services.AddSingleton(new TiendasPerfilOpciones(negocioLocal));
        services.AddSingleton(new TiendasModo(demostracion, sqlLocal));
        services.AddScoped<PerfilTiendaServicio>();
        services.AddScoped<ITiendaService, TiendaServicio>();
        services.AddScoped<CatalogoTiendaServicio>();
        if (demostracion)
        {
            services.AddSingleton<RepositorioTiendasDemostracion>();
            services.AddSingleton<IRepositorioTiendas>(sp => sp.GetRequiredService<RepositorioTiendasDemostracion>());
            services.AddScoped<IRepositorioPerfilTienda, RepositorioPerfilDemostracion>();
            services.AddScoped<IRepositorioCatalogoPublico, RepositorioCatalogoDemostracion>();
            services.AddScoped<IRepositorioUbicacionesTienda, RepositorioUbicacionesDemostracion>();
        }
        else if (sqlLocal)
        {
            services.AddScoped<RepositorioTiendasSqlLocal>();
            services.AddScoped<IRepositorioTiendas>(sp => sp.GetRequiredService<RepositorioTiendasSqlLocal>());
            services.AddScoped<IRepositorioPerfilTienda>(sp => sp.GetRequiredService<RepositorioTiendasSqlLocal>());
            services.AddScoped<IRepositorioCatalogoPublico>(sp => sp.GetRequiredService<RepositorioTiendasSqlLocal>());
            services.AddScoped<IRepositorioUbicacionesTienda>(sp => sp.GetRequiredService<RepositorioTiendasSqlLocal>());
        }
        else
        {
            services.AddScoped<IRepositorioTiendas>(_ => new RepositorioTiendasNoDisponible());
            services.AddScoped<IRepositorioPerfilTienda>(_ => new RepositorioTiendasNoDisponible());
            services.AddScoped<IRepositorioCatalogoPublico>(_ => new RepositorioTiendasNoDisponible());
            services.AddScoped<IRepositorioUbicacionesTienda>(_ => new RepositorioTiendasNoDisponible());
        }
        services.AddExploracionPublica();
        return services;
    }

    private sealed class RepositorioTiendasNoDisponible : IRepositorioTiendas, IRepositorioCatalogoPublico,
        IRepositorioUbicacionesTienda, IRepositorioPerfilTienda
    {
        public Task<UbicacionTiendaLectura[]> ConsultarAsync(long idNegocio, DateOnly fecha, CancellationToken ct) =>
            Task.FromException<UbicacionTiendaLectura[]>(NoDisponible());

        public Task<CatalogoTiendaLectura> ConsultarAsync(long idNegocio, ProductoTiendaFiltro filtro, CancellationToken ct) =>
            Task.FromException<CatalogoTiendaLectura>(NoDisponible());

        public Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct) =>
            Task.FromException<TiendaOpciones>(NoDisponible());

        public Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct) =>
            Task.FromException<Page<TiendaLectura>>(NoDisponible());

        public Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct) =>
            Task.FromException<TiendaLectura[]>(NoDisponible());

        public Task<PerfilTienda?> ObtenerAsync(long idNegocio, CancellationToken ct) =>
            Task.FromException<PerfilTienda?>(NoDisponible());

        public Task GuardarAsync(long idNegocio, PerfilTiendaDatos datos, AuditoriaPerfilTienda auditoria, CancellationToken ct) =>
            Task.FromException(NoDisponible());

        private static AccessFailure NoDisponible() => new(503, "Las tiendas estarán disponibles próximamente.");
    }
}

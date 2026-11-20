using Microsoft.Extensions.DependencyInjection;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Exploracion;

// Adaptación de lectura de los contratos públicos existentes, sin nuevas tablas/SP/consultas SQL.
// Transitoria para demo y copia local; sustituir por el contrato global paginado del DBA al escalar.
public sealed record ExploracionLimites(int Tiendas = 100, int Productos = 2000);
public sealed class ExploracionDesdeTiendas(ITiendaService tiendas, CatalogoTiendaServicio catalogos,
    ExploracionLimites? limites = null) : IRepositorioExploracionPublica
{
    private readonly ExploracionLimites max = limites ?? new();
    public async Task<ExploracionLectura> LeerAsync(CancellationToken ct)
    {
        if (max.Tiendas is < 1 or > 1000 || max.Productos is < 1 or > 10000)
            throw new InvalidOperationException("Los límites de exploración no son válidos.");
        using var plazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        plazo.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var visibles = new List<TiendaResumen>(); var productos = new List<ProductoExploracion>();
            var primera = await tiendas.ListarAsync(new(Tamano: 100), plazo.Token);
            var total = primera.Total;
            if (total > max.Tiendas) throw NoDisponible();
            for (var pagina = 1; ; pagina++)
            {
                var filas = pagina == 1 ? primera : await tiendas.ListarAsync(new(Pagina: pagina, Tamano: 100), plazo.Token);
                if (filas.Total != total || (filas.Items.Length == 0 && visibles.Count < total)) throw NoDisponible();
                visibles.AddRange(filas.Items);
                if (visibles.Count >= total) break;
            }
            if (visibles.Count != total || visibles.Select(t => t.IdNegocio).Distinct().Count() != total) throw NoDisponible();
            foreach (var tienda in visibles)
            {
                var primeraPagina = await catalogos.ConsultarAsync(tienda.Slug, new(Tamano: 100), plazo.Token);
                var cantidad = primeraPagina.Productos.Total;
                if (productos.Count + cantidad > max.Productos) throw NoDisponible();
                var leidos = 0;
                for (var pagina = 1; ; pagina++)
                {
                    var consulta = pagina == 1 ? primeraPagina : await catalogos.ConsultarAsync(tienda.Slug, new(Pagina: pagina, Tamano: 100), plazo.Token);
                    if (consulta.Tienda.IdNegocio != tienda.IdNegocio || consulta.Productos.Total != cantidad ||
                        (consulta.Productos.Items.Length == 0 && leidos < cantidad)) throw NoDisponible();
                    // Resumen y detalle deben referirse a la misma identidad y mercado.
                    if (consulta.Tienda.Slug != tienda.Slug || consulta.Tienda.IdMercado != tienda.IdMercado ||
                        consulta.Tienda.NombreComercial != tienda.NombreComercial || consulta.Tienda.Mercado != tienda.Mercado) throw NoDisponible();
                    productos.AddRange(consulta.Productos.Items.Select(p => new ProductoExploracion(tienda.IdNegocio, p)));
                    leidos += consulta.Productos.Items.Length;
                    if (leidos >= cantidad) break;
                }
                if (leidos != cantidad) throw NoDisponible();
            }
            return new(visibles.ToArray(), productos.ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw NoDisponible(); }
        catch (AccessFailure error) when (error.Status == 404) { throw NoDisponible(); } // Una tienda pudo cambiar durante la lectura.
    }
    private static AccessFailure NoDisponible() => new(503, "La búsqueda pública no está disponible en este momento.");
}
public static class ExploracionRegistro
{
    public static IServiceCollection AddExploracionPublica(this IServiceCollection servicios)
    {
        servicios.AddScoped<IRepositorioExploracionPublica, ExploracionDesdeTiendas>();
        servicios.AddScoped<ExploracionPublicaServicio>();
        return servicios;
    }
}

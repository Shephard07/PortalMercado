using Microsoft.EntityFrameworkCore;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Persistence;

public sealed class PortalMercadoDbContext : DbContext
{
    public PortalMercadoDbContext(
        DbContextOptions<PortalMercadoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Mercado> Mercados => Set<Mercado>();
    public DbSet<Sector> Sectores => Set<Sector>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<Negocio> Negocios => Set<Negocio>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PortalMercadoDbContext).Assembly);
    }
}
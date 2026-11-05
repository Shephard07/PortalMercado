using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Persistence.Configurations;

public sealed class SecurityConfigurations : IEntityTypeConfiguration<Rol>,
    IEntityTypeConfiguration<Permiso>, IEntityTypeConfiguration<UsuarioRol>,
    IEntityTypeConfiguration<RolPermiso>, IEntityTypeConfiguration<UsuarioNegocio>,
    IEntityTypeConfiguration<UsuarioMercado>, IEntityTypeConfiguration<UsuarioSeguridad>,
    IEntityTypeConfiguration<UsuarioSesion>, IEntityTypeConfiguration<AuditoriaAcceso>,
    IEntityTypeConfiguration<SeguridadVersion>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("Rol", "dbo"); b.HasKey(x => x.IdRol);
        b.Property(x => x.Codigo).HasColumnType("varchar(50)");
        b.Property(x => x.Nombre).HasColumnType("nvarchar(100)");
        b.HasIndex(x => x.Codigo).IsUnique();
    }
    public void Configure(EntityTypeBuilder<Permiso> b)
    {
        b.ToTable("Permiso", "dbo"); b.HasKey(x => x.IdPermiso);
        b.Property(x => x.Codigo).HasColumnType("varchar(90)");
        b.Property(x => x.Descripcion).HasColumnType("nvarchar(250)");
        b.HasIndex(x => x.Codigo).IsUnique();
    }
    public void Configure(EntityTypeBuilder<UsuarioRol> b)
    {
        b.ToTable("UsuarioRol", "dbo"); b.HasKey(x => new { x.IdUsuario, x.IdRol });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Rol>().WithMany().HasForeignKey(x => x.IdRol).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<RolPermiso> b)
    {
        b.ToTable("RolPermiso", "dbo"); b.HasKey(x => new { x.IdRol, x.IdPermiso });
        b.HasOne<Rol>().WithMany().HasForeignKey(x => x.IdRol).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Permiso>().WithMany().HasForeignKey(x => x.IdPermiso).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioNegocio> b)
    {
        b.ToTable("UsuarioNegocio", "dbo"); b.HasKey(x => new { x.IdUsuario, x.IdNegocio });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Negocio>().WithMany().HasForeignKey(x => x.IdNegocio).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioMercado> b)
    {
        b.ToTable("UsuarioMercado", "dbo"); b.HasKey(x => new { x.IdUsuario, x.IdMercado });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Mercado>().WithMany().HasForeignKey(x => x.IdMercado).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioSeguridad> b)
    {
        b.ToTable("UsuarioSeguridad", "dbo"); b.HasKey(x => x.IdUsuario);
        b.Property(x => x.IdUsuario).ValueGeneratedNever();
        b.Property(x => x.BloqueadoHasta).HasColumnType("datetime2(3)");
        b.HasOne<Usuario>().WithOne().HasForeignKey<UsuarioSeguridad>(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioSesion> b)
    {
        b.ToTable("UsuarioSesion", "dbo"); b.HasKey(x => x.IdSesion);
        b.Property(x => x.TokenHash).HasColumnType("char(64)");
        b.Property(x => x.Host).HasColumnType("varchar(8)");
        b.Property(x => x.CreadoEn).HasColumnType("datetime2(3)");
        b.Property(x => x.ExpiraEn).HasColumnType("datetime2(3)");
        b.Property(x => x.RevocadoEn).HasColumnType("datetime2(3)");
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<Negocio>().WithMany().HasForeignKey(x => x.IdNegocioActivo).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<AuditoriaAcceso> b)
    {
        b.ToTable("AuditoriaAcceso", "dbo"); b.HasKey(x => x.IdAuditoria);
        b.Property(x => x.Evento).HasColumnType("varchar(60)");
        b.Property(x => x.CorrelationId).HasColumnType("varchar(64)");
        b.Property(x => x.FechaUtc).HasColumnType("datetime2(3)");
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdActor).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<SeguridadVersion> b)
    {
        b.ToTable("SeguridadVersion", "dbo"); b.HasKey(x => x.Codigo);
        b.Property(x => x.Codigo).HasColumnType("varchar(80)");
        b.Property(x => x.AplicadoEn).HasColumnType("datetime2(3)");
    }
}

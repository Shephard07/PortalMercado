using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalMercado.Domain.Entities;
namespace PortalMercado.Infrastructure.Persistence.Configurations;

public sealed class MfaConfigurations : IEntityTypeConfiguration<UsuarioMfa>,
    IEntityTypeConfiguration<UsuarioMfaReto>, IEntityTypeConfiguration<UsuarioMfaRecuperacion>
{
    public void Configure(EntityTypeBuilder<UsuarioMfa> b)
    {
        b.ToTable("UsuarioMfa", "dbo"); b.HasKey(x => x.IdUsuario);
        b.Property(x => x.IdUsuario).ValueGeneratedNever();
        b.Property(x => x.SecretProtegido).HasColumnType("nvarchar(2048)");
        b.Property(x => x.ActivadoEn).HasColumnType("datetime2(3)");
        b.HasOne<Usuario>().WithOne().HasForeignKey<UsuarioMfa>(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioMfaReto> b)
    {
        b.ToTable("UsuarioMfaReto", "dbo"); b.HasKey(x => x.IdReto);
        b.Property(x => x.TokenHash).HasColumnType("char(64)");
        b.Property(x => x.Host).HasColumnType("varchar(8)");
        b.Property(x => x.ExpiraEn).HasColumnType("datetime2(3)");
        b.Property(x => x.ConsumidoEn).HasColumnType("datetime2(3)");
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<UsuarioMfaRecuperacion> b)
    {
        b.ToTable("UsuarioMfaRecuperacion", "dbo"); b.HasKey(x => new {x.IdUsuario, x.CodigoHash});
        b.Property(x => x.CodigoHash).HasColumnType("char(64)");
        b.Property(x => x.UsadoEn).HasColumnType("datetime2(3)");
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
}

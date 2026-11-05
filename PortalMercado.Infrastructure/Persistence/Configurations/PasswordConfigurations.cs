using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalMercado.Domain.Entities;
namespace PortalMercado.Infrastructure.Persistence.Configurations;
public sealed class PasswordConfigurations : IEntityTypeConfiguration<UsuarioTokenPassword>, IEntityTypeConfiguration<RecuperacionLimite>
{
    public void Configure(EntityTypeBuilder<UsuarioTokenPassword> b)
    {
        b.ToTable("UsuarioTokenPassword", "dbo"); b.HasKey(x => x.IdToken);
        b.Property(x => x.TokenHash).HasColumnType("char(64)"); b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.CreadoEn).HasColumnType("datetime2(3)"); b.Property(x => x.ExpiraEn).HasColumnType("datetime2(3)");
        b.Property(x => x.ConsumidoEn).HasColumnType("datetime2(3)"); b.Property(x => x.RevocadoEn).HasColumnType("datetime2(3)");
        b.HasIndex(x => new { x.IdUsuario, x.ExpiraEn });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.IdUsuario).OnDelete(DeleteBehavior.NoAction);
    }
    public void Configure(EntityTypeBuilder<RecuperacionLimite> b)
    {
        b.ToTable("RecuperacionLimite", "dbo"); b.HasKey(x => x.CorreoHash);
        b.Property(x => x.CorreoHash).HasColumnType("char(64)"); b.Property(x => x.VentanaUtc).HasColumnType("datetime2(3)");
    }
}

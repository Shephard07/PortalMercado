using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Persistence.Configurations;

public sealed class MercadoConfiguration : IEntityTypeConfiguration<Mercado>
{
    public void Configure(EntityTypeBuilder<Mercado> builder)
    {
        builder.ToTable("Mercado", "dbo");

        builder.HasKey(x => x.IdMercado);
        builder.Property(x => x.IdMercado).UseIdentityColumn();

        builder.Property(x => x.Nombre)
            .HasColumnType("nvarchar(180)").IsRequired();

        builder.Property(x => x.RUC).HasColumnType("varchar(11)");
        builder.Property(x => x.Direccion).HasColumnType("nvarchar(250)");
        builder.Property(x => x.Telefono).HasColumnType("varchar(30)");
        builder.Property(x => x.Correo).HasColumnType("varchar(254)");
        builder.Property(x => x.LogoUrl).HasColumnType("nvarchar(500)");

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.Property(x => x.CreadoEn)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("sysutcdatetime()");
    }
}

public sealed class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("Sector", "dbo");

        builder.HasKey(x => x.IdSector);
        builder.Property(x => x.IdSector).UseIdentityColumn();

        builder.Property(x => x.Codigo)
            .HasColumnType("varchar(30)").IsRequired();

        builder.Property(x => x.Nombre)
            .HasColumnType("nvarchar(120)").IsRequired();

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.IdMercado, x.Codigo })
            .IsUnique()
            .HasDatabaseName("UX_Sector_MercadoCodigo");

        builder.HasOne<Mercado>()
            .WithMany()
            .HasForeignKey(x => x.IdMercado)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Sector_IdMercado");
    }
}

public sealed class PuestoConfiguration : IEntityTypeConfiguration<Puesto>
{
    public void Configure(EntityTypeBuilder<Puesto> builder)
    {
        builder.ToTable("Puesto", "dbo", table =>
            table.HasCheckConstraint(
                "CK_Puesto_Estado",
                "[Estado] IN ('INACTIVO','SUSPENDIDO','OCUPADO','DISPONIBLE')"));

        builder.HasKey(x => x.IdPuesto);
        builder.Property(x => x.IdPuesto).UseIdentityColumn();

        builder.Property(x => x.Codigo)
            .HasColumnType("varchar(30)").IsRequired();

        builder.Property(x => x.Ubicacion)
            .HasColumnType("nvarchar(250)");

        builder.Property(x => x.Estado)
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue("DISPONIBLE");

        builder.HasIndex(x => new { x.IdSector, x.Codigo })
            .IsUnique()
            .HasDatabaseName("UX_Puesto_SectorCodigo");

        builder.HasOne<Sector>()
            .WithMany()
            .HasForeignKey(x => x.IdSector)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Puesto_IdSector");
    }
}

public sealed class NegocioConfiguration : IEntityTypeConfiguration<Negocio>
{
    public void Configure(EntityTypeBuilder<Negocio> builder)
    {
        builder.ToTable("Negocio", "dbo", table =>
            table.HasCheckConstraint(
                "CK_Negocio_Estado",
                "[Estado] IN ('BAJA','SUSPENDIDO','ACTIVO','PENDIENTE')"));

        builder.HasKey(x => x.IdNegocio);
        builder.Property(x => x.IdNegocio).UseIdentityColumn();

        builder.Property(x => x.NombreComercial)
            .HasColumnType("nvarchar(180)").IsRequired();

        builder.Property(x => x.Slug)
            .HasColumnType("varchar(160)").IsRequired();

        builder.Property(x => x.RUC).HasColumnType("varchar(11)");
        builder.Property(x => x.RazonSocial).HasColumnType("nvarchar(220)");
        builder.Property(x => x.DireccionFiscal).HasColumnType("nvarchar(250)");
        builder.Property(x => x.Descripcion).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LogoUrl).HasColumnType("nvarchar(500)");
        builder.Property(x => x.Telefono).HasColumnType("varchar(30)");
        builder.Property(x => x.Correo).HasColumnType("varchar(254)");
        builder.Property(x => x.Horario).HasColumnType("nvarchar(500)");

        builder.Property(x => x.Estado)
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue("PENDIENTE");

        builder.Property(x => x.CreadoEn)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("sysutcdatetime()");

        builder.HasIndex(x => new { x.IdMercado, x.Slug })
            .IsUnique()
            .HasDatabaseName("UX_Negocio_MercadoSlug");

        builder.HasOne<Mercado>()
            .WithMany()
            .HasForeignKey(x => x.IdMercado)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Negocio_IdMercado");

        // Comerciante queda fuera del modelo piloto.
        // Se conserva su identificador; la FK existente la controla SQL Server.
        builder.Property(x => x.IdComerciante)
            .HasColumnType("bigint")
            .IsRequired();
    }
}

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario", "dbo");

        builder.HasKey(x => x.IdUsuario);
        builder.Property(x => x.IdUsuario).UseIdentityColumn();

        builder.Property(x => x.Correo)
            .HasColumnType("varchar(254)").IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnType("nvarchar(500)").IsRequired();

        builder.Property(x => x.Nombre)
            .HasColumnType("nvarchar(150)").IsRequired();

        builder.Property(x => x.Telefono)
            .HasColumnType("varchar(30)");

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.Property(x => x.EmailConfirmado)
            .HasDefaultValue(false);

        builder.Property(x => x.DosFactoresHabilitado)
            .HasDefaultValue(false);

        builder.Property(x => x.CreadoEn)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("sysutcdatetime()");

        builder.Property(x => x.UltimoAcceso)
            .HasColumnType("datetime2(3)");

        builder.Property<string>("CorreoNormalizado")
            .HasColumnType("varchar(254)")
            .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Correo])) COLLATE Latin1_General_100_CI_AS)", stored: true);

        builder.HasIndex(x => x.Correo)
            .IsUnique()
            .HasDatabaseName("UX_Usuario_Correo");
    }
}
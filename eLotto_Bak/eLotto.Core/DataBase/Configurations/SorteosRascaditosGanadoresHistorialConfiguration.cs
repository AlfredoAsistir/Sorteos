using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations;

public sealed class SorteosRascaditosGanadoresHistorialConfiguration
    : IEntityTypeConfiguration<SorteosRascaditosGanadoresHistorial>
{
    public void Configure(EntityTypeBuilder<SorteosRascaditosGanadoresHistorial> builder)
    {
        builder.ToTable("SorteosRascaditosGanadoresHistorial");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Folio).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(x => x.ImportePremio).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MatrizResultado).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(x => x.LineaGanadora).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(x => x.SorteoNombre).IsRequired();
        builder.Property(x => x.SorteoImagen).HasMaxLength(2048);
        builder.Property(x => x.FechaArchivado)
            .IsRequired();
        builder.HasIndex(x => new { x.UsuarioId, x.FechaRevelado });
        builder.HasIndex(x => x.SorteoId);
        builder.HasIndex(x => new { x.UsuarioId, x.SorteoId, x.Folio }).IsUnique();
        builder.HasIndex(x => x.WalletTransactionPremioId).IsUnique();
        builder.HasOne(x => x.Sorteo).WithMany().HasForeignKey(x => x.SorteoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WalletTransactionOrigen).WithMany()
            .HasForeignKey(x => x.WalletTransactionOrigenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WalletTransactionPremio).WithMany()
            .HasForeignKey(x => x.WalletTransactionPremioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

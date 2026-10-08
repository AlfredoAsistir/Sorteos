using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations;

public sealed class BoletosConfirmadosHistorialConfiguration
    : IEntityTypeConfiguration<BoletosConfirmadosHistorial>
{
    public void Configure(EntityTypeBuilder<BoletosConfirmadosHistorial> builder)
    {
        builder.ToTable("BoletosConfirmadosHistorial", table =>
        {
            table.HasCheckConstraint("CK_BoletosConfirmadosHistorial_Cantidad", "[CantidadBoletos] > 0");
            table.HasCheckConstraint("CK_BoletosConfirmadosHistorial_Importes", "[PrecioUnitario] >= 0 AND [ImporteTotal] >= 0");
            table.HasCheckConstraint("CK_BoletosConfirmadosHistorial_NumerosJson", "ISJSON([NumerosJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FolioCompra).HasMaxLength(40).IsRequired();
        builder.Property(x => x.NumerosJson).IsRequired();
        builder.Property(x => x.PrecioUnitario).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ImporteTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.WhatsAppConfirm).HasMaxLength(20);
        builder.Property(x => x.CuentaAsignada).HasMaxLength(250);
        builder.Property(x => x.SorteoNombre).IsRequired();
        builder.Property(x => x.FechaArchivado)
            .IsRequired();
        builder.HasIndex(x => new { x.SorteoId, x.UsuarioId, x.FolioCompra }).IsUnique();
        builder.HasIndex(x => new { x.UsuarioId, x.FechaCompra });
        builder.HasOne(x => x.Sorteo).WithMany().HasForeignKey(x => x.SorteoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class BoletosConfirmadosConfiguration : IEntityTypeConfiguration<BoletosConfirmados>
    {
        public void Configure(EntityTypeBuilder<BoletosConfirmados> builder)
        {
            builder.ToTable("BoletosConfirmados");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Numero).HasMaxLength(32).IsRequired();
            builder.Property(x => x.FolioCompra).HasMaxLength(40).IsRequired();
            builder.Property(x => x.WhatsAppConfirm).HasMaxLength(20);
            builder.Property(x => x.CuentaAsignada).HasMaxLength(250);
            builder.HasIndex(x => new { x.SorteosId, x.Numero }).IsUnique();
            builder.HasIndex(x => new { x.SorteosId, x.UsuarioId, x.FolioCompra });
            builder.HasOne<Sorteos>().WithMany().HasForeignKey(x => x.SorteosId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Users>().WithMany().HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class SorteosBoletosConfiguration : IEntityTypeConfiguration<SorteosBoletos>
    {
        public void Configure(EntityTypeBuilder<SorteosBoletos> builder)
        {
            builder.ToTable("SorteosBoletos");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Numero).HasMaxLength(32).IsRequired();
            builder.Property(x => x.FolioCompra).HasMaxLength(40).IsRequired();
            builder.HasIndex(x => new { x.SorteosId, x.Numero })
                .IsUnique()
                .HasDatabaseName("UX_SorteosBoletos_SorteosId_Numero");
            builder.HasIndex(x => new { x.SorteosId, x.PreApartado, x.Fecha });
            builder.HasIndex(x => new { x.SorteosId, x.UsuarioId, x.PreApartado });            builder.HasOne(x => x.Sorteos)
                .WithMany(x => x.SorteosBoletos)
                .HasForeignKey(x => x.SorteosId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

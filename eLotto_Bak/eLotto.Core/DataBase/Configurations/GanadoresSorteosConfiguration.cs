using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class GanadoresSorteosConfiguration : IEntityTypeConfiguration<GanadoresSorteos>
    {
        public void Configure(EntityTypeBuilder<GanadoresSorteos> builder)
        {
            builder.ToTable("GanadoresSorteos");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.NumeroGanador).HasMaxLength(32);
            builder.Property(x => x.FolioSorteo).IsRequired();
            builder.Property(x => x.Nombre).IsRequired();
            builder.HasIndex(x => x.SorteoId)
                .IsUnique()
                .HasFilter("[SorteoId] IS NOT NULL");
            builder.HasOne(x => x.Sorteo)
                .WithMany()
                .HasForeignKey(x => x.SorteoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

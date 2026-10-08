using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class SorteosRascaditoPremiosConfiguration : IEntityTypeConfiguration<SorteosRascaditoPremios>
    {
        public void Configure(EntityTypeBuilder<SorteosRascaditoPremios> builder)
        {
            builder.ToTable("SorteosRascaditoPremios", table =>
            {
                table.HasCheckConstraint("CK_SorteosRascaditoPremios_Premio", "[Premio] > 0");
                table.HasCheckConstraint("CK_SorteosRascaditoPremios_Cantidad", "[Cantidad] > 0");
                table.HasCheckConstraint(
                    "CK_SorteosRascaditoPremios_Entregados",
                    "[Entregados] >= 0 AND [Entregados] <= [Cantidad]");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Premio).HasPrecision(18, 2).IsRequired();
            builder.Property(x => x.Cantidad).IsRequired();
            builder.Property(x => x.Entregados).HasDefaultValue(0).IsRequired();
            builder.HasIndex(x => x.SorteosId);
            builder.HasOne(x => x.Sorteos)
                .WithMany(x => x.RascaditoPremios)
                .HasForeignKey(x => x.SorteosId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

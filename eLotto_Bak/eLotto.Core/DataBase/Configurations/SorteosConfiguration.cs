using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class SorteosConfiguration : IEntityTypeConfiguration<Sorteos>
    {
        public void Configure(EntityTypeBuilder<Sorteos> builder)
        {
            builder.ToTable("Sorteos", table =>
            {
                table.HasCheckConstraint(
                    "CK_Sorteos_PorcentajeMinimoVenta",
                    "[PorcentajeMinimoVenta] >= 1 AND [PorcentajeMinimoVenta] <= 99");
                table.HasCheckConstraint(
                    "CK_Sorteos_RascaditosConfiguracion",
                    "([RascaditosHabilitados] = 0 AND [GanadoresPorGrupo] = 0 AND [RascaditosPorGrupo] = 0 AND [ImporteDepositoStripePorRascadito] = 0) OR " +
                    "([RascaditosHabilitados] = 1 AND [GanadoresPorGrupo] > 0 AND [RascaditosPorGrupo] > 0 AND [GanadoresPorGrupo] <= [RascaditosPorGrupo] AND [ImporteDepositoStripePorRascadito] > 0)");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Nombre).IsRequired();
            builder.Property(x => x.Imagen1).IsRequired();
            builder.Property(x => x.Imagen2Tema).HasMaxLength(16);
            builder.Property(x => x.Imagen3Tema).HasMaxLength(16);
            builder.Property(x => x.Fecha).IsRequired();
            builder.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(64).HasDefaultValue("CDMX");
            builder.Property(x => x.UrlTransmisionEnVivo).HasMaxLength(2048);
            builder.Property(x => x.PrecioBoleto).HasPrecision(18, 2);
            builder.Property(x => x.PrecioPorMil).HasPrecision(18, 2);
            builder.Property(x => x.PorcentajeMinimoVenta).HasDefaultValue(70);
            builder.Property(x => x.RascaditosHabilitados).HasDefaultValue(false);
            builder.Property(x => x.GanadoresPorGrupo).HasDefaultValue(0);
            builder.Property(x => x.RascaditosPorGrupo).HasDefaultValue(0);
            builder.Property(x => x.ImporteDepositoStripePorRascadito).HasPrecision(18, 2).HasDefaultValue(0m);
        }
    }
}

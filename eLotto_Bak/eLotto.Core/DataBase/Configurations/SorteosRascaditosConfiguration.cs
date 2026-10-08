using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations
{
    public class SorteosRascaditosConfiguration : IEntityTypeConfiguration<SorteosRascaditos>
    {
        public void Configure(EntityTypeBuilder<SorteosRascaditos> builder)
        {
            builder.ToTable("SorteosRascaditos", table =>
            {
                table.HasCheckConstraint(
                    "CK_SorteosRascaditos_Resultado",
                    "([EsGanador] = 1 AND [SorteosRascaditoPremioId] IS NOT NULL AND [ImportePremio] IS NOT NULL AND [ImportePremio] > 0) OR " +
                    "([EsGanador] = 0 AND [SorteosRascaditoPremioId] IS NULL AND [ImportePremio] IS NULL)");
                table.HasCheckConstraint(
                    "CK_SorteosRascaditos_Revelado",
                    "([Revelado] = 0 AND [FechaRevelado] IS NULL) OR ([Revelado] = 1 AND [FechaRevelado] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_SorteosRascaditos_FechaRevelado",
                    "[FechaRevelado] IS NULL OR [FechaRevelado] >= [FechaGeneracion]");
                table.HasCheckConstraint(
                    "CK_SorteosRascaditos_Acreditacion",
                    "([EsGanador] = 0 AND [WalletTransactionPremioId] IS NULL) OR " +
                    "([EsGanador] = 1 AND [Revelado] = 0 AND [WalletTransactionPremioId] IS NULL) OR " +
                    "([EsGanador] = 1 AND [Revelado] = 1 AND [WalletTransactionPremioId] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_SorteosRascaditos_ResultadoVisual",
                    "([MatrizResultado] IS NULL AND [LineaGanadora] IS NULL) OR " +
                    "([MatrizResultado] IS NOT NULL AND (([EsGanador] = 1 AND [LineaGanadora] IS NOT NULL) OR ([EsGanador] = 0 AND [LineaGanadora] IS NULL)))");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Folio)
                .HasMaxLength(32)
                .IsUnicode(false)
                .IsRequired();
            builder.Property(x => x.ImportePremio).HasPrecision(18, 2);
            builder.Property(x => x.MatrizResultado)
                .HasMaxLength(256)
                .IsUnicode(false);
            builder.Property(x => x.LineaGanadora)
                .HasMaxLength(32)
                .IsUnicode(false);
            builder.Property(x => x.Revelado).HasDefaultValue(false).IsRequired();
            builder.Property(x => x.FechaGeneracion)
                .IsRequired();

            builder.HasIndex(x => new { x.UsuarioId, x.SorteosId, x.Folio })
                .IsUnique()
                .HasDatabaseName("UX_SorteosRascaditos_UsuarioId_SorteosId_Folio");
            builder.HasIndex(x => x.WalletTransactionOrigenId)
                .HasDatabaseName("IX_SorteosRascaditos_WalletTransactionOrigenId");
            builder.HasIndex(x => new { x.SorteosId, x.EsGanador })
                .HasDatabaseName("IX_SorteosRascaditos_SorteosId_EsGanador");
            builder.HasIndex(x => new { x.SorteosId, x.UsuarioId, x.EsGanador })
                .HasDatabaseName("IX_SorteosRascaditos_SorteosId_UsuarioId_EsGanador");
            builder.HasIndex(x => x.WalletTransactionPremioId)
                .IsUnique()
                .HasFilter("[WalletTransactionPremioId] IS NOT NULL")
                .HasDatabaseName("UX_SorteosRascaditos_WalletTransactionPremioId");

            builder.HasOne(x => x.Sorteos)
                .WithMany(x => x.Rascaditos)
                .HasForeignKey(x => x.SorteosId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Usuario)
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.WalletTransactionOrigen)
                .WithMany()
                .HasForeignKey(x => x.WalletTransactionOrigenId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.SorteosRascaditoPremio)
                .WithMany()
                .HasForeignKey(x => x.SorteosRascaditoPremioId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.WalletTransactionPremio)
                .WithOne()
                .HasForeignKey<SorteosRascaditos>(x => x.WalletTransactionPremioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

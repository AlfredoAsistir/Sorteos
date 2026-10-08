using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using eLotto.Core.Models;

namespace eLotto.Core.Data.Configurations
{
    public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
    {
        public void Configure(EntityTypeBuilder<WalletTransaction> builder)
        {
            builder.ToTable("WalletTransactions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.StripePaymentIntentId).HasMaxLength(255);
            builder.Property(x => x.StripeEventId).HasMaxLength(255);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.StripeMessage).HasMaxLength(1000);
            builder.Property(x => x.UserMessage).HasMaxLength(500);
            builder.HasIndex(x => x.StripePaymentIntentId).IsUnique().HasFilter("[StripePaymentIntentId] IS NOT NULL");
            builder.HasIndex(x => x.StripeEventId).IsUnique().HasFilter("[StripeEventId] IS NOT NULL");
            builder.HasIndex(x => x.SorteoId);
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Wallet)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Sorteo)
                .WithMany()
                .HasForeignKey(x => x.SorteoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

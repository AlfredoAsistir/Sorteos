using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations;

public sealed class ReferralDepositRewardConfiguration
    : IEntityTypeConfiguration<ReferralDepositReward>
{
    public void Configure(EntityTypeBuilder<ReferralDepositReward> builder)
    {
        builder.ToTable("ReferralDepositRewards", table =>
        {
            table.HasCheckConstraint("CK_ReferralDepositRewards_DifferentUsers", "[ReferredUserId] <> [ReferrerUserId]");
            table.HasCheckConstraint("CK_ReferralDepositRewards_DepositAmount", "[DepositAmount] > 0");
            table.HasCheckConstraint(
                "CK_ReferralDepositRewards_PercentageApplied",
                "[PercentageApplied] > 0 AND [PercentageApplied] <= 100");
            table.HasCheckConstraint("CK_ReferralDepositRewards_RewardAmount", "[RewardAmount] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
        builder.Property(x => x.PercentageApplied).HasPrecision(5, 2);
        builder.Property(x => x.RewardAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.HasIndex(x => x.SourceDepositTransactionId)
            .IsUnique()
            .HasDatabaseName("UX_ReferralDepositRewards_SourceDepositTransactionId");
        builder.HasIndex(x => x.RewardWalletTransactionId)
            .IsUnique()
            .HasDatabaseName("UX_ReferralDepositRewards_RewardWalletTransactionId");
        builder.HasIndex(x => new { x.ReferredUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.ReferrerUserId, x.CreatedAt });
        builder.HasOne(x => x.ReferredUser)
            .WithMany()
            .HasForeignKey(x => x.ReferredUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReferrerUser)
            .WithMany()
            .HasForeignKey(x => x.ReferrerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceDepositTransaction)
            .WithMany()
            .HasForeignKey(x => x.SourceDepositTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RewardWalletTransaction)
            .WithMany()
            .HasForeignKey(x => x.RewardWalletTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

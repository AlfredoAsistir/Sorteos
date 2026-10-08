using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations;

public sealed class ReferralWinnerCashRewardConfiguration
    : IEntityTypeConfiguration<ReferralWinnerCashReward>
{
    public void Configure(EntityTypeBuilder<ReferralWinnerCashReward> builder)
    {
        builder.ToTable("ReferralWinnerCashRewards", table =>
        {
            table.HasCheckConstraint("CK_ReferralWinnerCashRewards_RequiredTickets", "[RequiredTickets] >= 0");
            table.HasCheckConstraint("CK_ReferralWinnerCashRewards_ActualTickets", "[ActualTickets] >= 0");
            table.HasCheckConstraint("CK_ReferralWinnerCashRewards_RewardAmount", "[RewardAmount] > 0");
            table.HasCheckConstraint("CK_ReferralWinnerCashRewards_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_ReferralWinnerCashRewards_PaymentState",
                "([Status] = 1 AND [PaidAt] IS NULL) OR ([Status] = 2 AND [PaidAt] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RewardAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.HasIndex(x => x.WinnerRecordId)
            .IsUnique()
            .HasDatabaseName("UX_ReferralWinnerCashRewards_WinnerRecordId");
        builder.HasIndex(x => new { x.ReferrerUserId, x.CreatedAt });
        builder.HasOne(x => x.WinnerRecord)
            .WithOne(x => x.ReferralWinnerCashReward)
            .HasForeignKey<ReferralWinnerCashReward>(x => x.WinnerRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReferrerUser)
            .WithMany()
            .HasForeignKey(x => x.ReferrerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

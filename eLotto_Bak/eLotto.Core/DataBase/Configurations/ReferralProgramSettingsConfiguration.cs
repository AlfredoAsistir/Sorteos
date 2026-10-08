using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eLotto.Core.Data.Configurations;

public sealed class ReferralProgramSettingsConfiguration
    : IEntityTypeConfiguration<ReferralProgramSettings>
{
    public void Configure(EntityTypeBuilder<ReferralProgramSettings> builder)
    {
        builder.ToTable("ReferralProgramSettings", table =>
        {
            table.HasCheckConstraint("CK_ReferralProgramSettings_Singleton", "[Id] = 1");
            table.HasCheckConstraint(
                "CK_ReferralProgramSettings_DepositRewardPercentage",
                "[DepositRewardPercentage] >= 0 AND [DepositRewardPercentage] <= 100");
            table.HasCheckConstraint(
                "CK_ReferralProgramSettings_MaxRewardedDeposits",
                "[MaxRewardedDeposits] >= 0");
            table.HasCheckConstraint(
                "CK_ReferralProgramSettings_WinnerCashRewardAmount",
                "[WinnerCashRewardAmount] >= 0");
            table.HasCheckConstraint(
                "CK_ReferralProgramSettings_MinimumConfirmedTickets",
                "[MinimumConfirmedTickets] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DepositRewardPercentage).HasPrecision(5, 2);
        builder.Property(x => x.WinnerCashRewardAmount).HasPrecision(18, 2);
        builder.Property(x => x.UpdatedAt)
            .IsRequired();
        builder.Property(x => x.RowVersion)
            .IsRequired()
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}

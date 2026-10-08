using System.ComponentModel.DataAnnotations;

namespace eLotto.Models;

public sealed record ReferralProgramSettingsResponse(
    byte Id,
    bool IsActive,
    decimal DepositRewardPercentage,
    int MaxRewardedDeposits,
    decimal WinnerCashRewardAmount,
    int MinimumConfirmedTickets,
    DateTime UpdatedAt,
    byte[] RowVersion);

public sealed record ReferralProgramBenefitsResponse(
    bool IsActive,
    decimal DepositRewardPercentage,
    int MaxRewardedDeposits,
    decimal WinnerCashRewardAmount,
    int MinimumConfirmedTickets);

public sealed class UpdateReferralProgramSettingsRequest : IValidatableObject
{
    public bool IsActive { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal DepositRewardPercentage { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxRewardedDeposits { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal WinnerCashRewardAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int MinimumConfirmedTickets { get; set; }

    [Required, MinLength(8), MaxLength(8)]
    public byte[] RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DepositRewardPercentage != decimal.Round(DepositRewardPercentage, 2))
            yield return new ValidationResult(
                "El porcentaje admite como máximo dos decimales.",
                [nameof(DepositRewardPercentage)]);

        if (WinnerCashRewardAmount != decimal.Round(WinnerCashRewardAmount, 2))
            yield return new ValidationResult(
                "El premio en efectivo admite como máximo dos decimales.",
                [nameof(WinnerCashRewardAmount)]);
    }
}

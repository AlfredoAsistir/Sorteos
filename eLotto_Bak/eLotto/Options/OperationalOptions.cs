namespace eLotto.Options;

public sealed class LotteryRulesOptions
{
    public const string SectionName = "LotteryRules";
    public int SalesCloseMinutesBeforeDraw { get; set; } = 90;
    public int ReservationMinutes { get; set; } = 7;
    public int RescheduleDays { get; set; } = 7;
}

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public int TokenLifetimeMinutes { get; set; } = 60;
    public int UserRoleValidityYears { get; set; } = 10;
}

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";
    public int ResendCooldownSeconds { get; set; } = 15;
    public int HttpTimeoutSeconds { get; set; } = 15;
}

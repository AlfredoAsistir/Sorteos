namespace eLotto.Options
{
    public class DepositOptions
    {
        public const string SectionName = "Deposits";
        public decimal MinimumAmount { get; set; } = 100m;
        public decimal MaximumAmount { get; set; } = 2000m;
        public decimal SuggestedAmount { get; set; } = 300m;
        public string Currency { get; set; } = "mxn";
        public int PendingExpirationMinutes { get; set; } = 30;
        public int AsynchronousPendingExpirationDays { get; set; } = 7;
        public int CleanupIntervalMinutes { get; set; } = 5;
    }
}

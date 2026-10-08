using Microsoft.Extensions.Options;
using eLotto.Options;

namespace eLotto.Services
{
    public class ExpiredDepositCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DepositOptions _options;
        private readonly ILogger<ExpiredDepositCleanupService> _logger;

        public ExpiredDepositCleanupService(
            IServiceScopeFactory scopeFactory,
            IOptions<DepositOptions> options,
            ILogger<ExpiredDepositCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CleanupIntervalMinutes));
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var walletService = scope.ServiceProvider.GetRequiredService<IWalletPaymentService>();
                    await walletService.CancelExpiredDepositsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while cancelling expired Stripe deposits.");
                }
            }
        }
    }
}

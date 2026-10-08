using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using eLotto.Models;
using eLotto.Options;
using Stripe;

namespace eLotto.Services
{
    public interface IWalletPaymentService
    {
        Task<PaymentIntentResponse> CreateDepositAsync(string userName, int sorteoId, decimal amount, string paymentMethod,
            string name = null, string email = null);
        Task ProcessStripeEventAsync(Event stripeEvent, CancellationToken cancellationToken = default);
        Task<WalletResponse> GetWalletAsync(string userName);
        Task<PagedWalletTransactionsResponse> GetTransactionsAsync(string userName, int page, int pageSize);
        Task CancelExpiredDepositsAsync(CancellationToken cancellationToken);
        Task<DepositStatusResponse> SyncDepositStatusAsync(string userName, int transactionId);
        Task<string> ResolveInstructionUrlAsync(string code);
    }

    public class WalletPaymentService : IWalletPaymentService
    {
        private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private readonly eLottoContext _context;
        private readonly StripeOptions _stripeOptions;
        private readonly DepositOptions _depositOptions;
        private readonly ILogger<WalletPaymentService> _logger;
        private readonly IWhatsAppService _whatsAppService;
        private readonly IScratchcardAssignmentService _scratchcardAssignmentService;
        private readonly IUserLotteryRepository _lotteryRepository;
        private readonly ISorteoTimeService _sorteoTimeService;
        private readonly string _frontendBaseUrl;
        private readonly string _applicationName;
        private readonly int _salesCloseMinutes;

        public WalletPaymentService(eLottoContext context, IOptions<StripeOptions> stripeOptions,
            IOptions<DepositOptions> depositOptions, ILogger<WalletPaymentService> logger,
            IWhatsAppService whatsAppService,
            IScratchcardAssignmentService scratchcardAssignmentService,
            IUserLotteryRepository lotteryRepository,
            IConfiguration configuration,
            ISorteoTimeService sorteoTimeService)
        {
            _context = context;
            _stripeOptions = stripeOptions.Value;
            _depositOptions = depositOptions.Value;
            _logger = logger;
            _whatsAppService = whatsAppService;
            _scratchcardAssignmentService = scratchcardAssignmentService;
            _lotteryRepository = lotteryRepository;
            _sorteoTimeService = sorteoTimeService;
            _applicationName = configuration["Branding:ApplicationName"]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_applicationName))
                throw new InvalidOperationException("Branding:ApplicationName is not configured.");
            _salesCloseMinutes = int.TryParse(configuration["LotteryRules:SalesCloseMinutesBeforeDraw"], out var closeMinutes) && closeMinutes > 0
                ? closeMinutes : SorteoDisponibilidadPolicy.MinutosBloqueoPredeterminados;
            _frontendBaseUrl = configuration["AppUrls:FrontendBaseUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("AppUrls:FrontendBaseUrl is not configured.");
        }

        public async Task<PaymentIntentResponse> CreateDepositAsync(string userName, int sorteoId, decimal amount, string paymentMethod,
            string name = null, string email = null)
        {
            var sorteo = await RequireAvailableLotteryAsync(sorteoId);
            ValidateAmount(amount);
            paymentMethod = NormalizePaymentMethod(paymentMethod);
            EnsureTestSecretKey();
            var user = await GetUserAsync(userName);
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (paymentMethod is "oxxo" or "bank_transfer")
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Nombre y correo electrónico son obligatorios para este método de depósito.");

                user.Name = name.Trim();
                user.Email = email.Trim().ToLowerInvariant();
                await _context.SaveChangesAsync();
            }

            WalletTransaction transaction;
            await using (var creation = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                var lockedSorteo = await _context.Sorteos
                    .FromSqlInterpolated($"SELECT * FROM [Sorteos] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sorteo.Id}")
                    .AsNoTracking()
                    .SingleAsync();
                var serverNow = ApplicationClock.NowOffset;
                if (!SorteoDisponibilidadPolicy.VentaDisponible(
                    lockedSorteo, serverNow, _sorteoTimeService, _salesCloseMinutes))
                    throw new InvalidOperationException("Los depósitos están cerrados para este sorteo.");

                if (wallet == null)
                {
                    var createdAt = serverNow.DateTime;
                    wallet = new UserWallet { UserId = user.Id, CreatedAt = createdAt, UpdatedAt = createdAt };
                    _context.UserWallets.Add(wallet);
                    await _context.SaveChangesAsync();
                }

                transaction = new WalletTransaction
                {
                    UserId = user.Id, WalletId = wallet.Id, Type = WalletTransactionType.Deposit,
                    SorteoId = lockedSorteo.Id,
                    Amount = amount, Status = WalletTransactionStatus.Pending,
                    Description = GetDepositDescription(paymentMethod),
                    CreatedAt = _sorteoTimeService.ConvertToSorteoTime(serverNow, lockedSorteo).DateTime
                };
                _context.WalletTransactions.Add(transaction);
                await _context.SaveChangesAsync();
                await creation.CommitAsync();
            }

            try
            {
                var service = new PaymentIntentService(new StripeClient(_stripeOptions.SecretKey));
                var options = new PaymentIntentCreateOptions
                {
                    Amount = ToMinorUnits(amount),
                    Currency = _depositOptions.Currency.ToLowerInvariant(),
                    Metadata = new Dictionary<string, string>
                    {
                        ["UserId"] = user.Id.ToString(),
                        ["WalletTransactionId"] = transaction.Id.ToString(),
                        ["Application"] = _applicationName,
                        ["PaymentMethod"] = paymentMethod,
                        ["SorteoId"] = sorteo.Id.ToString()
                    }
                };
                if (paymentMethod == "card")
                {
                    // Conserva el flujo de tarjeta que ya estaba habilitado en la cuenta Stripe.
                    options.AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true };
                }
                else
                {
                    options.PaymentMethodTypes = new List<string> { GetStripePaymentMethod(paymentMethod) };
                }

                if (paymentMethod == "bank_transfer")
                {
                    options.Customer = await GetOrCreateStripeCustomerAsync(user);
                    options.PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        CustomerBalance = new PaymentIntentPaymentMethodOptionsCustomerBalanceOptions
                        {
                            FundingType = "bank_transfer",
                            BankTransfer = new PaymentIntentPaymentMethodOptionsCustomerBalanceBankTransferOptions
                            {
                                Type = "mx_bank_transfer"
                            }
                        }
                    };
                }
                var paymentIntent = await service.CreateAsync(
                    options,
                    new RequestOptions { IdempotencyKey = $"elotto-deposit-{transaction.Id}-{transaction.CreatedAt.Ticks}" });

                transaction.StripePaymentIntentId = paymentIntent.Id;
                await _context.SaveChangesAsync();

                string instructionUrl = null;
                var whatsAppSent = false;
                if (paymentMethod is "oxxo" or "bank_transfer")
                {
                    paymentIntent = await service.ConfirmAsync(
                        paymentIntent.Id,
                        new PaymentIntentConfirmOptions
                        {
                            PaymentMethodData = new PaymentIntentPaymentMethodDataOptions
                            {
                                Type = GetStripePaymentMethod(paymentMethod),
                                BillingDetails = new PaymentIntentPaymentMethodDataBillingDetailsOptions
                                {
                                    Name = user.Name,
                                    Email = user.Email
                                }
                            }
                        },
                        new RequestOptions
                        {
                            IdempotencyKey = $"elotto-deposit-confirm-{transaction.Id}-{transaction.CreatedAt.Ticks}"
                        });

                    var stripeInstructionUrl = paymentMethod == "oxxo"
                        ? paymentIntent.NextAction?.OxxoDisplayDetails?.HostedVoucherUrl
                        : paymentIntent.NextAction?.DisplayBankTransferInstructions?.HostedInstructionsUrl;
                    if (string.IsNullOrWhiteSpace(stripeInstructionUrl))
                        throw new InvalidOperationException("Stripe confirmó el depósito, pero no devolvió las instrucciones.");

                    var methodName = paymentMethod == "oxxo" ? "vale OXXO" : "transferencia SPEI";
                    instructionUrl = $"{_frontendBaseUrl}/p/{CreateInstructionCode(transaction)}";
                    var message = $"💳 Instrucciones de depósito {_applicationName}\n\nHola {user.Name}, aquí tienes tu {methodName}:\n\n{instructionUrl}\n\nConserva este enlace hasta completar el pago. Tu saldo se acreditará automáticamente cuando Stripe lo confirme.";
                    try
                    {
                        whatsAppSent = await _whatsAppService.SendTextAsync(user.WhatsApp, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not send deposit instructions for transaction {TransactionId} by WhatsApp.", transaction.Id);
                    }
                }

                _logger.LogInformation("PaymentIntent {PaymentIntentId} created for transaction {TransactionId}.", paymentIntent.Id, transaction.Id);
                return new PaymentIntentResponse(
                    paymentIntent.ClientSecret,
                    transaction.Id,
                    instructionUrl,
                    whatsAppSent);
            }
            catch (Exception ex)
            {
                transaction.Status = WalletTransactionStatus.Failed;
                transaction.StripeMessage = ex is StripeException stripeException
                    ? stripeException.StripeError?.Message ?? stripeException.Message
                    : ex.Message;
                transaction.UserMessage = ex is StripeException stripeError
                    ? GetUserMessage(stripeError.StripeError?.DeclineCode, stripeError.StripeError?.Code)
                    : "No fue posible iniciar el pago. Inténtalo nuevamente.";
                transaction.CompletedAt = _sorteoTimeService.ConvertToSorteoTime(ApplicationClock.NowOffset, sorteo).DateTime;
                _logger.LogError(
                    ex,
                    "Could not create Stripe PaymentIntent for transaction {TransactionId} using {PaymentMethod}.",
                    transaction.Id,
                    paymentMethod);
                await _context.SaveChangesAsync();
                throw;
            }
        }


        public async Task ProcessStripeEventAsync(
            Event stripeEvent,
            CancellationToken cancellationToken = default)
        {
            if (stripeEvent.Data.Object is not PaymentIntent paymentIntent) return;
            _logger.LogInformation("Stripe webhook {StripeEventId} received: {StripeEventType}.", stripeEvent.Id, stripeEvent.Type);
            await using var dbTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var transaction = await _context.WalletTransactions
                .FromSqlInterpolated(
                    $"SELECT * FROM [WalletTransactions] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [StripePaymentIntentId] = {paymentIntent.Id}")
                .SingleOrDefaultAsync(cancellationToken);

            if (transaction == null)
            {
                _logger.LogWarning("No transaction exists for PaymentIntent {PaymentIntentId}.", paymentIntent.Id);
                await dbTransaction.CommitAsync(cancellationToken);
                return;
            }
            if (transaction.StripeEventId == stripeEvent.Id)
            {
                _logger.LogInformation("Stripe event {StripeEventId} was already processed.", stripeEvent.Id);
                await dbTransaction.CommitAsync(cancellationToken);
                return;
            }

            var terminalAt = stripeEvent.Type is "payment_intent.payment_failed" or "payment_intent.canceled"
                ? await GetSorteoNowAsync(transaction.SorteoId, cancellationToken)
                : default;


            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    await CompleteDepositAsync(
                        stripeEvent,
                        paymentIntent,
                        transaction,
                        cancellationToken);
                    break;
                case "payment_intent.payment_failed":
                    SetTerminalStatus(
                        transaction,
                        stripeEvent.Id,
                        WalletTransactionStatus.Failed,
                        paymentIntent.LastPaymentError?.Message,
                        GetUserMessage(
                            paymentIntent.LastPaymentError?.DeclineCode,
                            paymentIntent.LastPaymentError?.Code),
                        terminalAt);
                    _logger.LogWarning("PaymentIntent {PaymentIntentId} failed.", paymentIntent.Id);
                    break;
                case "payment_intent.canceled":
                    SetTerminalStatus(
                        transaction,
                        stripeEvent.Id,
                        WalletTransactionStatus.Cancelled,
                        paymentIntent.CancellationReason ?? "PaymentIntent cancelado por Stripe.",
                        "El pago fue cancelado.",
                        terminalAt);
                    _logger.LogInformation("PaymentIntent {PaymentIntentId} was cancelled.", paymentIntent.Id);
                    break;

                default:
                    await dbTransaction.CommitAsync(cancellationToken);
                    return;
            }
            await SaveChangesWithScratchcardFolioRetryAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        public async Task<WalletResponse> GetWalletAsync(string userName)
        {
            var user = await GetUserAsync(userName);
            var balance = await _context.UserWallets.AsNoTracking().Where(x => x.UserId == user.Id)
                .Select(x => (decimal?)x.Balance).FirstOrDefaultAsync() ?? 0m;
            return new WalletResponse(
                balance,
                _depositOptions.Currency.ToUpperInvariant(),
                user.Name,
                user.Email,
                _depositOptions.MinimumAmount,
                _depositOptions.MaximumAmount,
                _depositOptions.SuggestedAmount,
                _stripeOptions.PublishableKey);
        }

        public async Task<PagedWalletTransactionsResponse> GetTransactionsAsync(string userName, int page, int pageSize)
        {
            var user = await GetUserAsync(userName);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.WalletTransactions.AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Status != WalletTransactionStatus.Pending)
                .OrderByDescending(x => x.Id);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new WalletTransactionResponse(x.Id, x.Type, x.Amount, x.Status,
                    x.Description, x.StripeMessage, x.UserMessage, x.CreatedAt, x.CompletedAt)).ToListAsync();
            return new PagedWalletTransactionsResponse(items, page, pageSize, total);
        }

        public async Task CancelExpiredDepositsAsync(CancellationToken cancellationToken)
        {
            EnsureTestSecretKey();
            var expirationMinutes = Math.Max(1, _depositOptions.PendingExpirationMinutes);
            var now = ApplicationClock.NowOffset;
            var asynchronousExpiration = TimeSpan.FromDays(Math.Max(1, _depositOptions.AsynchronousPendingExpirationDays));
            var pendingDeposits = await _context.WalletTransactions
                .Include(x => x.Sorteo)
                .Where(x => x.Type == WalletTransactionType.Deposit &&
                    x.Status == WalletTransactionStatus.Pending &&
                    x.StripePaymentIntentId != null)
                .ToListAsync(cancellationToken);

            if (pendingDeposits.Count == 0) return;
            var service = new PaymentIntentService(new StripeClient(_stripeOptions.SecretKey));
            foreach (var transaction in pendingDeposits)
            {
                var createdAt = _sorteoTimeService.ResolveRecordedTime(
                    transaction.CreatedAt,
                    transaction.Sorteo ?? throw new InvalidOperationException(
                        $"El depósito {transaction.Id} no está asociado a un sorteo."));
                var age = now - createdAt;
                if (age < TimeSpan.FromMinutes(expirationMinutes)) continue;

                try
                {
                    var paymentIntent = await service.GetAsync(
                        transaction.StripePaymentIntentId,
                        cancellationToken: cancellationToken);
                    if (IsAsynchronousPayment(paymentIntent) && age < asynchronousExpiration)
                        continue;

                    paymentIntent = await service.CancelAsync(
                        transaction.StripePaymentIntentId,
                        cancellationToken: cancellationToken);
                    if (paymentIntent.Status == "canceled")
                    {
                        transaction.Status = WalletTransactionStatus.Cancelled;
                        transaction.StripeMessage = paymentIntent.CancellationReason
                            ?? "PaymentIntent cancelado por expiración.";
                        transaction.UserMessage = "El intento de pago expiró y fue cancelado.";
                        transaction.CompletedAt = await GetSorteoNowAsync(transaction.SorteoId, cancellationToken);
                        _logger.LogInformation(
                            "Expired PaymentIntent {PaymentIntentId} was cancelled for transaction {TransactionId}.",
                            paymentIntent.Id,
                            transaction.Id);
                    }
                }
                catch (StripeException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Expired PaymentIntent {PaymentIntentId} could not be cancelled; Stripe webhook will remain authoritative.",
                        transaction.StripePaymentIntentId);
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<DepositStatusResponse> SyncDepositStatusAsync(string userName, int transactionId)
        {
            EnsureTestSecretKey();
            var user = await GetUserAsync(userName);
            var transaction = await _context.WalletTransactions.FirstOrDefaultAsync(x =>
                x.Id == transactionId &&
                x.UserId == user.Id &&
                x.Type == WalletTransactionType.Deposit);
            if (transaction == null) throw new KeyNotFoundException();
            if (string.IsNullOrWhiteSpace(transaction.StripePaymentIntentId))
                return new DepositStatusResponse(
                    transaction.Id,
                    transaction.Status,
                    transaction.StripeMessage,
                    transaction.UserMessage);

            var service = new PaymentIntentService(new StripeClient(_stripeOptions.SecretKey));
            var paymentIntent = await service.GetAsync(transaction.StripePaymentIntentId);
            if (paymentIntent.Metadata.GetValueOrDefault("Application") != _applicationName ||
                paymentIntent.Metadata.GetValueOrDefault("WalletTransactionId") != transaction.Id.ToString() ||
                paymentIntent.Metadata.GetValueOrDefault("UserId") != user.Id.ToString())
            {
                throw new InvalidOperationException("Stripe PaymentIntent metadata does not match the transaction.");
            }

            if (paymentIntent.Status == "requires_payment_method" && paymentIntent.LastPaymentError != null)
            {
                transaction.Status = WalletTransactionStatus.Failed;
                transaction.StripeMessage = paymentIntent.LastPaymentError.Message;
                transaction.UserMessage = GetUserMessage(
                    paymentIntent.LastPaymentError.DeclineCode,
                    paymentIntent.LastPaymentError.Code);
                transaction.CompletedAt = await GetSorteoNowAsync(transaction.SorteoId, CancellationToken.None);
            }
            else if (paymentIntent.Status == "canceled")
            {
                transaction.Status = WalletTransactionStatus.Cancelled;
                transaction.StripeMessage = paymentIntent.CancellationReason
                    ?? "PaymentIntent cancelado por Stripe.";
                transaction.UserMessage = "El pago fue cancelado.";
                transaction.CompletedAt = await GetSorteoNowAsync(transaction.SorteoId, CancellationToken.None);
            }

            await _context.SaveChangesAsync();
            return new DepositStatusResponse(
                transaction.Id,
                transaction.Status,
                transaction.StripeMessage,
                transaction.UserMessage);
        }

        public async Task<string> ResolveInstructionUrlAsync(string code)
        {
            EnsureTestSecretKey();
            if (!TryDecodeTransactionId(code, out var transactionId)) return null;

            var transaction = await _context.WalletTransactions.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == transactionId &&
                x.Type == WalletTransactionType.Deposit &&
                x.StripePaymentIntentId != null);
            if (transaction == null || !IsValidInstructionCode(transaction, code)) return null;

            var paymentIntent = await new PaymentIntentService(new StripeClient(_stripeOptions.SecretKey))
                .GetAsync(transaction.StripePaymentIntentId);
            if (paymentIntent.Metadata.GetValueOrDefault("Application") != _applicationName ||
                paymentIntent.Metadata.GetValueOrDefault("WalletTransactionId") != transaction.Id.ToString())
                return null;

            var paymentMethod = paymentIntent.Metadata.GetValueOrDefault("PaymentMethod");
            var instructionUrl = paymentMethod switch
            {
                "oxxo" => paymentIntent.NextAction?.OxxoDisplayDetails?.HostedVoucherUrl,
                "bank_transfer" => paymentIntent.NextAction?.DisplayBankTransferInstructions?.HostedInstructionsUrl,
                _ => null
            };
            return IsAllowedStripeUrl(instructionUrl) ? instructionUrl : null;
        }
        private async Task CompleteDepositAsync(
            Event stripeEvent,
            PaymentIntent paymentIntent,
            WalletTransaction transaction,
            CancellationToken cancellationToken)
        {
            if (transaction.Status == WalletTransactionStatus.Completed)
            {
                _logger.LogInformation("PaymentIntent {PaymentIntentId} was already credited.", paymentIntent.Id);
                return;
            }
            if (transaction.Type != WalletTransactionType.Deposit)
            {
                _logger.LogWarning(
                    "PaymentIntent {PaymentIntentId} is not associated with a deposit transaction.",
                    paymentIntent.Id);
                return;
            }
            var expected = ToMinorUnits(transaction.Amount);
            var received = paymentIntent.AmountReceived > 0 ? paymentIntent.AmountReceived : paymentIntent.Amount;
            if (paymentIntent.Metadata.GetValueOrDefault("Application") != _applicationName ||
                paymentIntent.Metadata.GetValueOrDefault("WalletTransactionId") != transaction.Id.ToString() ||
                paymentIntent.Metadata.GetValueOrDefault("UserId") != transaction.UserId.ToString())
            {
                _logger.LogWarning("PaymentIntent {PaymentIntentId} has inconsistent metadata.", paymentIntent.Id);
                return;
            }
            if (received != expected || !string.Equals(paymentIntent.Currency, _depositOptions.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PaymentIntent {PaymentIntentId} amount or currency mismatch.", paymentIntent.Id);
                return;
            }

            var lockedWallet = await _context.UserWallets
                .FromSqlInterpolated(
                    $"SELECT * FROM [UserWallets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [Id] = {transaction.WalletId} AND [UserId] = {transaction.UserId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (lockedWallet == null)
                throw new InvalidOperationException(
                    $"No se encontró la cartera de la transacción {transaction.Id}.");

            var completedMoment = ApplicationClock.NowOffset;
            var completedAt = completedMoment.DateTime;
            var completedAtSorteo = await ConvertToSorteoTimeAsync(
                completedMoment, transaction.SorteoId, cancellationToken);
            await CreateReferralDepositRewardIfEligibleAsync(
                transaction,
                completedAt,
                completedAtSorteo,
                cancellationToken);

            await _scratchcardAssignmentService.AssignForConfirmedStripeDepositAsync(
                transaction,
                cancellationToken);

            var updatedWallets = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [UserWallets] WITH (ROWLOCK) SET [Balance] = [Balance] + {transaction.Amount}, [UpdatedAt] = {completedAt} WHERE [Id] = {transaction.WalletId} AND [UserId] = {transaction.UserId}",
                cancellationToken);
            if (updatedWallets != 1)
                throw new InvalidOperationException(
                    $"No se encontró la cartera de la transacción {transaction.Id}.");

            transaction.Status = WalletTransactionStatus.Completed;
            transaction.StripeMessage = null;
            transaction.UserMessage = null;
            transaction.CompletedAt = completedAtSorteo;
            transaction.StripeEventId = stripeEvent.Id;
            _logger.LogInformation("PaymentIntent {PaymentIntentId} credited transaction {TransactionId}.", paymentIntent.Id, transaction.Id);
        }

        private async Task CreateReferralDepositRewardIfEligibleAsync(
            WalletTransaction sourceDeposit,
            DateTime completedAtServer,
            DateTime completedAtSorteo,
            CancellationToken cancellationToken)
        {
            var referrerUserId = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == sourceDeposit.UserId)
                .Select(user => user.ReferredByUserId)
                .SingleAsync(cancellationToken);
            if (!referrerUserId.HasValue) return;

            var settings = await _context.ReferralProgramSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            if (settings == null)
            {
                _logger.LogCritical(
                    "Referral settings singleton is missing while processing deposit transaction {TransactionId}. The deposit will be credited without a referral reward.",
                    sourceDeposit.Id);
                return;
            }
            if (!IsValidReferralProgramSettings(settings))
            {
                _logger.LogCritical(
                    "Referral settings singleton is invalid while processing deposit transaction {TransactionId}. The deposit will be credited without a referral reward. Percentage={Percentage}, MaxDeposits={MaxDeposits}, WinnerReward={WinnerReward}, MinimumTickets={MinimumTickets}.",
                    sourceDeposit.Id,
                    settings.DepositRewardPercentage,
                    settings.MaxRewardedDeposits,
                    settings.WinnerCashRewardAmount,
                    settings.MinimumConfirmedTickets);
                return;
            }
            if (!settings.IsActive ||
                settings.DepositRewardPercentage == 0m ||
                settings.MaxRewardedDeposits == 0)
                return;

            if (await _context.ReferralDepositRewards
                .AsNoTracking()
                .AnyAsync(
                    reward => reward.SourceDepositTransactionId == sourceDeposit.Id,
                    cancellationToken))
                return;

            var rewardedDeposits = await _context.ReferralDepositRewards
                .AsNoTracking()
                .CountAsync(
                    reward => reward.ReferredUserId == sourceDeposit.UserId,
                    cancellationToken);
            if (rewardedDeposits >= settings.MaxRewardedDeposits) return;

            var rewardAmount = decimal.Round(
                sourceDeposit.Amount * settings.DepositRewardPercentage / 100m,
                2,
                MidpointRounding.AwayFromZero);
            if (rewardAmount <= 0m) return;

            var updatedReferredWallets = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [UserWallets] WITH (ROWLOCK) SET [Balance] = [Balance] + {rewardAmount}, [UpdatedAt] = {completedAtServer} WHERE [Id] = {sourceDeposit.WalletId} AND [UserId] = {sourceDeposit.UserId}",
                cancellationToken);
            if (updatedReferredWallets != 1)
                throw new InvalidOperationException(
                    $"No se pudo acreditar el bono del usuario referido para la transacción {sourceDeposit.Id}.");

            _context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = sourceDeposit.UserId,
                WalletId = sourceDeposit.WalletId,
                SorteoId = sourceDeposit.SorteoId,
                Type = WalletTransactionType.ReferralDepositReward,
                Amount = rewardAmount,
                Status = WalletTransactionStatus.Completed,
                Description = "Bono por depósito como referido",
                CreatedAt = completedAtSorteo,
                CompletedAt = completedAtSorteo
            });

            var referrerWallet = await _context.UserWallets
                .FromSqlInterpolated(
                    $"SELECT * FROM [UserWallets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [UserId] = {referrerUserId.Value}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            UserWallet newReferrerWallet = null;
            if (referrerWallet == null)
            {
                newReferrerWallet = new UserWallet
                {
                    UserId = referrerUserId.Value,
                    Balance = rewardAmount,
                    CreatedAt = completedAtServer,
                    UpdatedAt = completedAtServer
                };
            }
            else
            {
                var updatedWallets = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [UserWallets] WITH (ROWLOCK) SET [Balance] = [Balance] + {rewardAmount}, [UpdatedAt] = {completedAtServer} WHERE [Id] = {referrerWallet.Id} AND [UserId] = {referrerUserId.Value}",
                    cancellationToken);
                if (updatedWallets != 1)
                    throw new InvalidOperationException(
                        $"No se pudo acreditar el bono de referido para la transacción {sourceDeposit.Id}.");
            }

            var rewardTransaction = new WalletTransaction
            {
                UserId = referrerUserId.Value,
                WalletId = referrerWallet?.Id ?? 0,
                Wallet = newReferrerWallet,
                SorteoId = sourceDeposit.SorteoId,
                Type = WalletTransactionType.ReferralDepositReward,
                Amount = rewardAmount,
                Status = WalletTransactionStatus.Completed,
                Description = "Bono por depósito referido",
                CreatedAt = completedAtSorteo,
                CompletedAt = completedAtSorteo
            };
            _context.ReferralDepositRewards.Add(new ReferralDepositReward
            {
                ReferredUserId = sourceDeposit.UserId,
                ReferrerUserId = referrerUserId.Value,
                SourceDepositTransactionId = sourceDeposit.Id,
                RewardWalletTransaction = rewardTransaction,
                DepositAmount = sourceDeposit.Amount,
                PercentageApplied = settings.DepositRewardPercentage,
                RewardAmount = rewardAmount,
                CreatedAt = completedAtSorteo
            });

            _logger.LogInformation(
                "Referral deposit reward {RewardAmount} prepared for referred user {ReferredUserId} and referrer {ReferrerUserId} from deposit transaction {TransactionId}.",
                rewardAmount,
                sourceDeposit.UserId,
                referrerUserId.Value,
                sourceDeposit.Id);
        }

        private static bool IsValidReferralProgramSettings(ReferralProgramSettings settings) =>
            settings.Id == 1 &&
            settings.DepositRewardPercentage is >= 0m and <= 100m &&
            settings.MaxRewardedDeposits >= 0 &&
            settings.WinnerCashRewardAmount >= 0m &&
            settings.MinimumConfirmedTickets >= 0 &&
            settings.RowVersion is { Length: 8 };

        private async Task SaveChangesWithScratchcardFolioRetryAsync(
            CancellationToken cancellationToken)
        {
            const int maximumAttempts = 3;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateException ex) when (
                    attempt < maximumAttempts &&
                    IsScratchcardFolioCollision(ex))
                {
                    foreach (var entry in _context.ChangeTracker
                        .Entries<SorteosRascaditos>()
                        .Where(x => x.State == EntityState.Added))
                    {
                        entry.Entity.Folio = ScratchcardFolioGenerator.Create();
                    }
                }
            }
        }

        private static bool IsScratchcardFolioCollision(DbUpdateException exception)
        {
            var sqlException = exception.InnerException as SqlException;
            return sqlException?.Number is 2601 or 2627 &&
                sqlException.Message.Contains(
                    "UX_SorteosRascaditos_UsuarioId_SorteosId_Folio",
                    StringComparison.Ordinal);
        }

        private static void SetTerminalStatus(
            WalletTransaction transaction,
            string eventId,
            WalletTransactionStatus status,
            string stripeMessage,
            string userMessage,
            DateTime completedAt)
        {
            if (transaction.Status == WalletTransactionStatus.Completed) return;
            transaction.Status = status;
            transaction.StripeEventId = eventId;
            transaction.StripeMessage = stripeMessage;
            transaction.UserMessage = userMessage;
            transaction.CompletedAt = completedAt;
        }

        private Task<DateTime> GetSorteoNowAsync(int? sorteoId, CancellationToken cancellationToken) =>
            ConvertToSorteoTimeAsync(ApplicationClock.NowOffset, sorteoId, cancellationToken);

        private async Task<DateTime> ConvertToSorteoTimeAsync(
            DateTimeOffset instant, int? sorteoId, CancellationToken cancellationToken)
        {
            if (sorteoId == null)
                throw new InvalidOperationException("El depósito no está asociado a un sorteo.");

            var sorteo = await _context.Sorteos.AsNoTracking()
                .Where(x => x.Id == sorteoId.Value)
                .Select(x => new Sorteos { ZonaHoraria = x.ZonaHoraria })
                .SingleAsync(cancellationToken);
            return _sorteoTimeService.ConvertToSorteoTime(instant, sorteo).DateTime;
        }

        private async Task<Users> GetUserAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new UnauthorizedAccessException();
            return await _context.Users.FirstOrDefaultAsync(x => x.User == userName)
                ?? throw new UnauthorizedAccessException();
        }

        private async Task<Sorteos> RequireAvailableLotteryAsync(int sorteoId)
        {
            var ahora = ApplicationClock.NowOffset;
            var sorteo = await _lotteryRepository.GetCurrentAsync(ahora, CancellationToken.None);
            if (sorteo == null)
                throw new InvalidOperationException(
                    "No puedes agregar saldo porque actualmente no hay un sorteo disponible.");
            if (sorteo.Id != sorteoId)
                throw new InvalidOperationException(
                    "El sorteo disponible cambió. Actualiza la página antes de agregar saldo.");
            if (!SorteoDisponibilidadPolicy.VentaDisponible(sorteo, ahora, _sorteoTimeService, _salesCloseMinutes))
                throw new InvalidOperationException(
                    "Los depósitos están temporalmente cerrados porque el sorteo está próximo a iniciar o se encuentra en proceso.");
            return sorteo;
        }

        private async Task<string> GetOrCreateStripeCustomerAsync(Users user)
        {
            if (!string.IsNullOrWhiteSpace(user.StripeCustomerId)) return user.StripeCustomerId;

            var customer = await new CustomerService(new StripeClient(_stripeOptions.SecretKey)).CreateAsync(
                new CustomerCreateOptions
                {
                    Name = user.Name,
                    Email = !string.IsNullOrWhiteSpace(user.Email)
                        ? user.Email
                        : user.User.Contains('@') ? user.User : null,
                    Metadata = new Dictionary<string, string>
                    {
                        ["UserId"] = user.Id.ToString(),
                        ["Application"] = _applicationName
                    }
                },
                new RequestOptions { IdempotencyKey = $"elotto-customer-{user.Id}-{Guid.NewGuid():N}" });

            user.StripeCustomerId = customer.Id;
            await _context.SaveChangesAsync();
            return customer.Id;
        }

        private static string NormalizePaymentMethod(string paymentMethod)
        {
            var normalized = paymentMethod?.Trim().ToLowerInvariant();
            return normalized is "card" or "oxxo" or "bank_transfer"
                ? normalized
                : throw new ArgumentException("El método de pago seleccionado no es válido.", nameof(paymentMethod));
        }

        private static string GetStripePaymentMethod(string paymentMethod) =>
            paymentMethod == "bank_transfer" ? "customer_balance" : paymentMethod;

        private static string GetDepositDescription(string paymentMethod) => paymentMethod switch
        {
            "card" => "Depósito con tarjeta",
            "oxxo" => "Depósito en OXXO",
            "bank_transfer" => "Depósito por transferencia SPEI",
            _ => "Depósito mediante Stripe"
        };

        private static bool IsAsynchronousPayment(PaymentIntent paymentIntent) =>
            paymentIntent.Metadata.GetValueOrDefault("PaymentMethod") is "oxxo" or "bank_transfer";

        private void ValidateAmount(decimal amount)
        {
            if (amount != decimal.Round(amount, 2) || amount < _depositOptions.MinimumAmount || amount > _depositOptions.MaximumAmount)
                throw new ArgumentOutOfRangeException(nameof(amount),
                    $"El monto debe estar entre {_depositOptions.MinimumAmount:F2} y {_depositOptions.MaximumAmount:F2}, con máximo dos decimales.");
            if (!string.Equals(_depositOptions.Currency, "mxn", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solo se permiten depósitos en MXN.");
        }

        private void EnsureTestSecretKey()
        {
            if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey) ||
                !(_stripeOptions.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) ||
                  _stripeOptions.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Stripe SecretKey is not configured correctly.");
        }

        private static long ToMinorUnits(decimal amount) =>
            decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

        private string CreateInstructionCode(WalletTransaction transaction)
        {
            var transactionPart = EncodeBase62(transaction.Id);
            var signature = CreateInstructionSignature(transaction.Id, transaction.StripePaymentIntentId);
            return transactionPart + signature;
        }

        private bool IsValidInstructionCode(WalletTransaction transaction, string code)
        {
            var expected = CreateInstructionCode(transaction);
            return expected.Length == code?.Length && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(code));
        }

        private string CreateInstructionSignature(int transactionId, string paymentIntentId)
        {
            using var keyDerivation = new HMACSHA256(Encoding.UTF8.GetBytes(_stripeOptions.SecretKey));
            var signingKey = keyDerivation.ComputeHash(Encoding.UTF8.GetBytes("eLotto/payment-instruction-links/v1"));
            using var signer = new HMACSHA256(signingKey);
            var signature = signer.ComputeHash(Encoding.UTF8.GetBytes($"{transactionId}:{paymentIntentId}"));
            return Convert.ToBase64String(signature, 0, 8).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static bool TryDecodeTransactionId(string code, out int transactionId)
        {
            const int signatureLength = 11;
            transactionId = 0;
            if (string.IsNullOrWhiteSpace(code) || code.Length <= signatureLength || code.Length > 24)
                return false;

            foreach (var character in code[..^signatureLength])
            {
                var value = Base62Alphabet.IndexOf(character);
                if (value < 0 || transactionId > (int.MaxValue - value) / 62) return false;
                transactionId = transactionId * 62 + value;
            }
            return transactionId > 0;
        }

        private static string EncodeBase62(int value)
        {
            Span<char> buffer = stackalloc char[6];
            var position = buffer.Length;
            do
            {
                buffer[--position] = Base62Alphabet[value % 62];
                value /= 62;
            } while (value > 0);
            return new string(buffer[position..]);
        }

        private static bool IsAllowedStripeUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            (uri.Host.Equals("stripe.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".stripe.com", StringComparison.OrdinalIgnoreCase));
        private static string GetUserMessage(string declineCode, string errorCode)
        {
            var code = declineCode ?? errorCode;
            return code switch
            {
                "insufficient_funds" => "Tu tarjeta no tiene fondos suficientes. Prueba con otra tarjeta.",
                "expired_card" => "Tu tarjeta está vencida. Prueba con otra tarjeta.",
                "incorrect_cvc" => "El código de seguridad de la tarjeta es incorrecto.",
                "lost_card" => "La tarjeta fue rechazada. Comunícate con tu banco o prueba con otra tarjeta.",
                "stolen_card" => "La tarjeta fue rechazada. Comunícate con tu banco o prueba con otra tarjeta.",
                "processing_error" => "No fue posible procesar la tarjeta. Inténtalo nuevamente.",
                "card_declined" => "Tu tarjeta fue rechazada. Prueba con otra tarjeta.",
                _ => "No fue posible procesar el pago. Revisa tu tarjeta o prueba con otra."
            };
        }
    }
}

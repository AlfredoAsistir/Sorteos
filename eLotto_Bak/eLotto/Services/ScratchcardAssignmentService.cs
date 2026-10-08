using System.Buffers.Binary;
using System.Security.Cryptography;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Services
{
    public interface IScratchcardAssignmentService
    {
        Task<int> AssignForConfirmedStripeDepositAsync(
            WalletTransaction transaction,
            CancellationToken cancellationToken);
    }

    public readonly record struct ScratchcardDistributionState(
        long GeneratedScratchcards,
        long AssignedWinners,
        long ParticipatingUsers,
        long WinningUsers,
        long UserAssignedWinners,
        long RemainingPrizes,
        long RemainingCapacity);

    public static class ScratchcardDistributionPolicy
    {
        private const decimal TargetWinningUserCoverage = 0.80m;

        public static long CalculateScratchcards(
            decimal depositAmount,
            decimal depositAmountPerScratchcard,
            bool scratchcardsEnabled = true)
        {
            if (!scratchcardsEnabled || depositAmount <= 0 || depositAmountPerScratchcard <= 0)
                return 0;

            var result = decimal.Floor(depositAmount / depositAmountPerScratchcard);
            return result >= long.MaxValue ? long.MaxValue : (long)result;
        }

        public static long CalculateCapacity(
            long totalConfiguredPrizes,
            int winnersPerGroup,
            int scratchcardsPerGroup)
        {
            if (totalConfiguredPrizes <= 0 || winnersPerGroup <= 0 || scratchcardsPerGroup <= 0)
                return 0;

            var capacity = decimal.Floor(
                (decimal)totalConfiguredPrizes * scratchcardsPerGroup / winnersPerGroup);
            return capacity >= long.MaxValue ? long.MaxValue : (long)capacity;
        }

        public static long CalculateStrategicRemainingCapacity(
            long capacity,
            long alreadyGenerated,
            long remainingPrizes,
            int winnersPerGroup,
            int scratchcardsPerGroup)
        {
            if (remainingPrizes <= 0)
                return 0;

            return alreadyGenerated < capacity
                ? capacity - alreadyGenerated
                : CalculateCapacity(remainingPrizes, winnersPerGroup, scratchcardsPerGroup);
        }

        public static decimal CalculateWinnerProbability(
            int winnersPerGroup,
            int scratchcardsPerGroup,
            ScratchcardDistributionState state)
        {
            if (winnersPerGroup <= 0 || scratchcardsPerGroup <= 0)
                return 0;
            if (state.RemainingPrizes <= 0 || state.RemainingCapacity <= 0)
                return 0;

            var baseProbability = Math.Min(
                1m,
                (decimal)winnersPerGroup / scratchcardsPerGroup);
            var expectedWinners = (state.GeneratedScratchcards + 1m) * baseProbability;
            var relativeGap =
                (expectedWinners - state.AssignedWinners) /
                Math.Max(1m, expectedWinners);
            var globalCorrection = Math.Clamp(
                1m + (0.75m * relativeGap),
                0.50m,
                1.75m);
            var remainingInventoryProbability = Math.Min(
                1m,
                (decimal)state.RemainingPrizes / state.RemainingCapacity);
            var inventoryCorrection = Math.Clamp(
                remainingInventoryProbability / baseProbability,
                0.50m,
                1.50m);

            decimal userCorrection;
            if (state.UserAssignedWinners == 0)
            {
                var winningUserCoverage = state.ParticipatingUsers > 0
                    ? (decimal)state.WinningUsers / state.ParticipatingUsers
                    : 0m;
                var dispersionGap = Math.Max(
                    0m,
                    TargetWinningUserCoverage - winningUserCoverage);
                userCorrection = 1.15m + (0.50m * dispersionGap);
            }
            else
            {
                userCorrection = 1m / (1m + (0.55m * state.UserAssignedWinners));
            }

            return Math.Clamp(
                baseProbability * globalCorrection * inventoryCorrection * userCorrection,
                0m,
                0.95m);
        }

        public static bool IsWinner(decimal probability, decimal randomSample) =>
            probability > 0 &&
            randomSample >= 0 &&
            randomSample < 1 &&
            randomSample < probability;

        public static int SelectWeightedIndex(
            IReadOnlyList<int> remainingUnits,
            long randomTicket)
        {
            var totalRemaining = remainingUnits.Sum(x => Math.Max(0L, x));
            if (totalRemaining <= 0 || randomTicket < 0 || randomTicket >= totalRemaining)
                return -1;

            long accumulated = 0;
            for (var index = 0; index < remainingUnits.Count; index++)
            {
                accumulated += Math.Max(0, remainingUnits[index]);
                if (randomTicket < accumulated) return index;
            }

            return -1;
        }
    }

    public static class ScratchcardPrizeEligibilityPolicy
    {
        public static decimal? GetPreferredMaximumPrize(decimal recentStripeDepositTotal) =>
            recentStripeDepositTotal switch
            {
                <= 200m => 50m,
                <= 400m => 100m,
                <= 500m => 200m,
                _ => null
            };

        public static IReadOnlyList<int> GetEligiblePrizeIndexes(
            IReadOnlyList<decimal> prizeAmounts,
            IReadOnlyList<int> remainingUnits,
            decimal recentStripeDepositTotal)
        {
            if (prizeAmounts.Count != remainingUnits.Count)
                throw new ArgumentException("Prize amounts and remaining units must have the same length.");

            var available = Enumerable.Range(0, prizeAmounts.Count)
                .Where(index => remainingUnits[index] > 0)
                .ToArray();
            if (available.Length == 0) return Array.Empty<int>();

            var maximum = GetPreferredMaximumPrize(recentStripeDepositTotal);
            if (!maximum.HasValue) return available;

            var preferred = available
                .Where(index => prizeAmounts[index] <= maximum.Value)
                .ToArray();
            if (preferred.Length > 0) return preferred;

            var nextAmount = available
                .Where(index => prizeAmounts[index] > maximum.Value)
                .Select(index => prizeAmounts[index])
                .DefaultIfEmpty()
                .Min();
            return nextAmount <= 0
                ? Array.Empty<int>()
                : available.Where(index => prizeAmounts[index] == nextAmount).ToArray();
        }

        public static async Task<decimal> CalculateRecentConfirmedStripeDepositTotalAsync(
            IQueryable<WalletTransaction> transactions,
            WalletTransaction currentDeposit,
            ISorteoTimeService sorteoTimeService,
            CancellationToken cancellationToken)
        {
            if (currentDeposit.Type != WalletTransactionType.Deposit ||
                string.IsNullOrWhiteSpace(currentDeposit.StripePaymentIntentId) ||
                currentDeposit.Amount <= 0)
                return 0m;

            var previousDeposits = await transactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.Id != currentDeposit.Id &&
                    transaction.UserId == currentDeposit.UserId &&
                    transaction.Type == WalletTransactionType.Deposit &&
                    transaction.Status == WalletTransactionStatus.Completed &&
                    transaction.CompletedAt.HasValue &&
                    transaction.StripePaymentIntentId != null &&
                    transaction.StripeEventId != null)
                .Select(transaction => new
                {
                    transaction.Id,
                    transaction.Amount,
                    CompletedAt = transaction.CompletedAt!.Value,
                    ZonaHoraria = transaction.Sorteo!.ZonaHoraria
                })
                .ToListAsync(cancellationToken);

            var previousAmount = previousDeposits
                .OrderByDescending(deposit => sorteoTimeService.ResolveRecordedTime(
                    deposit.CompletedAt,
                    new Sorteos { ZonaHoraria = deposit.ZonaHoraria }))
                .ThenByDescending(deposit => deposit.Id)
                .Select(deposit => deposit.Amount)
                .FirstOrDefault();

            return currentDeposit.Amount + previousAmount;
        }

        public static decimal CalculateRecentConfirmedStripeDepositTotal(
            WalletTransaction currentDeposit,
            IEnumerable<WalletTransaction> previousTransactions)
        {
            if (currentDeposit.Type != WalletTransactionType.Deposit ||
                string.IsNullOrWhiteSpace(currentDeposit.StripePaymentIntentId) ||
                currentDeposit.Amount <= 0)
                return 0m;

            var previousAmount = previousTransactions
                .Where(transaction =>
                    transaction.Id != currentDeposit.Id &&
                    transaction.UserId == currentDeposit.UserId &&
                    transaction.Type == WalletTransactionType.Deposit &&
                    transaction.Status == WalletTransactionStatus.Completed &&
                    transaction.CompletedAt.HasValue &&
                    !string.IsNullOrWhiteSpace(transaction.StripePaymentIntentId) &&
                    !string.IsNullOrWhiteSpace(transaction.StripeEventId))
                .OrderByDescending(transaction => transaction.CompletedAt)
                .ThenByDescending(transaction => transaction.Id)
                .Select(transaction => transaction.Amount)
                .FirstOrDefault();

            return currentDeposit.Amount + previousAmount;
        }
    }

    public static class ScratchcardFolioGenerator
    {
        public static string Create() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    }

    public class ScratchcardAssignmentService : IScratchcardAssignmentService
    {
        private readonly eLottoContext _context;
        private readonly ILogger<ScratchcardAssignmentService> _logger;
        private readonly ISorteoTimeService _sorteoTimeService;

        public ScratchcardAssignmentService(
            eLottoContext context,
            ILogger<ScratchcardAssignmentService> logger,
            ISorteoTimeService sorteoTimeService)
        {
            _context = context;
            _logger = logger;
            _sorteoTimeService = sorteoTimeService;
        }

        public async Task<int> AssignForConfirmedStripeDepositAsync(
            WalletTransaction transaction,
            CancellationToken cancellationToken)
        {
            if (_context.Database.CurrentTransaction == null)
                throw new InvalidOperationException(
                    "La asignación de rascaditos requiere una transacción activa.");
            if (transaction.Type != WalletTransactionType.Deposit ||
                string.IsNullOrWhiteSpace(transaction.StripePaymentIntentId) ||
                !transaction.SorteoId.HasValue)
                return 0;

            var alreadyAssigned = _context.ChangeTracker
                .Entries<SorteosRascaditos>()
                .Any(x =>
                    x.State == EntityState.Added &&
                    x.Entity.WalletTransactionOrigenId == transaction.Id) ||
                await _context.SorteosRascaditos
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.WalletTransactionOrigenId == transaction.Id,
                        cancellationToken);
            if (alreadyAssigned)
            {
                _logger.LogInformation(
                    "Wallet transaction {WalletTransactionId} already has its scratchcard batch; assignment was skipped.",
                    transaction.Id);
                return 0;
            }

            var sorteo = await _context.Sorteos
                .FromSqlInterpolated(
                    $"SELECT * FROM [Sorteos] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [Id] = {transaction.SorteoId.Value}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (sorteo == null || !sorteo.RascaditosHabilitados)
                return 0;

            var requested = ScratchcardDistributionPolicy.CalculateScratchcards(
                transaction.Amount,
                sorteo.ImporteDepositoStripePorRascadito);
            if (requested == 0)
            {
                _logger.LogInformation(
                    "Wallet transaction {WalletTransactionId} did not reach the configured scratchcard amount for lottery {LotteryId}.",
                    transaction.Id,
                    sorteo.Id);
                return 0;
            }

            var prizes = await _context.SorteosRascaditoPremios
                .FromSqlInterpolated(
                    $"SELECT * FROM [SorteosRascaditoPremios] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [SorteosId] = {sorteo.Id}")
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var recentStripeDepositTotal = await
                ScratchcardPrizeEligibilityPolicy.CalculateRecentConfirmedStripeDepositTotalAsync(
                    _context.WalletTransactions,
                    transaction,
                    _sorteoTimeService,
                    cancellationToken);

            var totalConfiguredPrizes = prizes.Sum(x => (long)x.Cantidad);
            var capacity = ScratchcardDistributionPolicy.CalculateCapacity(
                totalConfiguredPrizes,
                sorteo.GanadoresPorGrupo,
                sorteo.RascaditosPorGrupo);
            if (capacity == 0 || prizes.All(x => x.Entregados >= x.Cantidad))
            {
                _logger.LogInformation(
                    "Lottery {LotteryId} has no scratchcard prizes available; wallet transaction {WalletTransactionId} generated none.",
                    sorteo.Id,
                    transaction.Id);
                return 0;
            }

            var scratchcardsQuery = _context.SorteosRascaditos
                .Where(x => x.SorteosId == sorteo.Id);
            var generated = await scratchcardsQuery.LongCountAsync(cancellationToken);

            var assignedWinners = await scratchcardsQuery
                .LongCountAsync(x => x.EsGanador, cancellationToken);
            var participatingUsers = await scratchcardsQuery
                .Select(x => x.UsuarioId)
                .Distinct()
                .LongCountAsync(cancellationToken);
            var winningUsers = await scratchcardsQuery
                .Where(x => x.EsGanador)
                .Select(x => x.UsuarioId)
                .Distinct()
                .LongCountAsync(cancellationToken);
            var userAssignedWinners = await scratchcardsQuery
                .LongCountAsync(
                    x => x.UsuarioId == transaction.UserId && x.EsGanador,
                    cancellationToken);
            var userAlreadyParticipates = await scratchcardsQuery
                .AnyAsync(x => x.UsuarioId == transaction.UserId, cancellationToken);
            if (!userAlreadyParticipates) participatingUsers++;

            var generatedAt = _sorteoTimeService.ConvertToSorteoTime(
                ApplicationClock.NowOffset, sorteo).DateTime;
            var created = 0;
            for (long index = 0; index < requested; index++)
            {
                var remainingPrizes = prizes.Sum(x => Math.Max(0L, x.Cantidad - x.Entregados));
                if (remainingPrizes == 0) break;

                var state = new ScratchcardDistributionState(
                    generated,
                    assignedWinners,
                    participatingUsers,
                    winningUsers,
                    userAssignedWinners,
                    remainingPrizes,
                    ScratchcardDistributionPolicy.CalculateStrategicRemainingCapacity(
                        capacity,
                        generated,
                        remainingPrizes,
                        sorteo.GanadoresPorGrupo,
                        sorteo.RascaditosPorGrupo));
                var probability = ScratchcardDistributionPolicy.CalculateWinnerProbability(
                    sorteo.GanadoresPorGrupo,
                    sorteo.RascaditosPorGrupo,
                    state);
                var shouldWin = ScratchcardDistributionPolicy.IsWinner(
                    probability,
                    ScratchcardSecureRandom.NextDecimal());
                var reservedPrize = shouldWin
                    ? await TryReservePrizeAsync(
                        sorteo.Id,
                        prizes,
                        recentStripeDepositTotal,
                        cancellationToken)
                    : null;
                var isWinner = reservedPrize != null;

                _context.SorteosRascaditos.Add(new SorteosRascaditos
                {
                    SorteosId = sorteo.Id,
                    UsuarioId = transaction.UserId,
                    Folio = ScratchcardFolioGenerator.Create(),
                    WalletTransactionOrigenId = transaction.Id,
                    EsGanador = isWinner,
                    SorteosRascaditoPremioId = reservedPrize?.Id,
                    ImportePremio = reservedPrize?.Premio,
                    Revelado = false,
                    FechaGeneracion = generatedAt,
                    FechaRevelado = null
                });

                generated++;
                created++;
                if (!isWinner) continue;

                assignedWinners++;
                if (userAssignedWinners == 0) winningUsers++;
                userAssignedWinners++;
            }

            _logger.LogInformation(
                "Generated {ScratchcardCount} scratchcards for wallet transaction {WalletTransactionId} in lottery {LotteryId}; requested {RequestedCount}, strategic capacity {Capacity}.",
                created,
                transaction.Id,
                sorteo.Id,
                requested,
                capacity);
            return created;
        }

        private async Task<SorteosRascaditoPremios> TryReservePrizeAsync(
            int sorteoId,
            IReadOnlyList<SorteosRascaditoPremios> prizes,
            decimal recentStripeDepositTotal,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var remaining = prizes
                    .Select(x => Math.Max(0, x.Cantidad - x.Entregados))
                    .ToArray();
                var eligibleIndexes = ScratchcardPrizeEligibilityPolicy.GetEligiblePrizeIndexes(
                    prizes.Select(prize => prize.Premio).ToArray(),
                    remaining,
                    recentStripeDepositTotal);
                var eligibleRemaining = remaining
                    .Select((units, index) => eligibleIndexes.Contains(index) ? units : 0)
                    .ToArray();
                var totalRemaining = eligibleRemaining.Sum(x => (long)x);
                if (totalRemaining == 0) return null;

                var selectedIndex = ScratchcardDistributionPolicy.SelectWeightedIndex(
                    eligibleRemaining,
                    ScratchcardSecureRandom.NextInt64(totalRemaining));
                if (selectedIndex < 0) return null;

                var selected = prizes[selectedIndex];
                var affected = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [SorteosRascaditoPremios] WITH (ROWLOCK) SET [Entregados] = [Entregados] + 1 WHERE [Id] = {selected.Id} AND [SorteosId] = {sorteoId} AND [Entregados] < [Cantidad]",
                    cancellationToken);
                if (affected == 1)
                {
                    selected.Entregados++;
                    return selected;
                }

                selected.Entregados = selected.Cantidad;
            }
        }
    }

    internal static class ScratchcardSecureRandom
    {
        public static decimal NextDecimal()
        {
            var value = NextUInt64();
            return value / ((decimal)ulong.MaxValue + 1m);
        }

        public static long NextInt64(long exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));

            var bound = (ulong)exclusiveMaximum;
            var threshold = unchecked((0UL - bound) % bound);
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);

            return (long)(value % bound);
        }

        private static ulong NextUInt64()
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            RandomNumberGenerator.Fill(bytes);
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }
    }
}

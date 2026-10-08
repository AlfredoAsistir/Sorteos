using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using eLotto.Models;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Services
{
    public interface IScratchcardRevealService
    {
        Task<ScratchcardListResponse> GetListAsync(
            int userId,
            CancellationToken cancellationToken);

        Task<ScratchcardStartResponse> StartAsync(
            int scratchcardId,
            int userId,
            CancellationToken cancellationToken);

        Task<ScratchcardRevealResponse> RevealAsync(
            int scratchcardId,
            int userId,
            CancellationToken cancellationToken);
    }

    public sealed class ScratchcardDataIntegrityException : Exception
    {
        public ScratchcardDataIntegrityException(string message) : base(message)
        {
        }
    }

    public sealed class ScratchcardRevealService : IScratchcardRevealService
    {
        private const int MaximumListItems = 50;
        private readonly eLottoContext _context;
        private readonly ILogger<ScratchcardRevealService> _logger;
        private readonly ISorteoTimeService _sorteoTimeService;

        public ScratchcardRevealService(
            eLottoContext context,
            ILogger<ScratchcardRevealService> logger,
            ISorteoTimeService sorteoTimeService)
        {
            _context = context;
            _logger = logger;
            _sorteoTimeService = sorteoTimeService;
        }

        public async Task<ScratchcardListResponse> GetListAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var userScratchcards = _context.SorteosRascaditos
                .AsNoTracking()
                .Where(x => x.UsuarioId == userId);
            var pendingCount = await userScratchcards
                .CountAsync(x => !x.Revelado, cancellationToken);
            var items = await userScratchcards
                .OrderBy(x => x.Revelado)
                .ThenByDescending(x => x.Id)
                .Take(MaximumListItems)
                .Select(x => new ScratchcardListItemResponse(
                    x.Id,
                    x.Folio,
                    x.Revelado,
                    x.FechaGeneracion,
                    x.FechaRevelado))
                .ToListAsync(cancellationToken);

            return new ScratchcardListResponse(pendingCount, items);
        }

        public async Task<ScratchcardStartResponse> StartAsync(
            int scratchcardId,
            int userId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var scratchcard = await _context.SorteosRascaditos
                .FromSqlInterpolated(
                    $"SELECT * FROM [SorteosRascaditos] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [Id] = {scratchcardId} AND [UsuarioId] = {userId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (scratchcard == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            await ValidatePersistedResultAsync(scratchcard, cancellationToken);
            var visualResultCreated = EnsureVisualResult(scratchcard);
            var possiblePrize = await ResolvePossiblePrizeAsync(
                scratchcard,
                cancellationToken);
            if (visualResultCreated)
                await _context.SaveChangesAsync(cancellationToken);

            var visualResult = ScratchcardVisualResultGenerator.Deserialize(
                scratchcard.MatrizResultado,
                scratchcard.LineaGanadora);
            var response = new ScratchcardStartResponse(
                scratchcard.Id,
                scratchcard.Folio,
                possiblePrize,
                visualResult.Cells);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        public async Task<ScratchcardRevealResponse> RevealAsync(
            int scratchcardId,
            int userId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var scratchcard = await _context.SorteosRascaditos
                .FromSqlInterpolated(
                    $"SELECT * FROM [SorteosRascaditos] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [Id] = {scratchcardId} AND [UsuarioId] = {userId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (scratchcard == null)
            {
                var ownerId = await _context.SorteosRascaditos
                    .AsNoTracking()
                    .Where(x => x.Id == scratchcardId)
                    .Select(x => (int?)x.UsuarioId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (ownerId.HasValue && ownerId.Value != userId)
                {
                    _logger.LogWarning(
                        "User {UserId} attempted to reveal scratchcard {ScratchcardId} owned by another user.",
                        userId,
                        scratchcardId);
                }

                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var wallet = await _context.UserWallets
                .FromSqlInterpolated(
                    $"SELECT * FROM [UserWallets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE [UserId] = {userId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (wallet == null)
                throw IntegrityError(
                    scratchcard,
                    "The scratchcard owner does not have a wallet.");

            await ValidatePersistedResultAsync(scratchcard, cancellationToken);
            var visualResultCreated = EnsureVisualResult(scratchcard);
            var possiblePrize = await ResolvePossiblePrizeAsync(
                scratchcard,
                cancellationToken);
            if (scratchcard.Revelado)
            {
                await ValidateExistingPrizeTransactionAsync(
                    scratchcard,
                    wallet,
                    cancellationToken);
                if (visualResultCreated)
                    await _context.SaveChangesAsync(cancellationToken);

                var existingResponse = CreateResponse(
                    scratchcard,
                    wallet.Balance,
                    possiblePrize,
                    false);
                await transaction.CommitAsync(cancellationToken);
                return existingResponse;
            }

            var revealedMoment = ApplicationClock.NowOffset;
            var sorteo = await _context.Sorteos
                .AsNoTracking()
                .Where(x => x.Id == scratchcard.SorteosId)
                .Select(x => new Sorteos { ZonaHoraria = x.ZonaHoraria })
                .SingleAsync(cancellationToken);
            var revealedAt = _sorteoTimeService.ConvertToSorteoTime(
                revealedMoment, sorteo).DateTime;
            scratchcard.Revelado = true;
            scratchcard.FechaRevelado = revealedAt;

            if (scratchcard.EsGanador)
            {
                if (scratchcard.WalletTransactionPremioId.HasValue)
                    throw IntegrityError(
                        scratchcard,
                        "An unrevealed scratchcard already references a prize transaction.");

                var prizeAmount = scratchcard.ImportePremio!.Value;
                var prizeTransaction = new WalletTransaction
                {
                    UserId = userId,
                    WalletId = wallet.Id,
                    SorteoId = scratchcard.SorteosId,
                    Type = WalletTransactionType.ScratchcardPrize,
                    Amount = prizeAmount,
                    Status = WalletTransactionStatus.Completed,
                    Description = CreateDescription(scratchcard.Folio),
                    CreatedAt = revealedAt,
                    CompletedAt = revealedAt
                };
                scratchcard.WalletTransactionPremio = prizeTransaction;
                wallet.Balance += prizeAmount;
                wallet.UpdatedAt = revealedMoment.DateTime;
            }

            await _context.SaveChangesAsync(cancellationToken);
            var response = CreateResponse(
                scratchcard,
                wallet.Balance,
                possiblePrize,
                true);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }

        private bool EnsureVisualResult(SorteosRascaditos scratchcard)
        {
            if (string.IsNullOrWhiteSpace(scratchcard.MatrizResultado))
            {
                if (!string.IsNullOrWhiteSpace(scratchcard.LineaGanadora))
                    throw IntegrityError(
                        scratchcard,
                        "A winning line exists without its visual matrix.");

                var generated = ScratchcardVisualResultGenerator.Generate(
                    scratchcard.EsGanador);
                scratchcard.MatrizResultado =
                    ScratchcardVisualResultGenerator.SerializeCells(generated.Cells);
                scratchcard.LineaGanadora = generated.WinningLine;
                return true;
            }

            ScratchcardVisualResult persisted;
            try
            {
                persisted = ScratchcardVisualResultGenerator.Deserialize(
                    scratchcard.MatrizResultado,
                    scratchcard.LineaGanadora);
            }
            catch (Exception exception) when (
                exception is System.Text.Json.JsonException ||
                exception is NotSupportedException)
            {
                throw IntegrityError(
                    scratchcard,
                    "The persisted visual matrix cannot be read.");
            }

            if (!ScratchcardVisualResultGenerator.IsValid(
                    persisted,
                    scratchcard.EsGanador))
            {
                throw IntegrityError(
                    scratchcard,
                    "The persisted visual matrix does not match the result.");
            }

            return false;
        }

        private async Task ValidatePersistedResultAsync(
            SorteosRascaditos scratchcard,
            CancellationToken cancellationToken)
        {
            if (scratchcard.Revelado != scratchcard.FechaRevelado.HasValue)
                throw IntegrityError(
                    scratchcard,
                    "The revealed state and reveal date are inconsistent.");

            if (scratchcard.EsGanador)
            {
                if (!scratchcard.SorteosRascaditoPremioId.HasValue ||
                    !scratchcard.ImportePremio.HasValue ||
                    scratchcard.ImportePremio.Value <= 0)
                {
                    throw IntegrityError(
                        scratchcard,
                        "A winning scratchcard has an invalid persisted prize.");
                }

                var prize = await _context.SorteosRascaditoPremios
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.Id == scratchcard.SorteosRascaditoPremioId.Value &&
                            x.SorteosId == scratchcard.SorteosId,
                        cancellationToken);
                if (prize == null)
                    throw IntegrityError(
                        scratchcard,
                        "The assigned prize does not exist in the scratchcard lottery.");

                var assignedWinners = await _context.SorteosRascaditos
                    .AsNoTracking()
                    .CountAsync(
                        x => x.SorteosRascaditoPremioId == prize.Id &&
                            x.EsGanador,
                        cancellationToken);
                if (prize.Entregados != assignedWinners)
                    throw IntegrityError(
                        scratchcard,
                        "The assigned prize inventory does not match its winning scratchcards.");

                return;
            }

            if (scratchcard.SorteosRascaditoPremioId.HasValue ||
                scratchcard.ImportePremio.HasValue ||
                scratchcard.WalletTransactionPremioId.HasValue)
            {
                throw IntegrityError(
                    scratchcard,
                    "A losing scratchcard contains prize information.");
            }
        }

        private async Task<decimal> ResolvePossiblePrizeAsync(
            SorteosRascaditos scratchcard,
            CancellationToken cancellationToken)
        {
            if (scratchcard.EsGanador)
                return scratchcard.ImportePremio!.Value;

            var configuredPrizes = await _context.SorteosRascaditoPremios
                .AsNoTracking()
                .Where(x =>
                    x.SorteosId == scratchcard.SorteosId &&
                    x.Premio > 0 &&
                    x.Cantidad > 0)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Premio, x.Cantidad })
                .ToListAsync(cancellationToken);
            var totalWeight = configuredPrizes.Sum(x => (long)x.Cantidad);
            if (totalWeight <= 0)
                throw IntegrityError(
                    scratchcard,
                    "The scratchcard lottery has no configured possible prizes.");

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scratchcard.Folio));
            var ticket = (long)(
                BinaryPrimitives.ReadUInt64LittleEndian(hash) %
                (ulong)totalWeight);
            long accumulated = 0;
            foreach (var configuredPrize in configuredPrizes)
            {
                accumulated += configuredPrize.Cantidad;
                if (ticket < accumulated)
                    return configuredPrize.Premio;
            }

            throw IntegrityError(
                scratchcard,
                "A possible prize could not be selected.");
        }
        private async Task ValidateExistingPrizeTransactionAsync(
            SorteosRascaditos scratchcard,
            UserWallet wallet,
            CancellationToken cancellationToken)
        {
            if (!scratchcard.EsGanador) return;
            if (!scratchcard.WalletTransactionPremioId.HasValue)
                throw IntegrityError(
                    scratchcard,
                    "A revealed winning scratchcard has no prize transaction.");

            var prizeTransaction = await _context.WalletTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == scratchcard.WalletTransactionPremioId.Value,
                    cancellationToken);
            if (prizeTransaction == null ||
                prizeTransaction.UserId != scratchcard.UsuarioId ||
                prizeTransaction.WalletId != wallet.Id ||
                prizeTransaction.Type != WalletTransactionType.ScratchcardPrize ||
                prizeTransaction.Status != WalletTransactionStatus.Completed ||
                prizeTransaction.Amount != scratchcard.ImportePremio ||
                prizeTransaction.Description != CreateDescription(scratchcard.Folio))
            {
                throw IntegrityError(
                    scratchcard,
                    "The existing prize transaction is inconsistent with the scratchcard.");
            }
        }

        private ScratchcardDataIntegrityException IntegrityError(
            SorteosRascaditos scratchcard,
            string detail)
        {
            _logger.LogError(
                "Scratchcard {ScratchcardId} ({Folio}) has inconsistent financial data: {Detail}",
                scratchcard.Id,
                scratchcard.Folio,
                detail);
            return new ScratchcardDataIntegrityException(
                "No fue posible acreditar el premio por una inconsistencia de datos.");
        }

        private static string CreateDescription(string folio) =>
            $"Premio de Rascadito {folio}";

        private static ScratchcardRevealResponse CreateResponse(
            SorteosRascaditos scratchcard,
            decimal currentBalance,
            decimal possiblePrize,
            bool revealedNow)
        {
            var visualResult = ScratchcardVisualResultGenerator.Deserialize(
                scratchcard.MatrizResultado,
                scratchcard.LineaGanadora);
            return new ScratchcardRevealResponse(
                scratchcard.Id,
                scratchcard.Folio,
                scratchcard.Revelado,
                revealedNow,
                scratchcard.EsGanador,
                scratchcard.EsGanador ? scratchcard.ImportePremio : null,
                possiblePrize,
                scratchcard.FechaRevelado!.Value,
                currentBalance,
                visualResult.Cells,
                visualResult.WinningLine);
        }
    }
}

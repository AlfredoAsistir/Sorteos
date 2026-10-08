using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using eLotto.Models;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Services
{
    public interface IUserPrizeHistoryService
    {
        Task<UserPrizeHistoryResponse> GetAsync(
            int userId,
            CancellationToken cancellationToken);
    }

    public sealed class UserPrizeHistoryService : IUserPrizeHistoryService
    {
        private readonly eLottoContext _context;
        private readonly ISorteoTimeService _sorteoTimeService;

        public UserPrizeHistoryService(eLottoContext context, ISorteoTimeService sorteoTimeService)
        {
            _context = context;
            _sorteoTimeService = sorteoTimeService;
        }

        public async Task<UserPrizeHistoryResponse> GetAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var lotteryPrizes = await _context.Sorteos
                .AsNoTracking()
                .Where(x =>
                    x.UsuarioIdGanador == userId &&
                    x.NumeroGanador != null &&
                    x.NumeroGanador != "")
                .Select(x => new UserPrizeHistoryItemResponse(
                    x.Id,
                    "sorteo",
                    "sorteo",
                    x.Fecha,
                    x.Id,
                    x.Nombre,
                    x.Imagen1,
                    x.NumeroGanador,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
                .ToListAsync(cancellationToken);

            var storedScratchcards = await _context.SorteosRascaditos
                .AsNoTracking()
                .Where(x =>
                    x.UsuarioId == userId &&
                    x.Revelado &&
                    x.EsGanador &&
                    x.FechaRevelado.HasValue &&
                    x.ImportePremio.HasValue &&
                    x.WalletTransactionPremioId.HasValue)
                .Select(x => new
                {
                    x.Id,
                    Fecha = x.FechaRevelado.Value,
                    x.SorteosId,
                    SorteoNombre = x.Sorteos.Nombre,
                    SorteoImagen = x.Sorteos.Imagen1,
                    x.Folio,
                    ImportePremio = x.ImportePremio.Value,
                    x.MatrizResultado,
                    x.LineaGanadora
                })
                .ToListAsync(cancellationToken);

            var scratchcardPrizes = storedScratchcards
                .Select(x =>
                {
                    var visual = ScratchcardVisualResultGenerator.Deserialize(
                        x.MatrizResultado,
                        x.LineaGanadora);
                    if (!ScratchcardVisualResultGenerator.IsValid(visual, true))
                        throw new ScratchcardDataIntegrityException(
                            $"El rascadito ganador {x.Id} no tiene un resultado visual válido.");

                    return new UserPrizeHistoryItemResponse(
                        x.Id,
                        "rascadito",
                        "vigente",
                        x.Fecha,
                        x.SorteosId,
                        x.SorteoNombre,
                        x.SorteoImagen,
                        null,
                        x.Id,
                        x.Folio,
                        x.ImportePremio,
                        visual.Cells,
                        visual.WinningLine,
                        null,
                        null,
                        null);
                })
                .ToList();

            var storedHistoricalScratchcards = await _context.SorteosRascaditosGanadoresHistorial
                .AsNoTracking()
                .Where(x => x.UsuarioId == userId)
                .Select(x => new
                {
                    x.Id,
                    Fecha = x.FechaRevelado,
                    x.SorteoId,
                    x.SorteoNombre,
                    SorteoImagen = x.SorteoImagen,
                    x.Folio,
                    x.ImportePremio,
                    x.MatrizResultado,
                    x.LineaGanadora
                })
                .ToListAsync(cancellationToken);

            var historicalScratchcardPrizes = storedHistoricalScratchcards
                .Select(x =>
                {
                    var visual = ScratchcardVisualResultGenerator.Deserialize(
                        x.MatrizResultado,
                        x.LineaGanadora);
                    if (!ScratchcardVisualResultGenerator.IsValid(visual, true))
                        throw new ScratchcardDataIntegrityException(
                            $"El rascadito ganador histórico {x.Id} no tiene un resultado visual válido.");

                    return new UserPrizeHistoryItemResponse(
                        x.Id,
                        "rascadito",
                        "historico",
                        x.Fecha,
                        x.SorteoId,
                        x.SorteoNombre,
                        x.SorteoImagen,
                        null,
                        x.Id,
                        x.Folio,
                        x.ImportePremio,
                        visual.Cells,
                        visual.WinningLine,
                        null,
                        null,
                        null);
                })
                .ToList();

            var referralCashPrizes = await (
                from reward in _context.ReferralWinnerCashRewards.AsNoTracking()
                join winner in _context.GanadoresSorteos.AsNoTracking()
                    on reward.WinnerRecordId equals winner.Id
                where reward.ReferrerUserId == userId
                select new UserPrizeHistoryItemResponse(
                    reward.Id,
                    "referido",
                    "referido",
                    reward.CreatedAt,
                    winner.SorteoId ?? 0,
                    winner.Nombre,
                    null,
                    null,
                    null,
                    null,
                    reward.RewardAmount,
                    null,
                    null,
                    winner.NombreGanador,
                    reward.RequiredTickets,
                    reward.ActualTickets))
                .ToListAsync(cancellationToken);

            var allPrizes = lotteryPrizes
                .Concat(scratchcardPrizes)
                .Concat(historicalScratchcardPrizes)
                .Concat(referralCashPrizes)
                .ToList();
            var sorteoIds = allPrizes.Select(x => x.SorteoId).Distinct().ToArray();
            var zones = await _context.Sorteos.AsNoTracking()
                .Where(x => sorteoIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.ZonaHoraria, cancellationToken);
            var prizes = allPrizes
                .OrderByDescending(x =>
                {
                    var sorteo = new Sorteos { Fecha = x.Fecha, ZonaHoraria = zones[x.SorteoId] };
                    return x.Tipo == "sorteo"
                        ? _sorteoTimeService.ResolveScheduledTime(sorteo)
                        : _sorteoTimeService.ResolveRecordedTime(x.Fecha, sorteo);
                })
                .ThenByDescending(x => x.Id)
                .ToList();

            return new UserPrizeHistoryResponse(
                prizes.Count,
                lotteryPrizes.Count,
                scratchcardPrizes.Count + historicalScratchcardPrizes.Count,
                referralCashPrizes.Count,
                scratchcardPrizes
                    .Concat(historicalScratchcardPrizes)
                    .Sum(x => x.ImportePremio ?? 0m),
                referralCashPrizes.Sum(x => x.ImportePremio ?? 0m),
                prizes);
        }
    }
}

using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace eLotto.Core.Repository
{
    public enum SorteoRascaditoPremioDeleteResult
    {
        Deleted,
        NotFound,
        HasDeliveredPrizes
    }

    public enum SorteoResultadoStatus
    {
        Ok,
        SorteoNoEncontrado,
        SorteoNoIniciado,
        SorteoFinalizado,
        NumeroInvalido,
        HayGanador,
        NumeroNoVendido,
        VentaMinimaNoAlcanzada,
        DocumentoTransparenciaNoPublicado
    }

    public sealed record SorteoGanadorVerificacion(
        SorteoResultadoStatus Status,
        int SorteoId,
        string NumeroGanador,
        bool HayGanador,
        int? UsuarioIdGanador,
        string UsuarioGanador,
        string WhatsAppGanador,
        string FolioCompra,
        SorteoGanadorReferencia Referencia);

    public sealed record SorteoGanadorReferencia(
        string ReferidorNombre,
        bool Finalizado,
        bool? PremioGenerado,
        int? BoletosRequeridos,
        int? BoletosConfirmados,
        decimal? ImportePremio,
        ReferralWinnerCashRewardStatus? Estado,
        DateTime? FechaPago);

    public sealed record SorteoGanadorFinalizado(
        int SorteoId,
        string SorteoNombre,
        string NumeroGanador,
        string UsuarioGanador,
        string FolioCompra,
        DateTime FechaFinalizacion,
        SorteoGanadorReferencia Referencia);

    public sealed record SorteoReprogramacionResultado(
        SorteoResultadoStatus Status,
        int SorteoId,
        string NumeroGanador,
        DateTime? NuevaFecha,
        int SorteosReprogramados);

    public sealed record SorteoFinalizacionResultado(
        SorteoResultadoStatus Status,
        int SorteoId,
        string NumeroGanador,
        int ComprasArchivadas,
        int RascaditosGanadoresArchivados,
        int RascaditosCaducados,
        SorteoGanadorFinalizado Ganador);

    public interface ISorteosRepository
    {
        Task<(Sorteos Sorteo, int TotalRecords)> GetPageAsync(int page);
        Task<Sorteos> GetByIdAsync(int id);
        Task<Sorteos> CreateAsync(Sorteos sorteo);
        Task<bool> UpdateAsync(Sorteos sorteo);
        Task<SorteoRascaditoPremioDeleteResult> DeleteScratchcardPrizeAsync(
            int sorteoId,
            int prizeId,
            CancellationToken cancellationToken);
        Task<bool> RemoveOptionalImageAsync(int id, int imageNumber);
        Task<bool> DeleteAsync(int id);
        Task<SorteoGanadorVerificacion> VerifyWinningNumberAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken);
        Task<SorteoReprogramacionResultado> RescheduleWithoutWinnerAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken);
        Task<SorteoFinalizacionResultado> FinalizeWinnerAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken);
        Task<SorteoGanadorFinalizado> GetLatestFinalizedWinnerAsync(
            CancellationToken cancellationToken);
    }

    public class SorteosRepository : ISorteosRepository
    {
        private readonly eLottoContext _context;
        private readonly int _rescheduleDays;
        private readonly int _salesCloseMinutes;
        private readonly ISorteoTimeService _sorteoTimeService;
        private readonly ISorteoTransparencyFileStore _transparencyFiles;

        public SorteosRepository(eLottoContext context) : this(context, null, new SorteoTimeService(),
            new SorteoTransparencyFileStore(SorteoTransparencyFileStore.DefaultDirectory)) { }

        public SorteosRepository(eLottoContext context, IConfiguration configuration, ISorteoTimeService sorteoTimeService,
            ISorteoTransparencyFileStore transparencyFiles)
        {
            _context = context;
            _sorteoTimeService = sorteoTimeService;
            _transparencyFiles = transparencyFiles;
            _rescheduleDays = int.TryParse(configuration?["LotteryRules:RescheduleDays"], out var value) && value > 0 ? value : 7;
            _salesCloseMinutes = int.TryParse(configuration?["LotteryRules:SalesCloseMinutesBeforeDraw"], out var closeMinutes) && closeMinutes > 0
                ? closeMinutes : SorteoDisponibilidadPolicy.MinutosBloqueoPredeterminados;
        }

        public async Task<(Sorteos Sorteo, int TotalRecords)> GetPageAsync(int page)
        {
            var totalRecords = await _context.Sorteos.CountAsync();
            var sorteo = await _context.Sorteos.AsNoTracking()
                .Include(x => x.RascaditoPremios.OrderBy(premio => premio.Id))
                .OrderByDescending(x => x.Id)
                .Skip(page - 1)
                .FirstOrDefaultAsync();
            return (sorteo, totalRecords);
        }

        public Task<Sorteos> GetByIdAsync(int id) =>
            _context.Sorteos.AsNoTracking()
                .Include(x => x.RascaditoPremios.OrderBy(premio => premio.Id))
                .FirstOrDefaultAsync(x => x.Id == id);

        public async Task<Sorteos> CreateAsync(Sorteos sorteo)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Sorteos.AddAsync(sorteo);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return sorteo;
        }

        public async Task<bool> UpdateAsync(Sorteos sorteo)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AcquireLotteryLockAsync(sorteo.Id, CancellationToken.None);
            var current = await _context.Sorteos
                .Include(x => x.RascaditoPremios)
                .FirstOrDefaultAsync(x => x.Id == sorteo.Id);
            if (current == null) return false;

            if (current.Fecha != sorteo.Fecha || current.ZonaHoraria != sorteo.ZonaHoraria ||
                current.CantidadBoletos != sorteo.CantidadBoletos ||
                current.PorcentajeMinimoVenta != sorteo.PorcentajeMinimoVenta)
            {
                var hasSales = await _context.BoletosConfirmados.AsNoTracking()
                    .AnyAsync(x => x.SorteosId == sorteo.Id);
                var hasDeposits = current.ZonaHoraria != sorteo.ZonaHoraria &&
                    await _context.WalletTransactions.AsNoTracking()
                        .AnyAsync(x => x.SorteoId == sorteo.Id && x.Type == WalletTransactionType.Deposit);
                if (hasDeposits)
                    throw new InvalidOperationException("La zona horaria del sorteo no puede modificarse después de iniciar un depósito.");
                if (hasSales || _transparencyFiles.Exists(current) || ApplicationClock.NowOffset >=
                    SorteoDisponibilidadPolicy.ObtenerInicioBloqueo(current, _sorteoTimeService, _salesCloseMinutes))
                    throw new InvalidOperationException("La fecha, zona horaria, numeración y venta mínima no pueden modificarse después de vender boletos o cerrar ventas. Utiliza la reprogramación.");
            }

            _context.Entry(current).CurrentValues.SetValues(sorteo);
            foreach (var currentPrize in current.RascaditoPremios.ToList())
            {
                var incomingPrize = sorteo.RascaditoPremios.SingleOrDefault(x => x.Id == currentPrize.Id);
                if (incomingPrize == null)
                {
                    _context.SorteosRascaditoPremios.Remove(currentPrize);
                    continue;
                }

                currentPrize.Premio = incomingPrize.Premio;
                currentPrize.Cantidad = incomingPrize.Cantidad;
            }

            foreach (var newPrize in sorteo.RascaditoPremios.Where(x => x.Id == 0))
            {
                current.RascaditoPremios.Add(new SorteosRascaditoPremios
                {
                    Premio = newPrize.Premio,
                    Cantidad = newPrize.Cantidad,
                    Entregados = 0
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<SorteoRascaditoPremioDeleteResult> DeleteScratchcardPrizeAsync(
            int sorteoId,
            int prizeId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var sorteo = await _context.Sorteos
                .Include(x => x.RascaditoPremios)
                .FirstOrDefaultAsync(x => x.Id == sorteoId, cancellationToken);
            var prize = sorteo?.RascaditoPremios.SingleOrDefault(x => x.Id == prizeId);
            if (prize == null) return SorteoRascaditoPremioDeleteResult.NotFound;
            if (prize.Entregados > 0) return SorteoRascaditoPremioDeleteResult.HasDeliveredPrizes;

            _context.SorteosRascaditoPremios.Remove(prize);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SorteoRascaditoPremioDeleteResult.Deleted;
        }

        public async Task<bool> RemoveOptionalImageAsync(int id, int imageNumber)
        {
            var sorteo = await _context.Sorteos.FindAsync(id);
            if (sorteo == null) return false;

            if (imageNumber == 2)
            {
                sorteo.Imagen2 = null;
                sorteo.Imagen2Tema = null;
            }
            else if (imageNumber == 3)
            {
                sorteo.Imagen3 = null;
                sorteo.Imagen3Tema = null;
            }
            else return false;

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> DeleteAsync(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await AcquireLotteryLockAsync(id, CancellationToken.None);
            var sorteo = await _context.Sorteos.FindAsync(id);
            if (sorteo == null) return false;
            if (await _context.BoletosConfirmados.AnyAsync(x => x.SorteosId == id) ||
                _transparencyFiles.Exists(sorteo))
                throw new InvalidOperationException("No se puede eliminar un sorteo con boletos vendidos o documentos de transparencia.");
            _context.Sorteos.Remove(sorteo);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<SorteoGanadorVerificacion> VerifyWinningNumberAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken)
        {
            var sorteo = await _context.Sorteos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sorteoId, cancellationToken);
            var validation = ValidateLotteryForResult(sorteo, numeroGanador);
            if (validation.Status != SorteoResultadoStatus.Ok)
                return new(validation.Status, sorteoId, validation.Number, false, null, null, null, null, null);

            if (!await HasPublishedTransparencyAsync(sorteo!, cancellationToken))
                return new(SorteoResultadoStatus.DocumentoTransparenciaNoPublicado, sorteoId, validation.Number, false, null, null, null, null, null);

            var ticket = await FindWinnerAsync(sorteoId, validation.Number, cancellationToken);
            if (ticket == null)
                return new(SorteoResultadoStatus.Ok, sorteoId, validation.Number, false, null, null, null, null, null);

            var referralEvaluation = await EvaluateReferralWinnerAsync(
                ticket.UsuarioId,
                sorteoId,
                cancellationToken);
            return
                new(
                    SorteoResultadoStatus.Ok,
                    sorteoId,
                    validation.Number,
                    true,
                    ticket.UsuarioId,
                    ticket.Usuario,
                    MaskWhatsApp(ticket.WhatsApp),
                    ticket.FolioCompra,
                    referralEvaluation != null
                        ? new SorteoGanadorReferencia(
                            referralEvaluation.ReferrerName,
                            false,
                            referralEvaluation.SettingsValid
                                ? referralEvaluation.Eligible
                                : null,
                            referralEvaluation.RequiredTickets,
                            referralEvaluation.ActualTickets,
                            referralEvaluation.Eligible
                                ? referralEvaluation.RewardAmount
                                : null,
                            null,
                            null)
                        : null);
        }

        public async Task<SorteoReprogramacionResultado> RescheduleWithoutWinnerAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                await AcquireLotteryLockAsync(sorteoId, cancellationToken);
                var sorteo = await _context.Sorteos
                    .FromSqlInterpolated($"SELECT * FROM Sorteos WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sorteoId}")
                    .SingleOrDefaultAsync(cancellationToken);
                var stateValidation = ValidateLotteryStateForResult(sorteo);
                if (stateValidation != SorteoResultadoStatus.Ok)
                    return new(stateValidation, sorteoId, null, null, 0);

                var minimumSaleReached = await HasReachedMinimumSaleAsync(sorteo, cancellationToken);
                var technicalIncident = !await HasPublishedTransparencyAsync(sorteo, cancellationToken);
                string normalizedNumber = null;
                if (minimumSaleReached && !technicalIncident)
                {
                    var validation = ValidateLotteryForResult(sorteo, numeroGanador);
                    if (validation.Status != SorteoResultadoStatus.Ok)
                        return new(validation.Status, sorteoId, validation.Number, null, 0);
                    normalizedNumber = validation.Number;
                    if (await _context.BoletosConfirmados.AsNoTracking()
                        .AnyAsync(x => x.SorteosId == sorteoId && x.Numero == normalizedNumber, cancellationToken))
                        return new(SorteoResultadoStatus.HayGanador, sorteoId, normalizedNumber, null, 0);
                }

                var fechaOriginal = sorteo.Fecha;
                var originalInstant = _sorteoTimeService.ResolveScheduledTime(sorteo);
                var candidates = await _context.Sorteos.ToListAsync(cancellationToken);
                var sorteos = candidates
                    .Select(item => new
                    {
                        Sorteo = item,
                        ScheduledAt = _sorteoTimeService.ResolveScheduledTime(item)
                    })
                    .Where(item => item.ScheduledAt >= originalInstant)
                    .OrderBy(item => item.ScheduledAt)
                    .Select(item => item.Sorteo)
                    .ToList();
                foreach (var item in sorteos)
                    item.Fecha = item.Fecha.AddDays(_rescheduleDays);

                await _context.SorteosBoletos
                    .Where(x =>
                        x.SorteosId == sorteoId &&
                        !x.Pagado &&
                        !_context.BoletosConfirmados.Any(sold =>
                            sold.SorteosId == x.SorteosId &&
                            sold.Numero == x.Numero))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.FolioCompra, string.Empty)
                        .SetProperty(x => x.UsuarioId, 0)
                        .SetProperty(x => x.PreApartado, false)
                        .SetProperty(x => x.Apartado, false)
                        .SetProperty(x => x.Aviso, false),
                        cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(
                    SorteoResultadoStatus.Ok,
                    sorteoId,
                    normalizedNumber,
                    sorteo.Fecha,
                    sorteos.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<SorteoFinalizacionResultado> FinalizeWinnerAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                await AcquireLotteryLockAsync(sorteoId, cancellationToken);
                var sorteo = await _context.Sorteos
                    .FromSqlInterpolated($"SELECT * FROM Sorteos WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sorteoId}")
                    .SingleOrDefaultAsync(cancellationToken);
                var validation = ValidateLotteryForResult(sorteo, numeroGanador);
                if (validation.Status != SorteoResultadoStatus.Ok)
                    return new(validation.Status, sorteoId, validation.Number, 0, 0, 0, null);

                if (!await HasReachedMinimumSaleAsync(sorteo, cancellationToken))
                    return new(SorteoResultadoStatus.VentaMinimaNoAlcanzada, sorteoId, validation.Number, 0, 0, 0, null);

                if (!await HasPublishedTransparencyAsync(sorteo, cancellationToken))
                    return new(SorteoResultadoStatus.DocumentoTransparenciaNoPublicado, sorteoId, validation.Number, 0, 0, 0, null);

                var winner = await FindWinnerAsync(sorteoId, validation.Number, cancellationToken);
                if (winner == null)
                    return new(SorteoResultadoStatus.NumeroNoVendido, sorteoId, validation.Number, 0, 0, 0, null);

                var finalizedAt = _sorteoTimeService.ConvertToSorteoTime(
                    ApplicationClock.NowOffset, sorteo).DateTime;
                var winnerRecord = new GanadoresSorteos
                {
                    SorteoId = sorteo.Id,
                    NumeroGanador = validation.Number,
                    FolioSorteo = winner.FolioCompra,
                    Nombre = sorteo.Nombre,
                    FechaFin = finalizedAt,
                    WhatsAppGanador = winner.WhatsApp,
                    UsuarioIdGanador = winner.UsuarioId,
                    NombreGanador = winner.Usuario
                };
                await AttachReferralWinnerCashRewardAsync(
                    winnerRecord,
                    winner.UsuarioId,
                    sorteoId,
                    finalizedAt,
                    cancellationToken);

                var archivedPurchases = await _context.BoletosConfirmados
                    .AsNoTracking()
                    .Where(x => x.SorteosId == sorteoId)
                    .GroupBy(x => new { x.UsuarioId, x.FolioCompra })
                    .CountAsync(cancellationToken);
                await ArchiveConfirmedPurchasesAsync(sorteoId, finalizedAt, cancellationToken);

                var winningScratchcards = await _context.SorteosRascaditos
                    .AsNoTracking()
                    .Where(x =>
                        x.SorteosId == sorteoId &&
                        x.Revelado &&
                        x.EsGanador &&
                        x.FechaRevelado.HasValue &&
                        x.ImportePremio.HasValue &&
                        x.WalletTransactionPremioId.HasValue)
                    .Select(x => new SorteosRascaditosGanadoresHistorial
                    {
                        SorteoId = x.SorteosId,
                        UsuarioId = x.UsuarioId,
                        Folio = x.Folio,
                        ImportePremio = x.ImportePremio!.Value,
                        MatrizResultado = x.MatrizResultado,
                        LineaGanadora = x.LineaGanadora,
                        FechaGeneracion = x.FechaGeneracion,
                        FechaRevelado = x.FechaRevelado!.Value,
                        WalletTransactionOrigenId = x.WalletTransactionOrigenId,
                        WalletTransactionPremioId = x.WalletTransactionPremioId!.Value,
                        SorteoNombre = sorteo.Nombre,
                        SorteoImagen = sorteo.Imagen1,
                        FechaArchivado = finalizedAt
                    })
                    .ToListAsync(cancellationToken);
                _context.SorteosRascaditosGanadoresHistorial.AddRange(winningScratchcards);

                var expiredScratchcards = await _context.SorteosRascaditos
                    .CountAsync(
                        x => x.SorteosId == sorteoId && !x.Revelado,
                        cancellationToken);

                sorteo.NumeroGanador = validation.Number;
                sorteo.UsuarioIdGanador = winner.UsuarioId;
                sorteo.NombreGanador = winner.Usuario;
                _context.GanadoresSorteos.Add(winnerRecord);
                await _context.SaveChangesAsync(cancellationToken);

                await _context.SorteosRascaditos
                    .Where(x => x.SorteosId == sorteoId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.BoletosConfirmados
                    .Where(x => x.SorteosId == sorteoId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.SorteosBoletos
                    .Where(x => x.SorteosId == sorteoId)
                    .ExecuteDeleteAsync(cancellationToken);
                await ResetEmptyOperationalIdentitiesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                var finalizedWinner = await GetFinalizedWinnerAsync(sorteoId, cancellationToken);
                return new(
                    SorteoResultadoStatus.Ok,
                    sorteoId,
                    validation.Number,
                    archivedPurchases,
                    winningScratchcards.Count,
                    expiredScratchcards,
                    finalizedWinner);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<bool> HasReachedMinimumSaleAsync(
            Sorteos sorteo,
            CancellationToken cancellationToken)
        {
            if (sorteo == null || sorteo.CantidadBoletos <= 0) return false;
            var soldTickets = await _context.BoletosConfirmados.AsNoTracking()
                .CountAsync(x => x.SorteosId == sorteo.Id, cancellationToken);
            return soldTickets * 100m >= sorteo.CantidadBoletos * sorteo.PorcentajeMinimoVenta;
        }

        private async Task<bool> HasPublishedTransparencyAsync(Sorteos sorteo, CancellationToken cancellationToken)
        {
            var soldTickets = await _context.BoletosConfirmados.AsNoTracking()
                .CountAsync(x => x.SorteosId == sorteo.Id, cancellationToken);
            if (soldTickets == sorteo.CantidadBoletos && sorteo.CantidadBoletos > 0)
            {
                var numbers = await _context.BoletosConfirmados.AsNoTracking()
                    .Where(x => x.SorteosId == sorteo.Id)
                    .Select(x => x.Numero)
                    .ToListAsync(cancellationToken);
                try
                {
                    if (SorteoTransparencyNumbers.GetUnsold(sorteo.CantidadBoletos, numbers).Length == 0)
                        return true;
                }
                catch (InvalidOperationException) { }
            }
            try { return await _transparencyFiles.ReadAsync(sorteo, cancellationToken) != null; }
            catch (InvalidOperationException) { return false; }
        }

        public async Task<SorteoGanadorFinalizado> GetLatestFinalizedWinnerAsync(
            CancellationToken cancellationToken)
        {
            var winners = await _context.GanadoresSorteos
                .AsNoTracking()
                .Where(winner => winner.SorteoId.HasValue)
                .Select(winner => new
                {
                    winner.Id,
                    winner.SorteoId,
                    winner.FechaFin,
                    winner.Sorteo.ZonaHoraria
                })
                .ToListAsync(cancellationToken);
            var latest = winners
                .OrderByDescending(winner => _sorteoTimeService.ResolveRecordedTime(
                    winner.FechaFin,
                    new Sorteos { ZonaHoraria = winner.ZonaHoraria }))
                .ThenByDescending(winner => winner.Id)
                .FirstOrDefault();
            return latest != null
                ? await GetFinalizedWinnerAsync(latest.SorteoId!.Value, cancellationToken)
                : null;
        }

        private async Task<SorteoGanadorFinalizado> GetFinalizedWinnerAsync(
            int sorteoId,
            CancellationToken cancellationToken)
        {
            var winner = await _context.GanadoresSorteos
                .AsNoTracking()
                .Include(record => record.ReferralWinnerCashReward)
                    .ThenInclude(reward => reward.ReferrerUser)
                .Where(record => record.SorteoId == sorteoId)
                .OrderByDescending(record => record.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (winner == null) return null;

            var reward = winner.ReferralWinnerCashReward;
            SorteoGanadorReferencia referral = null;
            if (reward != null)
            {
                referral = new SorteoGanadorReferencia(
                    reward.ReferrerUser.Name,
                    true,
                    true,
                    reward.RequiredTickets,
                    reward.ActualTickets,
                    reward.RewardAmount,
                    reward.Status,
                    reward.PaidAt);
            }
            else
            {
                var referrer = await _context.Users
                    .AsNoTracking()
                    .Where(user => user.Id == winner.UsuarioIdGanador)
                    .Select(user => user.Referrer == null
                        ? null
                        : new { user.Referrer.Name })
                    .SingleOrDefaultAsync(cancellationToken);
                if (referrer != null)
                {
                    referral = new SorteoGanadorReferencia(
                        referrer.Name,
                        true,
                        false,
                        null,
                        null,
                        null,
                        null,
                        null);
                }
            }

            return new SorteoGanadorFinalizado(
                winner.SorteoId!.Value,
                winner.Nombre,
                winner.NumeroGanador,
                winner.NombreGanador,
                winner.FolioSorteo,
                winner.FechaFin,
                referral);
        }

        private async Task AttachReferralWinnerCashRewardAsync(
            GanadoresSorteos winnerRecord,
            int winnerUserId,
            int sorteoId,
            DateTime createdAt,
            CancellationToken cancellationToken)
        {
            var evaluation = await EvaluateReferralWinnerAsync(
                winnerUserId,
                sorteoId,
                cancellationToken);
            if (evaluation == null) return;
            if (!evaluation.SettingsValid)
                throw new InvalidOperationException(
                    "La configuración del programa de referidos no existe o es inválida.");
            if (!evaluation.Eligible) return;

            winnerRecord.ReferralWinnerCashReward = new ReferralWinnerCashReward
            {
                ReferrerUserId = evaluation.ReferrerUserId,
                RequiredTickets = evaluation.RequiredTickets!.Value,
                ActualTickets = evaluation.ActualTickets!.Value,
                RewardAmount = evaluation.RewardAmount!.Value,
                Status = ReferralWinnerCashRewardStatus.Paid,
                CreatedAt = createdAt,
                PaidAt = createdAt
            };
        }

        private async Task<ReferralWinnerEvaluation> EvaluateReferralWinnerAsync(
            int winnerUserId,
            int sorteoId,
            CancellationToken cancellationToken)
        {
            var referrer = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == winnerUserId)
                .Select(user => user.Referrer == null
                    ? null
                    : new { user.Referrer.Id, user.Referrer.Name })
                .SingleAsync(cancellationToken);
            if (referrer == null) return null;

            var settings = await _context.ReferralProgramSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            if (settings == null || !IsValidReferralProgramSettings(settings))
                return new ReferralWinnerEvaluation(
                    referrer.Id,
                    referrer.Name,
                    false,
                    false,
                    null,
                    null,
                    null);

            var actualTickets = await _context.BoletosConfirmados
                .AsNoTracking()
                .CountAsync(
                    ticket =>
                        ticket.SorteosId == sorteoId &&
                        ticket.UsuarioId == referrer.Id,
                    cancellationToken);
            var eligible = settings.IsActive &&
                settings.WinnerCashRewardAmount > 0m &&
                actualTickets >= settings.MinimumConfirmedTickets;
            return new ReferralWinnerEvaluation(
                referrer.Id,
                referrer.Name,
                true,
                eligible,
                settings.MinimumConfirmedTickets,
                actualTickets,
                settings.WinnerCashRewardAmount);
        }

        private static bool IsValidReferralProgramSettings(ReferralProgramSettings settings) =>
            settings.Id == 1 &&
            settings.DepositRewardPercentage is >= 0m and <= 100m &&
            settings.MaxRewardedDeposits >= 0 &&
            settings.WinnerCashRewardAmount >= 0m &&
            settings.MinimumConfirmedTickets >= 0 &&
            settings.RowVersion is { Length: 8 };

        private Task ArchiveConfirmedPurchasesAsync(
            int sorteoId,
            DateTime archivedAt,
            CancellationToken cancellationToken) =>
            _context.Database.ExecuteSqlInterpolatedAsync($@"
;WITH Purchases AS
(
    SELECT
        confirmed.SorteosId,
        confirmed.UsuarioId,
        confirmed.FolioCompra,
        '[' + STRING_AGG(
            CONVERT(nvarchar(max), CHAR(34) + STRING_ESCAPE(confirmed.Numero, 'json') + CHAR(34)),
            ',') WITHIN GROUP (ORDER BY confirmed.Numero) + ']' AS NumerosJson,
        COUNT(*) AS CantidadBoletos,
        MIN(confirmed.Fecha) AS FechaCompra,
        MAX(confirmed.WhatsAppConfirm) AS WhatsAppConfirm,
        MAX(confirmed.CuentaAsignada) AS CuentaAsignada
    FROM BoletosConfirmados confirmed WITH (UPDLOCK, HOLDLOCK)
    WHERE confirmed.SorteosId = {sorteoId}
    GROUP BY confirmed.SorteosId, confirmed.UsuarioId, confirmed.FolioCompra
)
INSERT INTO BoletosConfirmadosHistorial
(
    SorteoId, UsuarioId, FolioCompra, NumerosJson, CantidadBoletos,
    PrecioUnitario, ImporteTotal, FechaCompra, WhatsAppConfirm,
    CuentaAsignada, SorteoNombre, FechaSorteo, FechaArchivado
)
SELECT
    purchase.SorteosId,
    purchase.UsuarioId,
    purchase.FolioCompra,
    purchase.NumerosJson,
    purchase.CantidadBoletos,
    CASE WHEN purchase.CantidadBoletos >= 1000 THEN lottery.PrecioPorMil ELSE lottery.PrecioBoleto END,
    purchase.CantidadBoletos *
        CASE WHEN purchase.CantidadBoletos >= 1000 THEN lottery.PrecioPorMil ELSE lottery.PrecioBoleto END,
    purchase.FechaCompra,
    purchase.WhatsAppConfirm,
    purchase.CuentaAsignada,
    lottery.Nombre,
    lottery.Fecha,
    {archivedAt}
FROM Purchases purchase
INNER JOIN Sorteos lottery ON lottery.Id = purchase.SorteosId
WHERE NOT EXISTS
(
    SELECT 1
    FROM BoletosConfirmadosHistorial history WITH (UPDLOCK, HOLDLOCK)
    WHERE history.SorteoId = purchase.SorteosId
      AND history.UsuarioId = purchase.UsuarioId
      AND history.FolioCompra = purchase.FolioCompra
);", cancellationToken);

        private Task ResetEmptyOperationalIdentitiesAsync(CancellationToken cancellationToken) =>
            _context.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM SorteosRascaditos)
    DBCC CHECKIDENT ('SorteosRascaditos', RESEED, 0) WITH NO_INFOMSGS;
IF NOT EXISTS (SELECT 1 FROM BoletosConfirmados)
    DBCC CHECKIDENT ('BoletosConfirmados', RESEED, 0) WITH NO_INFOMSGS;
IF NOT EXISTS (SELECT 1 FROM SorteosBoletos)
    DBCC CHECKIDENT ('SorteosBoletos', RESEED, 0) WITH NO_INFOMSGS;", cancellationToken);

        private async Task AcquireLotteryLockAsync(int sorteoId, CancellationToken cancellationToken)
        {
            var resource = $"eLotto:Sorteo:{sorteoId}:Boletos";
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource = {resource},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 30000;
IF @result < 0
    THROW 51002, 'No fue posible bloquear el sorteo para reprogramarlo.', 1;", cancellationToken);
        }

        private Task<WinnerTicket> FindWinnerAsync(
            int sorteoId,
            string numeroGanador,
            CancellationToken cancellationToken) =>
            (from ticket in _context.BoletosConfirmados.AsNoTracking()
             join user in _context.Users.AsNoTracking() on ticket.UsuarioId equals user.Id
             where ticket.SorteosId == sorteoId && ticket.Numero == numeroGanador
             select new WinnerTicket(
                 ticket.UsuarioId,
                 user.User,
                 string.IsNullOrWhiteSpace(ticket.WhatsAppConfirm) ? user.WhatsApp : ticket.WhatsAppConfirm,
                 ticket.FolioCompra))
            .FirstOrDefaultAsync(cancellationToken);

        private static string MaskWhatsApp(string whatsApp)
        {
            var digits = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 13 && digits.StartsWith("521", StringComparison.Ordinal))
                digits = "52" + digits[3..];
            if (digits.Length <= 8)
            {
                var visibleDigits = Math.Min(3, digits.Length);
                return new string('*', digits.Length - visibleDigits) + digits[(digits.Length - visibleDigits)..];
            }

            return "+" + digits[..^8] + "*****" + digits[^3..];
        }

        private (SorteoResultadoStatus Status, string Number) ValidateLotteryForResult(
            Sorteos sorteo,
            string numeroGanador)
        {
            var stateValidation = ValidateLotteryStateForResult(sorteo);
            if (stateValidation != SorteoResultadoStatus.Ok)
                return (stateValidation, numeroGanador?.Trim());

            var value = numeroGanador?.Trim();
            var initialNumber = sorteo.CantidadBoletos == 60000 ? 1 : 0;
            var finalNumber = sorteo.CantidadBoletos == 60000
                ? sorteo.CantidadBoletos
                : sorteo.CantidadBoletos - 1;
            if (string.IsNullOrWhiteSpace(value) ||
                value.Any(character => !char.IsDigit(character)) ||
                !int.TryParse(value, out var numericValue) ||
                numericValue < initialNumber ||
                numericValue > finalNumber)
                return (SorteoResultadoStatus.NumeroInvalido, value);

            var width = sorteo.CantidadBoletos.ToString().Length;
            return (SorteoResultadoStatus.Ok, numericValue.ToString().PadLeft(width, '0'));
        }

        private SorteoResultadoStatus ValidateLotteryStateForResult(Sorteos sorteo)
        {
            if (sorteo == null) return SorteoResultadoStatus.SorteoNoEncontrado;
            if (!string.IsNullOrWhiteSpace(sorteo.NumeroGanador)) return SorteoResultadoStatus.SorteoFinalizado;
            if (ApplicationClock.NowOffset < _sorteoTimeService.ResolveScheduledTime(sorteo))
                return SorteoResultadoStatus.SorteoNoIniciado;
            return SorteoResultadoStatus.Ok;
        }

        private sealed record WinnerTicket(
            int UsuarioId,
            string Usuario,
            string WhatsApp,
            string FolioCompra);

        private sealed record ReferralWinnerEvaluation(
            int ReferrerUserId,
            string ReferrerName,
            bool SettingsValid,
            bool Eligible,
            int? RequiredTickets,
            int? ActualTickets,
            decimal? RewardAmount);
    }
}





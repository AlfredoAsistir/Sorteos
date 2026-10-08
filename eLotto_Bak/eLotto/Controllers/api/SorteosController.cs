using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Models;
using eLotto.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eLotto.Controllers.api
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("[controller]")]
    public class SorteosController : ControllerBase
    {
        private readonly ISorteosRepository _repository;
        private readonly ISorteoImageStorageService _images;
        private readonly ISorteoTransparencyService _transparency;

        public SorteosController(ISorteosRepository repository, ISorteoImageStorageService images,
            ISorteoTransparencyService transparency)
        {
            _repository = repository;
            _images = images;
            _transparency = transparency;
        }

        [HttpGet]
        public async Task<IActionResult> GetPage([FromQuery] int page = 1)
        {
            if (page < 1) return BadRequest();
            var result = await _repository.GetPageAsync(page);
            return Ok(new { sorteo = result.Sorteo, totalRecords = result.TotalRecords, page });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var sorteo = await _repository.GetByIdAsync(id);
            return sorteo == null ? NotFound() : Ok(sorteo);
        }

        [HttpPost]
        [RequestSizeLimit(16 * 1024 * 1024)]
        public async Task<IActionResult> Create([FromForm] SorteoFormDto form, CancellationToken cancellationToken)
        {
            if (form.Imagen1 == null) ModelState.AddModelError(nameof(form.Imagen1), "La imagen principal es requerida.");
            ValidateScratchcardState(form);
            ValidateImageThemes(form);
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var sorteo = Map(form);
            try
            {
                sorteo.Imagen1 = await _images.SaveAsync(form.Imagen1, cancellationToken);
                if (form.Imagen2 != null) sorteo.Imagen2 = await _images.SaveAsync(form.Imagen2, cancellationToken);
                if (form.Imagen3 != null) sorteo.Imagen3 = await _images.SaveAsync(form.Imagen3, cancellationToken);
                var created = await _repository.CreateAsync(sorteo);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("{id:int}")]
        [RequestSizeLimit(16 * 1024 * 1024)]
        public async Task<IActionResult> Update(int id, [FromForm] SorteoFormDto form, CancellationToken cancellationToken)
        {
            if (id != form.Id) return BadRequest();
            var current = await _repository.GetByIdAsync(id);
            if (current == null) return NotFound();
            ValidateScratchcardState(form, current);
            ValidateImageThemes(form, current);
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var sorteo = Map(form, current);
            sorteo.Imagen1 = current.Imagen1;
            sorteo.Imagen2 = current.Imagen2;
            sorteo.Imagen3 = current.Imagen3;
            sorteo.NumeroGanador = current.NumeroGanador;
            sorteo.UsuarioIdGanador = current.UsuarioIdGanador;
            sorteo.NombreGanador = current.NombreGanador;
            try
            {
                if (form.Imagen1 != null) { var old = sorteo.Imagen1; sorteo.Imagen1 = await _images.SaveAsync(form.Imagen1, cancellationToken); _images.Delete(old); }
                if (form.Imagen2 != null) { var old = sorteo.Imagen2; sorteo.Imagen2 = await _images.SaveAsync(form.Imagen2, cancellationToken); _images.Delete(old); }
                if (form.Imagen3 != null) { var old = sorteo.Imagen3; sorteo.Imagen3 = await _images.SaveAsync(form.Imagen3, cancellationToken); _images.Delete(old); }
                await _repository.UpdateAsync(sorteo);
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var sorteo = await _repository.GetByIdAsync(id);
            if (sorteo == null) return NotFound();
            try { if (!await _repository.DeleteAsync(id)) return NotFound(); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            _images.Delete(sorteo.Imagen1); _images.Delete(sorteo.Imagen2); _images.Delete(sorteo.Imagen3);
            return NoContent();
        }

        [HttpDelete("{id:int}/imagenes/{imageNumber:int}")]
        public async Task<IActionResult> DeleteOptionalImage(int id, int imageNumber)
        {
            if (imageNumber is not (2 or 3))
                return BadRequest(new { message = "Solo se pueden eliminar las imágenes opcionales 2 y 3." });

            var sorteo = await _repository.GetByIdAsync(id);
            if (sorteo == null) return NotFound();

            var imagePath = imageNumber == 2 ? sorteo.Imagen2 : sorteo.Imagen3;
            if (string.IsNullOrWhiteSpace(imagePath)) return NoContent();

            if (!await _repository.RemoveOptionalImageAsync(id, imageNumber)) return NotFound();
            _images.Delete(imagePath);
            return NoContent();
        }

        [HttpDelete("{id:int}/rascadito-premios/{prizeId:int}")]
        public async Task<IActionResult> DeleteScratchcardPrize(
            int id,
            int prizeId,
            CancellationToken cancellationToken)
        {
            if (id <= 0 || prizeId <= 0) return BadRequest();

            var result = await _repository.DeleteScratchcardPrizeAsync(id, prizeId, cancellationToken);
            if (result == SorteoRascaditoPremioDeleteResult.NotFound) return NotFound();
            if (result == SorteoRascaditoPremioDeleteResult.HasDeliveredPrizes)
                return BadRequest(new { message = "No se puede eliminar una configuración que ya tiene premios entregados." });

            return NoContent();
        }

        [HttpPost("{id:int}/resultado/verificar")]
        public async Task<IActionResult> VerifyWinningNumber(
            int id,
            [FromBody] VerificarNumeroGanadorRequest request,
            CancellationToken cancellationToken)
        {
            await _transparency.GetStatusAsync(id, cancellationToken);
            var result = await _repository.VerifyWinningNumberAsync(
                id,
                request.NumeroGanador,
                cancellationToken);
            var error = MapResultError(result.Status);
            if (error != null) return error;

            return Ok(new VerificarNumeroGanadorResponse(
                result.SorteoId,
                result.NumeroGanador,
                result.HayGanador,
                result.UsuarioIdGanador,
                result.UsuarioGanador,
                result.WhatsAppGanador,
                result.FolioCompra,
                MapReferral(result.Referencia)));
        }

        [HttpGet("resultado/ultimo-finalizado")]
        public async Task<IActionResult> GetLatestFinalizedWinner(
            CancellationToken cancellationToken)
        {
            var result = await _repository.GetLatestFinalizedWinnerAsync(cancellationToken);
            return result == null ? NotFound() : Ok(MapFinalizedWinner(result));
        }

        [HttpPost("{id:int}/resultado/reprogramar")]
        public async Task<IActionResult> RescheduleWithoutWinner(
            int id,
            [FromBody] ReprogramarSorteoRequest request,
            CancellationToken cancellationToken)
        {
            await _transparency.GetStatusAsync(id, cancellationToken);
            var result = await _repository.RescheduleWithoutWinnerAsync(
                id,
                request.NumeroGanador,
                cancellationToken);
            var error = MapResultError(result.Status);
            if (error != null) return error;

            return Ok(new ReprogramarSorteoResponse(
                result.SorteoId,
                result.NumeroGanador,
                result.NuevaFecha!.Value,
                result.SorteosReprogramados));
        }

        [HttpPost("{id:int}/resultado/finalizar")]
        public async Task<IActionResult> FinalizeWinner(
            int id,
            [FromBody] VerificarNumeroGanadorRequest request,
            CancellationToken cancellationToken)
        {
            await _transparency.GetStatusAsync(id, cancellationToken);
            var result = await _repository.FinalizeWinnerAsync(
                id,
                request.NumeroGanador,
                cancellationToken);
            var error = MapResultError(result.Status);
            if (error != null) return error;

            return Ok(new FinalizarSorteoResponse(
                result.SorteoId,
                result.NumeroGanador,
                result.ComprasArchivadas,
                result.RascaditosGanadoresArchivados,
                result.RascaditosCaducados,
                MapFinalizedWinner(result.Ganador)));
        }

        private static GanadorFinalizadoResponse MapFinalizedWinner(
            SorteoGanadorFinalizado winner) => winner == null
                ? null
                : new GanadorFinalizadoResponse(
                    winner.SorteoId,
                    winner.SorteoNombre,
                    winner.NumeroGanador,
                    winner.UsuarioGanador,
                    winner.FolioCompra,
                    winner.FechaFinalizacion,
                    MapReferral(winner.Referencia));

        private static ReferenciaGanadorResponse MapReferral(
            SorteoGanadorReferencia referral) => referral == null
                ? null
                : new ReferenciaGanadorResponse(
                    referral.ReferidorNombre,
                    referral.Finalizado,
                    referral.PremioGenerado,
                    referral.BoletosRequeridos,
                    referral.BoletosConfirmados,
                    referral.ImportePremio,
                    referral.Estado,
                    referral.FechaPago);

        private IActionResult MapResultError(SorteoResultadoStatus status) => status switch
        {
            SorteoResultadoStatus.SorteoNoEncontrado => NotFound(new { message = "El sorteo no existe." }),
            SorteoResultadoStatus.SorteoNoIniciado => Conflict(new { message = "El sorteo todavía no inicia; no es posible verificar el número ganador." }),
            SorteoResultadoStatus.SorteoFinalizado => Conflict(new { message = "El sorteo ya tiene un resultado final." }),
            SorteoResultadoStatus.NumeroInvalido => BadRequest(new { message = "El número ganador está fuera de la numeración del sorteo." }),
            SorteoResultadoStatus.HayGanador => Conflict(new { message = "El número ya pertenece a un boleto vendido. El sorteo no puede reprogramarse." }),
            SorteoResultadoStatus.NumeroNoVendido => Conflict(new { message = "El número no pertenece a un boleto vendido. El sorteo no puede finalizarse con ganador." }),
            SorteoResultadoStatus.VentaMinimaNoAlcanzada => Conflict(new { message = "No se alcanzó el porcentaje mínimo de venta. El sorteo debe reprogramarse." }),
            SorteoResultadoStatus.DocumentoTransparenciaNoPublicado => Conflict(new { message = "No fue posible publicar el documento de transparencia. El sorteo debe reprogramarse." }),
            _ => null
        };

        private void ValidateScratchcardState(SorteoFormDto form, Sorteos current = null)
        {
            var submittedPrizes = form.RascaditoPremios ?? new List<SorteoRascaditoPremioFormDto>();
            if (!form.RascaditosHabilitados)
            {
                if (current?.RascaditoPremios.Any(x => x.Entregados > 0) == true)
                    ModelState.AddModelError(
                        nameof(form.RascaditosHabilitados),
                        "No se pueden deshabilitar los rascaditos porque existen premios entregados.");
                return;
            }

            var duplicateIds = submittedPrizes
                .Where(x => x.Id > 0)
                .GroupBy(x => x.Id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateId in duplicateIds)
                ModelState.AddModelError(
                    nameof(form.RascaditoPremios),
                    $"El premio con Id {duplicateId} está repetido.");

            for (var index = 0; index < submittedPrizes.Count; index++)
            {
                var submittedPrize = submittedPrizes[index];
                var field = $"{nameof(form.RascaditoPremios)}[{index}]";
                if (submittedPrize.Id < 0)
                {
                    ModelState.AddModelError($"{field}.{nameof(submittedPrize.Id)}", "El Id del premio no es válido.");
                    continue;
                }

                var currentPrize = current?.RascaditoPremios.SingleOrDefault(x => x.Id == submittedPrize.Id);
                if (current == null && submittedPrize.Id != 0 ||
                    current != null && submittedPrize.Id > 0 && currentPrize == null)
                {
                    ModelState.AddModelError(
                        $"{field}.{nameof(submittedPrize.Id)}",
                        "El premio no pertenece al sorteo.");
                    continue;
                }

                var delivered = currentPrize?.Entregados ?? 0;
                if (submittedPrize.Entregados != delivered)
                    ModelState.AddModelError(
                        $"{field}.{nameof(submittedPrize.Entregados)}",
                        "La cantidad de premios entregados no puede modificarse desde esta operación.");

                if (submittedPrize.Cantidad < delivered)
                    ModelState.AddModelError(
                        $"{field}.{nameof(submittedPrize.Cantidad)}",
                        "La cantidad no puede ser menor que los premios ya entregados.");
            }

            if (current == null) return;

            var submittedIds = submittedPrizes.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
            if (current.RascaditoPremios.Any(x => x.Entregados > 0 && !submittedIds.Contains(x.Id)))
                ModelState.AddModelError(
                    nameof(form.RascaditoPremios),
                    "No se puede eliminar una configuración que ya tiene premios entregados.");
        }

        private void ValidateImageThemes(SorteoFormDto form, Sorteos current = null)
        {
            ValidateImageTheme(form.Imagen2Tema, form.Imagen2 != null || current?.Imagen2 != null,
                form.Imagen2 != null, nameof(form.Imagen2Tema));
            ValidateImageTheme(form.Imagen3Tema, form.Imagen3 != null || current?.Imagen3 != null,
                form.Imagen3 != null, nameof(form.Imagen3Tema));
        }

        private void ValidateImageTheme(string theme, bool hasImage, bool hasNewImage, string field)
        {
            if (!string.IsNullOrEmpty(theme) && theme is not ("rascaditos" or "referidos"))
                ModelState.AddModelError(field, "Selecciona Rascaditos o Referidos de la lista.");
            else if (!hasImage && !string.IsNullOrEmpty(theme))
                ModelState.AddModelError(field, "Agrega una imagen antes de seleccionar su tema.");
            else if (hasNewImage && string.IsNullOrEmpty(theme))
                ModelState.AddModelError(field, "Selecciona el tema de la imagen.");
        }

        private static Sorteos Map(SorteoFormDto form, Sorteos current = null)
        {
            var scratchcardsEnabled = form.RascaditosHabilitados;
            var sorteo = new Sorteos
            {
                Id = form.Id,
                Nombre = form.Nombre.Trim(),
                Imagen2Tema = form.Imagen2 != null || current?.Imagen2 != null ? form.Imagen2Tema : null,
                Imagen3Tema = form.Imagen3 != null || current?.Imagen3 != null ? form.Imagen3Tema : null,
                Fecha = DateTime.SpecifyKind(form.Fecha, DateTimeKind.Unspecified),
                ZonaHoraria = form.ZonaHoraria,
                UrlTransmisionEnVivo = string.IsNullOrWhiteSpace(form.UrlTransmisionEnVivo)
                    ? null
                    : form.UrlTransmisionEnVivo.Trim(),
                PrecioBoleto = form.PrecioBoleto,
                PrecioPorMil = form.PrecioPorMil,
                CantidadBoletos = form.CantidadBoletos,
                PorcentajeMinimoVenta = form.PorcentajeMinimoVenta,
                RascaditosHabilitados = scratchcardsEnabled,
                GanadoresPorGrupo = scratchcardsEnabled ? form.GanadoresPorGrupo : 0,
                RascaditosPorGrupo = scratchcardsEnabled ? form.RascaditosPorGrupo : 0,
                ImporteDepositoStripePorRascadito = scratchcardsEnabled ? form.ImporteDepositoStripePorRascadito : 0,
                RascaditoPremios = scratchcardsEnabled
                    ? (form.RascaditoPremios ?? new List<SorteoRascaditoPremioFormDto>())
                        .Select(premio =>
                        {
                            var currentPrize = current?.RascaditoPremios.SingleOrDefault(x => x.Id == premio.Id);
                            return new SorteosRascaditoPremios
                            {
                                Id = premio.Id,
                                SorteosId = form.Id,
                                Premio = premio.Premio,
                                Cantidad = premio.Cantidad,
                                Entregados = currentPrize?.Entregados ?? 0
                            };
                        })
                        .ToList()
                    : new List<SorteosRascaditoPremios>()
            };
            return sorteo;
        }
    }
}


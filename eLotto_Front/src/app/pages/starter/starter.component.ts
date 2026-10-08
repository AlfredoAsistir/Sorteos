import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, ViewEncapsulation } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MaterialModule } from '../../material.module';
import { NumericInputComponent } from '../../components/numeric-input/numeric-input.component';
import { APP_BRANDING } from '../../config/branding.config';
import { NotificationService } from '../../core/notifications/notification.service';
import { SessionService } from '../../core/auth/session.service';
import { CurrentLottery, PreReserveResponse, RandomTicketType, TicketAvailability, TicketPurchaseResponse } from '../../core/user-lottery/user-lottery.models';
import { UserLotteryService } from '../../services/user-lottery.service';
import { LoadingService } from '../../services/loading.service';
import { WalletService } from '../../services/wallet.service';
import { ScratchcardsPanelComponent } from '../../components/scratchcards-panel/scratchcards-panel.component';
import { SorteoStatusComponent } from '../../components/sorteo-status/sorteo-status.component';
import { LotteryDateStripComponent } from '../../components/lottery-date-strip/lottery-date-strip.component';

@Component({
  selector: 'app-starter',
  imports: [CommonModule, ReactiveFormsModule, RouterLink, MaterialModule, NumericInputComponent, ScratchcardsPanelComponent, SorteoStatusComponent, LotteryDateStripComponent],
  templateUrl: './starter.component.html',
  styleUrl: './starter.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class StarterComponent implements OnInit, OnDestroy {
  private static readonly CarouselIntervalMs = 10_000;
  private static readonly CarouselReadingExtensionMs = 30_000;
  readonly appName = APP_BRANDING.name;
  readonly randomForm = this.fb.nonNullable.group({ tipo: this.fb.nonNullable.control<RandomTicketType>('azar'), valor: [''], cantidad: [300, [Validators.required, Validators.min(1), Validators.max(2000)]] });
  readonly manualForm = this.fb.nonNullable.group({ numeros: ['', Validators.required] });
  lottery: CurrentLottery | null = null;
  tickets: TicketAvailability[] = [];
  selectedTab = 0;
  onlyAvailable = false;
  loading = false;
  hasAvailableScratchcards: boolean | null = null;
  carouselIndex = 0;
  previousCarouselIndex: number | null = null;
  carouselDirection: 'left-to-right' | 'right-to-left' = 'left-to-right';
  folioCompra: string | null = null;
  manualQueryFolio: string | null = null;
  selectAllChecked = false;
  remainingSeconds = 0;
  lotteryCountdownSeconds = 0;
  private timerId: number | null = null;
  private lotteryStatusTimerId: number | null = null;
  private lotteryTargetTime = 0;
  private lotterySalesCloseTargetTime = 0;
  private lastBlockedLotteryRefreshAt = 0;
  private blockedLotteryRefreshInProgress = false;
  private carouselTimerId: number | null = null;
  private carouselTransitionTimerId: number | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly service: UserLotteryService,
    private readonly walletService: WalletService,
    private readonly loadingService: LoadingService,
    private readonly session: SessionService,
    private readonly notifications: NotificationService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  get isUser(): boolean { return this.session.roleName === 'User'; }
  get soldOut(): boolean {
    return !!this.lottery && this.lottery.cantidadBoletos > 0 &&
      this.lottery.boletosVendidos >= this.lottery.cantidadBoletos;
  }
  get hasRemainingWalletBalance(): boolean {
    return this.walletService.walletLoaded() && this.walletService.balance() > 0;
  }
  get remainingWalletBalanceFormatted(): string {
    return this.walletService.formatAmount(this.walletService.balance());
  }
  get carouselSlides(): Array<{ url: string; topic: 'rascaditos' | 'referidos' | null }> {
    if (!this.lottery) return [];
    return [
      { path: this.lottery.imagen1, topic: null },
      { path: this.lottery.imagen2, topic: this.lottery.imagen2Tema ?? null },
      { path: this.lottery.imagen3, topic: this.lottery.imagen3Tema ?? null },
    ].flatMap(slide => slide.path ? [{ url: this.service.imageUrl(slide.path), topic: slide.topic }] : []);
  }
  get images(): string[] { return this.carouselSlides.map(slide => slide.url); }
  get carouselAction(): { label: string; path: string; fragment?: string } | null {
    const topic = this.carouselSlides[this.carouselIndex]?.topic;
    if (topic === 'rascaditos')
      return { label: 'Cómo funcionan', path: '/starter/preguntas-frecuentes', fragment: 'rascaditos' };
    if (topic === 'referidos')
      return { label: 'Invitar amigos', path: '/starter/invitar-amigo' };
    return null;
  }
  get displayedTickets(): TicketAvailability[] { return this.onlyAvailable ? this.tickets.filter(x => x.disponible) : this.tickets; }
  get selectedTickets(): TicketAvailability[] { return this.tickets.filter(x => x.seleccionado); }
  get availableCount(): number { return this.tickets.filter(x => x.disponible).length; }
  get usesBulkPrice(): boolean { return this.selectedTickets.length >= 1000; }
  get selectedUnitPrice(): number {
    if (!this.lottery) return 0;
    return this.usesBulkPrice ? this.lottery.precioPorMil : this.lottery.precioBoleto;
  }
  get selectedTotal(): number { return this.selectedTickets.length * this.selectedUnitPrice; }
  get displayFolio(): string | null { return this.folioCompra ?? this.manualQueryFolio; }
  get countdown(): string { return `${String(Math.floor(this.remainingSeconds / 60)).padStart(2, '0')}:${String(this.remainingSeconds % 60).padStart(2, '0')}`; }

  ngOnInit(): void { if (this.isUser) this.loadCurrent(); }
  ngOnDestroy(): void { this.stopTimer(); this.stopLotteryStatusTimer(); this.stopCarousel(); this.stopCarouselTransition(); if (this.folioCompra) this.service.release().subscribe({ error: () => undefined }); }

  async loadCurrent(): Promise<void> {
    this.loading = true;
    try {
      this.lottery = await firstValueFrom(this.service.getCurrent());
      this.startLotteryStatusTimer();
      this.startCarousel();
    }
    catch (error) {  }
    finally { this.loading = false; this.changeDetector.detectChanges(); }
  }

  previousImage(): void {
    if (this.images.length) this.transitionTo((this.carouselIndex - 1 + this.images.length) % this.images.length, 'right-to-left');
    this.startCarousel();
  }
  nextImage(): void {
    this.advanceCarousel();
    this.startCarousel();
  }
  selectImage(index: number): void {
    this.transitionTo(index, index < this.carouselIndex ? 'right-to-left' : 'left-to-right');
    this.startCarousel();
  }

  extendCarouselReadingTime(): void {
    this.startCarousel(StarterComponent.CarouselIntervalMs + StarterComponent.CarouselReadingExtensionMs);
  }

  scrollToTicketGenerator(): void {
    if (!this.ensureSalesOpen()) return;
    this.selectedTab = 0;
    this.changeDetector.detectChanges();
    window.setTimeout(() => {
      const generateButton = document.getElementById('generate-numbers-button');
      if (!generateButton) return;

      generateButton.scrollIntoView({
        behavior: 'smooth',
        block: 'center',
        inline: 'nearest'
      });
    }, 0);
  }

  async generateRandom(): Promise<void> {
    if (!this.ensureSalesOpen()) return;
    if (this.randomForm.controls.cantidad.hasError('max')) {
      this.randomForm.markAllAsTouched();
      this.notifications.show(
        'Límite de boletos',
        'Puedes generar un máximo de 2,000 boletos por operación.',
        'warning'
      );
      return;
    }
    if (this.randomForm.invalid) { this.randomForm.markAllAsTouched(); return; }
    const { cantidad, tipo, valor } = this.randomForm.getRawValue();
    if (tipo !== 'azar' && !valor.trim()) { this.notifications.show('Dato requerido', 'Captura el número para aplicar el filtro.', 'warning'); return; }
    const startedAt = performance.now();
    this.prepareForTicketRequest();
    this.loading = true;
    this.loadingService.show(true);
    this.changeDetector.detectChanges();
    try {
      const result = await firstValueFrom(this.service.preReserveRandom(cantidad, tipo, valor));
      this.applyReservation(result);
      this.scrollToTicketGrid();
    } catch (error) { this.notifications.show('No fue posible generar boletos', this.errorMessage(error), 'warning'); }
    finally {
      await this.waitForMinimumDuration(startedAt, 4000);
      this.loadingService.hide();
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }

  private scrollToTicketGrid(): void {
    window.setTimeout(() => {
      const ticketGrid = document.getElementById('ticket-results-grid');
      if (!ticketGrid) return;

      ticketGrid.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
    }, 300);
  }

  async queryManual(): Promise<void> {
    if (!this.ensureSalesOpen()) return;
    if (this.manualForm.invalid) { this.manualForm.markAllAsTouched(); return; }
    const startedAt = performance.now();
    this.prepareForTicketRequest();
    this.loading = true;
    this.loadingService.show(true);
    this.changeDetector.detectChanges();
    try {
      const result = await firstValueFrom(this.service.queryManual(this.manualForm.controls.numeros.value));
      this.tickets = result.boletos.map(ticket => ({ ...ticket, seleccionado: false }));
      this.manualQueryFolio = result.folioCompra;
      this.changeDetector.detectChanges();
      this.scrollToTicketGrid();
    } catch (error) { this.notifications.show('Consulta inválida', this.errorMessage(error), 'warning'); }
    finally {
      await this.waitForMinimumDuration(startedAt, 4000);
      this.loadingService.hide();
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }

  toggle(ticket: TicketAvailability): void {
    if (!ticket.disponible) return;
    ticket.seleccionado = !ticket.seleccionado;
    this.selectAllChecked = this.tickets.filter(item => item.disponible).every(item => item.seleccionado);
  }

  selectAll(checked: boolean): void {
    this.selectAllChecked = checked;
    this.tickets = this.tickets.map(ticket => ({ ...ticket, seleccionado: ticket.disponible && checked }));
  }

  async purchaseSelectedTickets(): Promise<void> {
    if (this.loading || !this.ensureSalesOpen()) return;
    const numbers = this.selectedTickets.map(ticket => ticket.numero);
    if (!numbers.length) {
      this.notifications.show('Sin selección', 'Selecciona al menos un boleto para continuar con la compra.', 'warning');
      return;
    }

    this.loading = true;
    this.loadingService.show();
    this.changeDetector.detectChanges();
    try {
      if (this.selectedTab === 0 || this.folioCompra) {
        await this.confirmPurchase();
        return;
      }

      await this.preReserveManual();
      if (this.folioCompra) await this.confirmPurchase();
    } finally {
      this.loadingService.hide();
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }
  async preReserveManual(): Promise<void> {
    const numbers = this.selectedTickets.map(x => x.numero);
    if (!numbers.length) { this.notifications.show('Sin selección', 'Selecciona al menos un boleto disponible.', 'warning'); return; }
    if (!this.manualQueryFolio) { this.notifications.show('Consulta requerida', 'Consulta nuevamente los boletos para obtener un folio de compra.', 'warning'); return; }
    try { this.applyReservation(await firstValueFrom(this.service.preReserveManual(numbers, this.manualQueryFolio))); }
    catch (error) {
      const unavailableTickets = error instanceof HttpErrorResponse && Array.isArray(error.error?.noDisponibles)
        ? error.error.noDisponibles as string[]
        : [];
      if (unavailableTickets.length) {
        this.showUnavailableTickets(unavailableTickets);
      } else {
        this.notifications.show('Alguien fue más rápido', this.errorMessage(error), 'warning');
      }
      await this.queryManual();
    }
  }

  async clearSelection(): Promise<void> {
    if (!this.folioCompra) {
      this.resetSelection();
      return;
    }

    this.loading = true;
    this.loadingService.show();
    try {
      await firstValueFrom(this.service.release());
      this.resetSelection();
    } catch (error) {
      this.notifications.show('Error', this.errorMessage(error), 'error');
    } finally {
      this.loadingService.hide();
      this.loading = false;
      this.changeDetector.detectChanges();
    }
  }

  async confirmPurchase(): Promise<void> {
    const lottery = this.lottery;
    const folio = this.folioCompra;
    const numbers = this.selectedTickets.map(ticket => ticket.numero);
    if (!lottery || !folio || !numbers.length) return;

    try {
      const result = await firstValueFrom(this.service.purchase(lottery.id, folio, numbers));
      this.updateSalesProgress(result.numeros.length);
      this.resetSelection();
      this.walletService.getWallet().subscribe({ error: () => undefined });
      this.notifications.show(
        '¡Compra realizada!',
        result.numeros.length + ' boletos confirmados. ¡Mucha suerte!',
        'success'
      );
    } catch (error) {
      const purchaseError = this.purchaseError(error);
      if (purchaseError?.codigo === 'tickets_unavailable') {
        try {
          await firstValueFrom(this.service.release());
        } catch {
          // La compra ya fue revertida en backend.
        }
        this.resetSelection();
        this.showUnavailableTickets(purchaseError.noDisponibles);
      } else if (purchaseError?.codigo === 'insufficient_balance') {
        try {
          await firstValueFrom(this.walletService.getWallet(true));
        } catch {
          this.walletService.setBalanceFromServer(purchaseError.saldo);
        }
        const missing = Math.max(0, purchaseError.total - this.walletService.balance());
        this.changeDetector.detectChanges();
        await new Promise<void>(resolve => window.requestAnimationFrame(() => resolve()));
        this.notifications.show(
          'Saldo insuficiente',
          'Te faltan ' + this.walletService.formatAmount(missing) + '. Deposita saldo y no pierdas la oportunidad de ganar.',
          'warning'
        );
      } else {
        this.notifications.show('No fue posible comprar', this.errorMessage(error), 'error');
      }
    }
  }

  updateScratchcardPool(prizeAwarded: number): void {
    if (!this.lottery || prizeAwarded <= 0) return;
    this.lottery.bolsaRascaditosDisponible = Math.max(
      0,
      this.lottery.bolsaRascaditosDisponible - prizeAwarded
    );
    this.changeDetector.detectChanges();
  }
  updateScratchcardAvailability(hasAvailableScratchcards: boolean): void {
    this.hasAvailableScratchcards = hasAvailableScratchcards;
    this.changeDetector.detectChanges();
  }  private updateSalesProgress(purchasedTickets: number): void {
    if (!this.lottery || purchasedTickets <= 0) return;
    this.lottery.boletosVendidos = Math.min(
      this.lottery.cantidadBoletos,
      this.lottery.boletosVendidos + purchasedTickets
    );
    this.lottery.porcentajeVenta = Math.min(
      100,
      Math.round(this.lottery.boletosVendidos * 1000 / this.lottery.cantidadBoletos) / 10
    );
    this.lottery.ventaMinimaAlcanzada =
      this.lottery.boletosVendidos * 100 >= this.lottery.cantidadBoletos * this.lottery.porcentajeMinimoVenta;
  }
  changeTab(index: number): void { this.selectedTab = index; void this.clearSelection(); }

  private startCarousel(delayMs = StarterComponent.CarouselIntervalMs): void {
    this.stopCarousel();
    if (this.images.length < 2) return;
    this.carouselTimerId = window.setTimeout(() => {
      this.carouselTimerId = null;
      this.advanceCarousel();
      this.startCarousel();
    }, delayMs);
  }

  private advanceCarousel(): void {
    if (this.images.length < 2) return;
    this.transitionTo((this.carouselIndex + 1) % this.images.length, 'left-to-right');
  }

  private transitionTo(index: number, direction: 'left-to-right' | 'right-to-left'): void {
    if (index === this.carouselIndex || !this.images[index]) return;
    this.stopCarouselTransition();
    this.previousCarouselIndex = this.carouselIndex;
    this.carouselDirection = direction;
    this.carouselIndex = index;
    this.changeDetector.detectChanges();
    this.carouselTransitionTimerId = window.setTimeout(() => {
      this.previousCarouselIndex = null;
      this.carouselTransitionTimerId = null;
      this.changeDetector.detectChanges();
    }, 650);
  }

  private stopCarousel(): void {
    if (this.carouselTimerId !== null) window.clearTimeout(this.carouselTimerId);
    this.carouselTimerId = null;
  }

  private stopCarouselTransition(): void {
    if (this.carouselTransitionTimerId !== null) window.clearTimeout(this.carouselTransitionTimerId);
    this.carouselTransitionTimerId = null;
    this.previousCarouselIndex = null;
  }

  private startLotteryStatusTimer(): void {
    this.stopLotteryStatusTimer();
    if (!this.lottery) return;
    this.lotteryTargetTime = performance.now() + Math.max(0, this.lottery.segundosParaInicio) * 1000;
    this.lotterySalesCloseTargetTime = performance.now() + Math.max(0, this.lottery.segundosParaCierreVentas) * 1000;
    this.lastBlockedLotteryRefreshAt = performance.now();
    const tick = () => {
      if (!this.lottery) return;
      const salesWereOpen = this.lottery.ventaDisponible;
      this.lotteryCountdownSeconds = Math.max(
        0,
        Math.ceil((this.lotteryTargetTime - performance.now()) / 1000)
      );
      this.lottery.segundosParaInicio = this.lotteryCountdownSeconds;
      this.lottery.segundosParaCierreVentas = Math.max(0, Math.ceil((this.lotterySalesCloseTargetTime - performance.now()) / 1000));
      if (this.lotteryCountdownSeconds === 0) {
        this.lottery.estado = 'en_proceso';
        this.lottery.ventaDisponible = false;
      } else if (this.lottery.segundosParaCierreVentas <= 0) {
        this.lottery.estado = 'proximo_a_iniciar';
        this.lottery.ventaDisponible = false;
      }
      if (salesWereOpen && !this.lottery.ventaDisponible) {
        const hadReservation = !!this.folioCompra;
        if (hadReservation) this.service.release().subscribe({ error: () => undefined });
        this.resetSelection();
        this.notifications.show(
          'Venta cerrada',
          'El sorteo está próximo a iniciar. Las compras y los depósitos quedaron temporalmente cerrados.',
          'info'
        );
      }
      if (
        !this.lottery.ventaDisponible &&
        performance.now() - this.lastBlockedLotteryRefreshAt >= 15_000
      ) {
        this.lastBlockedLotteryRefreshAt = performance.now();
        void this.refreshBlockedLottery();
      }
      this.changeDetector.detectChanges();
    };
    tick();
    this.lotteryStatusTimerId = window.setInterval(tick, 1000);
  }

  private stopLotteryStatusTimer(): void {
    if (this.lotteryStatusTimerId !== null) window.clearInterval(this.lotteryStatusTimerId);
    this.lotteryStatusTimerId = null;
  }

  private async refreshBlockedLottery(): Promise<void> {
    if (this.blockedLotteryRefreshInProgress || !this.lottery || this.lottery.ventaDisponible) return;
    this.blockedLotteryRefreshInProgress = true;
    try {
      const refreshed = await firstValueFrom(this.service.getCurrent());
      const changed =
        refreshed.id !== this.lottery.id ||
        refreshed.fecha !== this.lottery.fecha ||
        refreshed.estado !== this.lottery.estado ||
        refreshed.ventaDisponible !== this.lottery.ventaDisponible ||
        refreshed.boletosVendidos !== this.lottery.boletosVendidos ||
        refreshed.porcentajeVenta !== this.lottery.porcentajeVenta ||
        refreshed.ventaMinimaAlcanzada !== this.lottery.ventaMinimaAlcanzada ||
        refreshed.porcentajeMinimoVenta !== this.lottery.porcentajeMinimoVenta ||
        refreshed.urlTransmisionEnVivo !== this.lottery.urlTransmisionEnVivo;
      if (!changed) return;

      this.lottery = refreshed;
      this.hasAvailableScratchcards = null;
      this.startLotteryStatusTimer();
      this.startCarousel();
      this.changeDetector.detectChanges();
    } catch {
      // El sondeo es silencioso; el estado visible se conserva hasta la siguiente consulta.
    } finally {
      this.blockedLotteryRefreshInProgress = false;
    }
  }

  private ensureSalesOpen(): boolean {
    if (this.soldOut) {
      this.notifications.show('Boletos agotados', 'Se vendió el 100% de los boletos de este sorteo.', 'info');
      return false;
    }
    if (this.lottery?.ventaDisponible) return true;
    this.notifications.show(
      'Sorteo no disponible',
      'La venta está cerrada porque el sorteo está próximo a iniciar o se encuentra en proceso.',
      'info'
    );
    return false;
  }
  private applyReservation(result: PreReserveResponse): void {
    this.folioCompra = result.folioCompra;
    this.manualQueryFolio = null;
    this.selectAllChecked = false;
    this.tickets = result.numeros.map(numero => ({ numero, disponible: true, seleccionado: true }));
    this.startTimer(result.segundosParaExpirar);
    this.changeDetector.detectChanges();
  }

  private startTimer(seconds: number): void {
    this.stopTimer();
    if (seconds <= 0) return;
    const deadline = performance.now() + seconds * 1000;
    const tick = () => {
      this.remainingSeconds = Math.max(0, Math.ceil((deadline - performance.now()) / 1000));
      if (this.remainingSeconds === 0) {
        this.stopTimer(); this.folioCompra = null; this.manualQueryFolio = null; this.selectAllChecked = false; this.tickets = [];
        this.notifications.show('Tiempo agotado', 'Los boletos preapartados fueron liberados. Inténtalo nuevamente.', 'warning');
      }
      this.changeDetector.detectChanges();
    };
    tick(); this.timerId = window.setInterval(tick, 1000);
  }

  private prepareForTicketRequest(): void {
    this.stopTimer();
    this.tickets = [];
    this.folioCompra = null;
    this.manualQueryFolio = null;
    this.selectAllChecked = false;
    this.onlyAvailable = false;
  }

  private resetSelection(): void { this.stopTimer(); this.folioCompra = null; this.manualQueryFolio = null; this.selectAllChecked = false; this.tickets = []; this.onlyAvailable = false; this.changeDetector.detectChanges(); }
  private async waitForMinimumDuration(startedAt: number, minimumMilliseconds: number): Promise<void> {
    const remaining = minimumMilliseconds - (performance.now() - startedAt);
    if (remaining > 0) await new Promise<void>(resolve => window.setTimeout(resolve, remaining));
  }
  private stopTimer(): void { if (this.timerId !== null) window.clearInterval(this.timerId); this.timerId = null; this.remainingSeconds = 0; }
  private showUnavailableTickets(numbers: string[]): void {
    const visibleNumbers = numbers.slice(0, 20);
    this.notifications.show(
      'Alguien fue más rápido',
      '',
      'warning',
      {
        highlightedMessage: 'Algunos boletos dejaron de estar disponibles',
        detailLabel: 'No disponibles',
        detailText: visibleNumbers.join(', '),
      }
    );
  }
  private errorMessage(error: unknown): string { return error instanceof HttpErrorResponse ? error.error?.message ?? error.error?.mensaje ?? 'Ocurrió un error al procesar la solicitud.' : 'Ocurrió un error al procesar la solicitud.'; }
  private purchaseError(error: unknown): TicketPurchaseResponse | null { return error instanceof HttpErrorResponse && error.error ? error.error as TicketPurchaseResponse : null; }
}



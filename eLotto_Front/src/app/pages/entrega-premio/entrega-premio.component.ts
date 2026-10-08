import { CommonModule } from '@angular/common';
import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { FormNavigationDirective } from '../../core/forms/form-navigation.directive';
import { NotificationService } from '../../core/notifications/notification.service';
import { SorteoWinnerVerification } from '../../core/sorteos/sorteo.models';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { MaterialModule } from '../../material.module';
import { LoadingService } from '../../services/loading.service';
import { SorteosService } from '../../services/sorteos.service';
import { UserLotteryService } from '../../services/user-lottery.service';

@Component({
  selector: 'app-entrega-premio',
  standalone: true,
  imports: [CommonModule, ServerLocalDatePipe, ReactiveFormsModule, MaterialModule, FormNavigationDirective],
  templateUrl: './entrega-premio.component.html',
  styleUrl: './entrega-premio.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntregaPremioComponent implements OnInit, OnDestroy {
  readonly confettiPieces = Array.from({ length: 48 }, (_, index) => ({
    left: 20 + ((index * 37) % 61),
    delay: (index * 83) % 900,
    duration: 3000 + ((index * 137) % 1500),
    drift: ((index * 29) % 180) - 90,
    rotation: 360 + ((index * 47) % 540),
    color: ['#f6c945', '#e53935', '#1e88e5', '#43a047', '#8e24aa'][index % 5],
  }));
  readonly cannonConfetti = Array.from({ length: 40 }, (_, index) => {
    const launchesFromLeft = index % 2 === 0;
    const variation = ((index * 43) % 190) - 95;
    const travelX = launchesFromLeft ? 35 + variation : -35 - variation;
    const lift = -(300 + ((index * 31) % 230));
    const rotation = 420 + ((index * 53) % 620);
    return {
      origin: launchesFromLeft ? 34 : 66,
      travelX,
      travelEndX: Math.round(travelX * 1.35),
      lift,
      finalLift: lift + 180,
      delay: 850 + ((index * 71) % 650),
      rotation,
      finalRotation: Math.round(rotation * 1.4),
      color: ['#ffca28', '#ef5350', '#42a5f5', '#66bb6a', '#ab47bc'][index % 5],
    };
  });
  readonly fireworkSparks = Array.from({ length: 12 }, (_, index) => index);
  readonly form = this.fb.nonNullable.group({
    numeroGanador: ['', [Validators.required, Validators.pattern(/^\d+$/)]],
  });

  sorteo: CurrentLottery | null = null;
  loading = false;
  processing = false;
  verification: SorteoWinnerVerification | null = null;
  minimumSaleOutcomeVisible = false;
  showCelebration = false;
  private statusTimerId: number | null = null;
  private celebrationTimeoutId: number | null = null;
  private celebrationAudio: AudioContext | null = null;

  constructor(
    private readonly fb: FormBuilder,
    readonly lotteryService: UserLotteryService,
    private readonly sorteosService: SorteosService,
    private readonly notifications: NotificationService,
    private readonly globalLoading: LoadingService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.statusTimerId !== null) window.clearInterval(this.statusTimerId);
    if (this.celebrationTimeoutId !== null) window.clearTimeout(this.celebrationTimeoutId);
    void this.celebrationAudio?.close();
  }

  get ticketDigits(): number {
    return Math.max(1, String(this.sorteo?.cantidadBoletos ?? 1).length);
  }

  get sorteoTerminado(): boolean {
    return this.sorteo?.estado === 'en_proceso' || this.sorteo?.estado === 'finalizado';
  }

  get capturaDisponible(): boolean {
    return this.sorteo?.estado === 'en_proceso' && this.sorteo.ventaMinimaAlcanzada;
  }

  get minimumSaleRequiresReschedule(): boolean {
    return this.sorteo?.estado === 'en_proceso' && !this.sorteo.ventaMinimaAlcanzada;
  }

  get numeroValido(): boolean {
    const value = this.form.controls.numeroGanador.value;
    return this.form.valid && value.length === this.ticketDigits;
  }

  get estadoLabel(): string {
    if (this.sorteo?.estado === 'en_proceso') return 'En proceso';
    if (this.sorteo?.estado === 'proximo_a_iniciar') return 'Próximo a iniciar';
    return 'Activo';
  }

  async load(): Promise<void> {
    if (this.loading) return;
    this.loading = true;
    try {
      this.sorteo = await firstValueFrom(this.lotteryService.getCurrent()).catch(() => null);
      this.verification = null;
      this.minimumSaleOutcomeVisible = this.minimumSaleRequiresReschedule;
      this.form.reset({ numeroGanador: '' });
      this.startStatusTimer();
    } finally {
      this.loading = false;
      this.changeDetector.markForCheck();
    }
  }

  onNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const sanitized = input.value.replace(/\D/g, '').slice(0, this.ticketDigits);
    if (input.value !== sanitized) input.value = sanitized;
    this.form.controls.numeroGanador.setValue(sanitized);
    this.verification = null;
  }

  async prepareVerification(): Promise<void> {
    if (this.minimumSaleRequiresReschedule) {
      this.form.reset({ numeroGanador: '' });
      this.verification = null;
      this.minimumSaleOutcomeVisible = true;
      this.changeDetector.markForCheck();
      return;
    }
    if (!this.capturaDisponible) {
      this.notifications.show(
        'Captura no disponible',
        'El número ganador solo puede verificarse cuando el sorteo se encuentre en proceso.',
        'warning'
      );
      return;
    }
    if (!this.numeroValido) {
      this.form.controls.numeroGanador.markAsTouched();
      this.notifications.show(
        'Número incompleto',
        'Captura los ' + this.ticketDigits + ' dígitos del primer lugar de la Lotería Nacional.',
        'warning'
      );
      return;
    }

    if (!this.sorteo || this.processing) return;

    await this.prepareCelebrationAudio();
    this.processing = true;
    try {
      this.verification = await firstValueFrom(this.sorteosService.verifyWinningNumber(
        this.sorteo.id,
        this.form.controls.numeroGanador.value
      ));
      this.form.controls.numeroGanador.setValue(this.verification.numeroGanador);
      if (this.verification.hayGanador) this.startCelebration();
    } catch (error) {
      this.verification = null;
      this.notifications.show('No fue posible verificar', this.errorMessage(error), 'error');
    } finally {
      this.processing = false;
      this.changeDetector.markForCheck();
    }
  }

  clear(): void {
    this.form.reset({ numeroGanador: '' });
    this.verification = null;
  }

  async confirmReschedule(): Promise<void> {
    if (!this.sorteo || this.processing) return;
    const reschedulingForMinimumSale = this.minimumSaleRequiresReschedule;
    if (!reschedulingForMinimumSale && (!this.verification || this.verification.hayGanador)) return;
    const numeroGanador = reschedulingForMinimumSale
      ? null
      : this.verification?.numeroGanador ?? this.form.controls.numeroGanador.value;
    if (!reschedulingForMinimumSale && !numeroGanador) return;

    const confirmed = await this.notifications.confirm(
      '¿Reprogramar el sorteo?',
      reschedulingForMinimumSale
        ? `Solo se vendió el ${this.sorteo.porcentajeVenta}% de los boletos y se requiere al menos el ${this.sorteo.porcentajeMinimoVenta}%. El sorteo se reprogramará para dentro de 7 días; los boletos vendidos se conservarán.`
        : 'El sorteo se reprogramará para dentro de 7 días. Los boletos ya vendidos se conservarán y el resto volverá a estar disponible.',
      'Sí, Reprogramar',
      'No, Cancelar'
    );
    if (!confirmed) return;

    this.processing = true;
    this.globalLoading.show();
    try {
      const result = await firstValueFrom(this.sorteosService.rescheduleWithoutWinner(
        this.sorteo.id,
        numeroGanador
      ));
      this.notifications.show(
        'Sorteo reprogramado',
        'La nueva fecha es ' + new ServerLocalDatePipe().transform(result.nuevaFecha),
        'success'
      );
      await this.load();
    } catch (error) {
      this.notifications.show('No fue posible reprogramar', this.errorMessage(error), 'error');
    } finally {
      this.processing = false;
      this.globalLoading.hide();
      this.changeDetector.markForCheck();
    }
  }

  async confirmFinalize(): Promise<void> {
    if (!this.sorteo || !this.verification?.hayGanador || this.processing) return;
    const confirmed = await this.notifications.confirm(
      '¿Finalizar el sorteo?',
      'Se registrará al ganador y se dara por finalizado el sorteo.',
      'Sí, Finalizar',
      'No, Cancelar',
      true
    );
    if (!confirmed) return;

    this.processing = true;
    this.globalLoading.show();
    try {
      await firstValueFrom(this.sorteosService.finalizeWinner(
        this.sorteo.id,
        this.verification.numeroGanador
      ));
      this.notifications.show(
        'Sorteo finalizado',
        '',
        'success'
      );
      await this.load();
    } catch (error) {
      this.notifications.show('No fue posible finalizar', this.errorMessage(error), 'error');
    } finally {
      this.processing = false;
      this.globalLoading.hide();
      this.changeDetector.markForCheck();
    }
  }

  private startStatusTimer(): void {
    if (this.statusTimerId !== null) window.clearInterval(this.statusTimerId);
    if (!this.sorteo || this.sorteo.estado === 'en_proceso') return;
    const target = performance.now() + Math.max(0, this.sorteo.segundosParaInicio) * 1000;
    this.statusTimerId = window.setInterval(() => {
      if (!this.sorteo || performance.now() < target) return;
      if (this.statusTimerId !== null) window.clearInterval(this.statusTimerId);
      this.statusTimerId = null;
      void this.load();
    }, 1000);
  }

  private errorMessage(error: unknown): string {
    const candidate = error as { error?: { message?: string } };
    return candidate?.error?.message ?? 'Ocurrió un error al procesar el resultado del sorteo.';
  }

  private async prepareCelebrationAudio(): Promise<void> {
    try {
      this.celebrationAudio ??= new AudioContext();
      if (this.celebrationAudio.state === 'suspended')
        await this.celebrationAudio.resume();
    } catch {
      this.celebrationAudio = null;
    }
  }

  private startCelebration(): void {
    if (this.celebrationTimeoutId !== null)
      window.clearTimeout(this.celebrationTimeoutId);
    this.showCelebration = true;
    this.playCelebrationAudio();
    this.celebrationTimeoutId = window.setTimeout(() => {
      this.showCelebration = false;
      this.celebrationTimeoutId = null;
      this.changeDetector.markForCheck();
    }, 5000);
  }

  private playCelebrationAudio(): void {
    const context = this.celebrationAudio;
    if (!context || context.state !== 'running') return;

    const start = context.currentTime + 0.03;
    const master = context.createGain();
    master.gain.setValueAtTime(0.22, start);
    master.gain.exponentialRampToValueAtTime(0.001, start + 4.2);
    master.connect(context.destination);

    [523.25, 659.25, 783.99, 1046.5].forEach((frequency, index) => {
      const noteStart = start + index * 0.32;
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = index === 3 ? 'sine' : 'triangle';
      oscillator.frequency.setValueAtTime(frequency, noteStart);
      gain.gain.setValueAtTime(0.001, noteStart);
      gain.gain.exponentialRampToValueAtTime(0.32, noteStart + 0.04);
      gain.gain.exponentialRampToValueAtTime(0.001, noteStart + 0.58);
      oscillator.connect(gain);
      gain.connect(master);
      oscillator.start(noteStart);
      oscillator.stop(noteStart + 0.62);
    });

    const applauseDuration = 3.3;
    const buffer = context.createBuffer(1, context.sampleRate * applauseDuration, context.sampleRate);
    const samples = buffer.getChannelData(0);
    for (let index = 0; index < samples.length; index++)
      samples[index] = Math.random() * 2 - 1;
    const applause = context.createBufferSource();
    const applauseFilter = context.createBiquadFilter();
    const applauseGain = context.createGain();
    applause.buffer = buffer;
    applauseFilter.type = 'bandpass';
    applauseFilter.frequency.value = 1650;
    applauseFilter.Q.value = 0.7;
    applauseGain.gain.setValueAtTime(0.001, start + 0.7);
    for (let offset = 0.7; offset < 3.4; offset += 0.14) {
      applauseGain.gain.setValueAtTime(0.045 + Math.random() * 0.025, start + offset);
      applauseGain.gain.exponentialRampToValueAtTime(0.004, start + offset + 0.09);
    }
    applause.connect(applauseFilter);
    applauseFilter.connect(applauseGain);
    applauseGain.connect(master);
    applause.start(start + 0.7);
    applause.stop(start + 4);
  }
}








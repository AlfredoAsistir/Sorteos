import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import {
  ScratchcardAutoPlaySpeed,
  ScratchcardDialogData,
  ScratchcardDialogResult,
  ScratchcardRevealResponse,
  ScratchcardStartResponse,
  ScratchcardSymbol,
} from '../../core/scratchcards/scratchcard.models';
import { NotificationService } from '../../core/notifications/notification.service';
import { MaterialModule } from '../../material.module';
import { ScratchcardService } from '../../services/scratchcard.service';
import { WalletService } from '../../services/wallet.service';
import { ScratchLayerComponent } from '../scratch-layer/scratch-layer.component';

type ScratchcardUiState = 'ready' | 'scratching' | 'revealing' | 'revealed' | 'error';

@Component({
  selector: 'app-scratchcard-dialog',
  standalone: true,
  imports: [CommonModule, MaterialModule, ScratchLayerComponent],
  templateUrl: './scratchcard-dialog.component.html',
  styleUrl: './scratchcard-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScratchcardDialogComponent implements OnInit, AfterViewInit, OnDestroy {
  readonly cellIndexes = Array.from({ length: 9 }, (_, index) => index);
  readonly confettiPieces = Array.from({ length: 18 }, (_, index) => index);
  readonly autoPlaySpeeds: readonly ScratchcardAutoPlaySpeed[] = [0.5, 1, 1.5];
  readonly symbolIcons: Record<ScratchcardSymbol, string> = {
    money: 'paid',
    star: 'star',
    gift: 'card_giftcard',
    trophy: 'emoji_events',
    ticket: 'confirmation_number',
    crown: 'workspace_premium',
  };
  private readonly boardLines: readonly (readonly number[])[] = [
    [0, 1, 2],
    [3, 4, 5],
    [6, 7, 8],
    [0, 3, 6],
    [1, 4, 7],
    [2, 5, 8],
    [0, 4, 8],
    [2, 4, 6],
  ];

  state: ScratchcardUiState = 'ready';
  revealInProgress = false;
  forceReveal = false;
  visualComplete = false;
  celebrationActive = false;
  autoPlayActive = false;
  autoPlaySpeed: ScratchcardAutoPlaySpeed = 0.5;
  autoPrizeRevealed = false;
  nextCountdown: number | null = null;
  response: ScratchcardRevealResponse | null = null;
  preview: ScratchcardStartResponse | null = null;
  private revealRequest: Promise<void> | null = null;
  private startRequest: Promise<void> | null = null;
  private boardDecisionReached = false;
  private readonly revealedZones = new Set<string>();
  private winningCells = new Set<number>();
  private audioContext: AudioContext | null = null;
  private readonly autoRevealedCells = new Set<number>();
  private autoPlayRunId = 0;
  private autoPlayStartTimer: ReturnType<typeof setTimeout> | null = null;
  private nextCountdownTimer: ReturnType<typeof setInterval> | null = null;
  private destroyed = false;

  constructor(
    @Inject(MAT_DIALOG_DATA) readonly data: ScratchcardDialogData,
    private readonly dialogRef: MatDialogRef<ScratchcardDialogComponent, ScratchcardDialogResult>,
    private readonly scratchcards: ScratchcardService,
    readonly walletService: WalletService,
    private readonly notifications: NotificationService,
    private readonly changeDetector: ChangeDetectorRef
  ) {
    this.autoPlaySpeed = data.autoPlaySpeed ?? 0.5;
  }

  ngOnInit(): void {
    if (this.data.historicalResult) this.showHistoricalResult();
  }

  ngAfterViewInit(): void {
    if (this.data.historicalResult) return;
    if (!this.data.autoPlay) return;
    this.autoPlayStartTimer = setTimeout(() => {
      this.autoPlayStartTimer = null;
      void this.startAutoPlay();
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.autoPlayRunId++;
    if (this.autoPlayStartTimer) clearTimeout(this.autoPlayStartTimer);
    this.clearNextCountdown();
    void this.audioContext?.close();
    this.audioContext = null;
  }

  onScratchStarted(): void {
    if (this.visualComplete) return;
    this.primeAudio();
    if (this.state === 'ready') this.state = 'scratching';
    void this.ensureStarted();
  }

  onZoneRevealed(zone: string): void {
    this.revealedZones.add(zone);
    if (!zone.startsWith('cell-') || this.boardDecisionReached || this.visualComplete) return;

    void this.ensureStarted().then(() => this.evaluateBoardState());
  }

  async retryResult(): Promise<void> {
    if (this.revealInProgress || this.visualComplete) return;
    this.primeAudio();

    if (this.boardDecisionReached) {
      await this.ensureReveal();
      if (this.response) this.completeVisualExperience();
      return;
    }

    await this.ensureStarted();
    this.evaluateBoardState();
  }

  close(): void {
    this.cancelAutoPlay(false);
    this.closeDialog(false, false);
  }

  next(): void {
    if (!this.visualComplete || !this.data.hasNext) return;
    const continueAutoPlay = this.autoPlayActive;
    this.cancelAutoPlay(true);
    this.closeDialog(true, continueAutoPlay);
  }

  toggleAutoPlay(): void {
    if (this.autoPlayActive) {
      this.cancelAutoPlay(false);
      return;
    }
    this.autoPlaySpeed = 0.5;
    void this.startAutoPlay();
  }

  setAutoPlaySpeed(speed: ScratchcardAutoPlaySpeed): void {
    this.autoPlaySpeed = speed;
    this.changeDetector.markForCheck();
  }

  shouldForceRevealCell(index: number): boolean {
    return this.forceReveal || this.autoRevealedCells.has(index);
  }

  symbolAt(index: number): ScratchcardSymbol | null {
    return this.response?.matrizResultado[index] ??
      this.preview?.matrizResultado[index] ??
      null;
  }

  possiblePrize(): number | null {
    return this.response?.premioPosible ?? this.preview?.premioPosible ?? null;
  }

  isWinningCell(index: number): boolean {
    return this.visualComplete && this.winningCells.has(index);
  }

  private async ensureStarted(): Promise<void> {
    if (this.preview || this.response || this.startRequest) {
      return this.startRequest ?? Promise.resolve();
    }

    this.startRequest = this.requestStart();
    await this.startRequest;
  }

  private async requestStart(): Promise<void> {
    try {
      const preview = await firstValueFrom(
        this.scratchcards.start(this.data.scratchcard.id)
      );
      this.validateStartResponse(preview);
      this.preview = preview;
      if (this.state === 'error') this.state = 'scratching';
    } catch (error) {
      this.state = 'error';
      this.notifications.show(
        'No fue posible preparar el rascadito',
        this.errorMessage(error),
        'warning'
      );
    } finally {
      this.startRequest = null;
      this.changeDetector.markForCheck();
    }
  }
  private async ensureReveal(): Promise<void> {
    if (this.response || this.revealRequest) return this.revealRequest ?? Promise.resolve();

    this.revealInProgress = true;
    this.state = 'revealing';
    this.changeDetector.markForCheck();
    this.revealRequest = this.requestReveal();
    await this.revealRequest;
  }

  private async requestReveal(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.scratchcards.reveal(this.data.scratchcard.id)
      );
      this.validateResponse(response);
      this.response = response;
      this.winningCells = this.resolveWinningCells(response.lineaGanadora);
      this.walletService.setBalanceFromServer(response.saldoActual);

      if (!response.reveladoAhora) {
        this.forceReveal = true;
        this.completeVisualExperience(false);
      } else if (this.boardDecisionReached) {
        this.completeVisualExperience();
      } else {
        this.state = 'scratching';
      }
    } catch (error) {
      this.state = 'error';
      this.notifications.show(
        'No fue posible revelar',
        this.errorMessage(error),
        'warning'
      );
    } finally {
      this.revealInProgress = false;
      this.revealRequest = null;
      this.changeDetector.markForCheck();
    }
  }

  private evaluateBoardState(): void {
    if (!this.preview || this.response || this.boardDecisionReached || this.revealInProgress) return;

    const revealedIndexes = new Set(
      this.cellIndexes.filter(index => this.revealedZones.has('cell-' + index))
    );
    const matrix = this.preview.matrizResultado;
    const hasCompletedWinningLine = this.boardLines.some(line =>
      line.every(index => revealedIndexes.has(index)) &&
      line.every(index => matrix[index] === matrix[line[0]])
    );
    const hasPossibleWinningLine = this.boardLines.some(line => {
      const revealedSymbols = line
        .filter(index => revealedIndexes.has(index))
        .map(index => matrix[index]);
      return new Set(revealedSymbols).size <= 1;
    });

    if (!hasCompletedWinningLine && hasPossibleWinningLine) return;

    this.boardDecisionReached = true;
    void this.ensureReveal().then(() => {
      if (this.response) this.completeVisualExperience();
    });
  }

  private async startAutoPlay(): Promise<void> {
    if (this.autoPlayActive || this.visualComplete || this.destroyed) return;

    this.autoPlayActive = true;
    this.clearNextCountdown();
    const runId = ++this.autoPlayRunId;
    this.onScratchStarted();
    this.changeDetector.markForCheck();
    await this.ensureStarted();

    if (!this.preview || !this.isAutoPlayRunActive(runId)) {
      if (!this.preview) this.autoPlayActive = false;
      this.changeDetector.markForCheck();
      return;
    }

    if (!this.autoPrizeRevealed) {
      this.autoPrizeRevealed = true;
      this.changeDetector.markForCheck();
      if (!await this.waitForAutoPlay(this.autoPlayDelay(650), runId)) return;
    }

    for (const index of this.cellIndexes) {
      if (!this.isAutoPlayRunActive(runId) || this.visualComplete) return;
      const zone = 'cell-' + index;
      if (this.revealedZones.has(zone)) continue;

      this.autoRevealedCells.add(index);
      this.changeDetector.markForCheck();
      this.onZoneRevealed(zone);
      if (!await this.waitForAutoPlay(this.autoPlayDelay(850), runId)) return;
    }
  }

  private isAutoPlayRunActive(runId: number): boolean {
    return !this.destroyed && this.autoPlayActive && this.autoPlayRunId === runId;
  }

  private autoPlayDelay(normalSpeedMilliseconds: number): number {
    return Math.round(normalSpeedMilliseconds * 0.5 / this.autoPlaySpeed);
  }

  private async waitForAutoPlay(milliseconds: number, runId: number): Promise<boolean> {
    await new Promise<void>(resolve => setTimeout(resolve, milliseconds));
    return this.isAutoPlayRunActive(runId);
  }

  private cancelAutoPlay(preserveState: boolean): void {
    this.autoPlayRunId++;
    this.clearNextCountdown();
    if (!preserveState) this.autoPlayActive = false;
    this.changeDetector.markForCheck();
  }

  private startNextCountdown(): void {
    this.clearNextCountdown();
    if (!this.autoPlayActive) return;
    if (!this.data.hasNext) {
      this.autoPlayActive = false;
      return;
    }

    this.nextCountdown = this.autoPlaySpeed === 0.5
      ? 4
      : this.autoPlaySpeed === 1
        ? 3
        : 2;
    this.nextCountdownTimer = setInterval(() => {
      if (this.nextCountdown === null) return;
      this.nextCountdown--;
      if (this.nextCountdown <= 0) {
        this.next();
        return;
      }
      this.changeDetector.markForCheck();
    }, 1000);
  }

  private clearNextCountdown(): void {
    if (this.nextCountdownTimer) clearInterval(this.nextCountdownTimer);
    this.nextCountdownTimer = null;
    this.nextCountdown = null;
  }

  private closeDialog(openNext: boolean, autoPlay: boolean): void {
    this.dialogRef.close({
      scratchcardId: this.data.scratchcard.id,
      revealed: this.response?.revelado ?? this.data.scratchcard.revelado,
      revealedAt: this.response?.fechaRevelado,
      prizeAwarded: this.response?.reveladoAhora && this.response.esGanador
        ? this.response.importePremio ?? 0
        : undefined,
      openNext,
      autoPlay,
      autoPlaySpeed: this.autoPlaySpeed,
    });
  }

  private completeVisualExperience(allowCelebration = true): void {
    if (!this.response || this.visualComplete) return;
    this.visualComplete = true;
    this.forceReveal = true;
    this.state = 'revealed';

    if (
      allowCelebration &&
      this.response.reveladoAhora &&
      this.response.esGanador &&
      this.response.importePremio
    ) {
      this.celebrationActive = true;
      this.walletService.showCredit(this.response.importePremio);
      this.playWinnerSound();
    }
    this.startNextCountdown();
    this.changeDetector.markForCheck();
  }

  private showHistoricalResult(): void {
    const history = this.data.historicalResult;
    if (!history) return;
    if (history.matrizResultado.length !== 9 || !history.lineaGanadora) {
      this.state = 'error';
      this.notifications.show(
        'No fue posible mostrar el rascadito',
        'El resultado visual guardado está incompleto.',
        'warning'
      );
      this.changeDetector.markForCheck();
      return;
    }

    this.response = {
      id: history.id,
      folio: history.folio,
      revelado: true,
      reveladoAhora: false,
      esGanador: true,
      importePremio: history.importePremio,
      premioPosible: history.importePremio,
      fechaRevelado: history.fechaRevelado,
      saldoActual: this.walletService.balance(),
      matrizResultado: history.matrizResultado,
      lineaGanadora: history.lineaGanadora,
    };
    this.winningCells = this.resolveWinningCells(history.lineaGanadora);
    this.completeVisualExperience(false);
  }
  private validateStartResponse(response: ScratchcardStartResponse): void {
    if (
      response.id !== this.data.scratchcard.id ||
      response.matrizResultado?.length !== 9 ||
      !response.premioPosible ||
      response.premioPosible <= 0
    ) {
      throw new Error('El servidor devolvió una vista previa incompleta.');
    }
  }
  private validateResponse(response: ScratchcardRevealResponse): void {
    if (!response.revelado || response.matrizResultado?.length !== 9) {
      throw new Error('El servidor devolvió un resultado visual incompleto.');
    }
    if (!response.premioPosible || response.premioPosible <= 0) {
      throw new Error('El servidor no devolvió un premio posible válido.');
    }
    if (response.esGanador && (!response.importePremio || !response.lineaGanadora)) {
      throw new Error('El servidor devolvió un premio incompleto.');
    }
  }

  private resolveWinningCells(line: string | null | undefined): Set<number> {
    const lines: Record<string, number[]> = {
      'row:0': [0, 1, 2],
      'row:1': [3, 4, 5],
      'row:2': [6, 7, 8],
      'column:0': [0, 3, 6],
      'column:1': [1, 4, 7],
      'column:2': [2, 5, 8],
      'diagonal:main': [0, 4, 8],
      'diagonal:anti': [2, 4, 6],
    };
    return new Set(line ? lines[line] ?? [] : []);
  }

  private primeAudio(): void {
    if (typeof window === 'undefined' || this.audioContext) return;
    const AudioContextConstructor = window.AudioContext;
    if (!AudioContextConstructor) return;
    this.audioContext = new AudioContextConstructor();
    void this.audioContext.resume();
  }

  private playWinnerSound(): void {
    const context = this.audioContext;
    if (!context) return;
    void context.resume();
    const now = context.currentTime;
    [523.25, 659.25, 783.99].forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = 'sine';
      oscillator.frequency.value = frequency;
      gain.gain.setValueAtTime(0.0001, now + index * 0.09);
      gain.gain.exponentialRampToValueAtTime(0.12, now + index * 0.09 + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + index * 0.09 + 0.24);
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(now + index * 0.09);
      oscillator.stop(now + index * 0.09 + 0.26);
    });
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ??
        error.error?.message ??
        'Revisa tu conexión e inténtalo nuevamente; tu premio permanece seguro.';
    }
    return error instanceof Error
      ? error.message
      : 'Revisa tu conexión e inténtalo nuevamente; tu premio permanece seguro.';
  }
}


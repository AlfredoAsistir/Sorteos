import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnInit, Output } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import {
  ScratchcardAutoPlaySpeed,
  ScratchcardDialogData,
  ScratchcardDialogResult,
  ScratchcardListItem,
} from '../../core/scratchcards/scratchcard.models';
import { NotificationService } from '../../core/notifications/notification.service';
import { MaterialModule } from '../../material.module';
import { ScratchcardService } from '../../services/scratchcard.service';
import { ScratchcardDialogComponent } from '../scratchcard-dialog/scratchcard-dialog.component';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-scratchcards-panel',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule],
  templateUrl: './scratchcards-panel.component.html',
  styleUrl: './scratchcards-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScratchcardsPanelComponent implements OnInit {
  @Output() readonly availabilityChanged = new EventEmitter<boolean>();
  @Output() readonly prizeAwarded = new EventEmitter<number>();
  loading = true;
  pendingCount = 0;
  scratchcards: ScratchcardListItem[] = [];

  constructor(
    private readonly service: ScratchcardService,
    private readonly dialog: MatDialog,
    private readonly notifications: NotificationService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  get pendingScratchcards(): ScratchcardListItem[] {
    return this.scratchcards.filter(scratchcard => !scratchcard.revelado);
  }

  ngOnInit(): void {
    void this.load();
  }

  async openScratchcard(scratchcard: ScratchcardListItem): Promise<void> {
    let current: ScratchcardListItem | undefined = scratchcard;
    let autoPlay = false;
    let autoPlaySpeed: ScratchcardAutoPlaySpeed = 0.5;

    while (current) {
      const hasNext = this.pendingScratchcards.some(item => item.id !== current?.id);
      const shouldAutoPlay = autoPlay;
      const dialogRef: MatDialogRef<ScratchcardDialogComponent, ScratchcardDialogResult> = this.dialog.open<ScratchcardDialogComponent, ScratchcardDialogData, ScratchcardDialogResult>(ScratchcardDialogComponent, {
        data: {
          scratchcard: current,
          hasNext,
          autoPlay: shouldAutoPlay,
          autoPlaySpeed,
        },
        autoFocus: false,
        restoreFocus: true,
        disableClose: true,
        maxWidth: '100vw',
        panelClass: 'scratchcard-dialog-panel',
      });
      const result: ScratchcardDialogResult | undefined = await firstValueFrom(dialogRef.afterClosed());
      if (result?.revealed) this.applyRevealResult(result);
      const prizeAwarded = result?.prizeAwarded ?? 0;
      if (prizeAwarded > 0) this.prizeAwarded.emit(prizeAwarded);
      autoPlay = result?.autoPlay ?? false;
      autoPlaySpeed = result?.autoPlaySpeed ?? autoPlaySpeed;
      if (!result?.openNext) break;

      current = this.pendingScratchcards.find(item => item.id !== current?.id);
    }
  }

  private async load(): Promise<void> {
    this.loading = true;
    try {
      const response = await firstValueFrom(this.service.getScratchcards());
      this.pendingCount = response.pendientes;
      this.scratchcards = response.rascaditos;
      this.availabilityChanged.emit(this.pendingScratchcards.length > 0);
    } catch (error) {
      this.notifications.show(
        'Rascaditos no disponibles',
        this.errorMessage(error),
        'warning'
      );
    } finally {
      this.loading = false;
      this.changeDetector.markForCheck();
    }
  }

  private applyRevealResult(result: ScratchcardDialogResult): void {
    const current = this.scratchcards.find(
      scratchcard => scratchcard.id === result.scratchcardId
    );
    if (!current || current.revelado) return;
    current.revelado = true;
    current.fechaRevelado = result.revealedAt ?? null;
    this.pendingCount = Math.max(0, this.pendingCount - 1);
    this.scratchcards = [
      ...this.scratchcards.filter(scratchcard => !scratchcard.revelado),
      ...this.scratchcards
        .filter(scratchcard => scratchcard.revelado)
        .sort((left, right) => right.id - left.id),
    ];
    this.availabilityChanged.emit(this.pendingScratchcards.length > 0);
    this.changeDetector.markForCheck();
  }

  private errorMessage(error: unknown): string {
    return error instanceof HttpErrorResponse
      ? error.error?.message ?? 'No fue posible consultar tus rascaditos.'
      : 'No fue posible consultar tus rascaditos.';
  }
}

import { CommonModule } from '@angular/common';
import { ServerLocalDatePipe } from '../../core/date/server-local-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ScratchcardDialogComponent } from '../../components/scratchcard-dialog/scratchcard-dialog.component';
import { ScratchcardDialogData } from '../../core/scratchcards/scratchcard.models';
import { NotificationService } from '../../core/notifications/notification.service';
import { UserPrizeHistory, UserPrizeHistoryItem } from '../../core/winnings/user-prize.models';
import { MaterialModule } from '../../material.module';
import { LoadingService } from '../../services/loading.service';
import { SorteosService } from '../../services/sorteos.service';
import { UserPrizeService } from '../../services/user-prize.service';

@Component({
  selector: 'app-he-ganado',
  standalone: true,
  imports: [CommonModule, ServerLocalDatePipe, MaterialModule],
  templateUrl: './he-ganado.component.html',
  styleUrl: './he-ganado.component.scss'
})
export class HeGanadoComponent implements OnInit, OnDestroy {
  private readonly service = inject(UserPrizeService);
  private readonly sorteosService = inject(SorteosService);
  private readonly loadingService = inject(LoadingService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly changeDetector = inject(ChangeDetectorRef);

  history: UserPrizeHistory | null = null;
  loaded = false;

  async ngOnInit(): Promise<void> {
    this.loadingService.show();
    try {
      this.history = await firstValueFrom(this.service.getHistory());
    } catch (error) {
      this.notifications.show(
        'No fue posible cargar tus premios',
        this.errorMessage(error),
        'error'
      );
    } finally {
      this.loaded = true;
      this.loadingService.hide();
      this.changeDetector.detectChanges();
    }
  }

  ngOnDestroy(): void {
    this.loadingService.hide();
  }

  imageUrl(path: string | null): string | null {
    return this.sorteosService.imageUrl(path);
  }

  openScratchcard(prize: UserPrizeHistoryItem): void {
    if (
      prize.tipo !== 'rascadito' ||
      !prize.rascaditoId ||
      !prize.rascaditoFolio ||
      !prize.importePremio ||
      !prize.matrizResultado ||
      !prize.lineaGanadora
    ) return;

    this.dialog.open<ScratchcardDialogComponent, ScratchcardDialogData>(
      ScratchcardDialogComponent,
      {
        data: {
          scratchcard: {
            id: prize.rascaditoId,
            folio: prize.rascaditoFolio,
            revelado: true,
            fechaGeneracion: prize.fecha,
            fechaRevelado: prize.fecha,
          },
          hasNext: false,
          autoPlay: false,
          historyMode: true,
          historicalResult: {
            id: prize.rascaditoId,
            folio: prize.rascaditoFolio,
            importePremio: prize.importePremio,
            fechaRevelado: prize.fecha,
            matrizResultado: prize.matrizResultado,
            lineaGanadora: prize.lineaGanadora,
          },
        },
        autoFocus: false,
        restoreFocus: true,
        disableClose: true,
        maxWidth: '100vw',
        panelClass: 'scratchcard-dialog-panel',
      }
    );
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ??
        error.error?.message ??
        'No fue posible consultar el historial de premios.';
    }
    return error instanceof Error
      ? error.message
      : 'No fue posible consultar el historial de premios.';
  }
}

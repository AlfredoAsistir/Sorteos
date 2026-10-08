import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output } from '@angular/core';
import { Subscription } from 'rxjs';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { NotificationService } from '../../core/notifications/notification.service';
import { environment } from '../../../environments/environment';

interface TransparencyStatus {
  fechaProgramada: string;
  estado: 'pendiente' | 'publicado' | 'reprogramar' | 'incidencia' | 'no_aplica';
}

@Component({
  selector: 'app-sorteo-transparency',
  standalone: true,
  templateUrl: './sorteo-transparency.component.html',
  styleUrl: './sorteo-transparency.component.scss',
})
export class SorteoTransparencyComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) lottery!: CurrentLottery;
  @Input() showDownload = true;
  @Output() stateChange = new EventEmitter<TransparencyStatus['estado'] | null>();
  status: TransparencyStatus | null = null;
  downloading = false;
  private timer: ReturnType<typeof setInterval> | null = null;
  private request: Subscription | null = null;
  private downloadRequest: Subscription | null = null;
  private loadedKey = '';

  constructor(private readonly http: HttpClient, private readonly notifications: NotificationService) {}

  ngOnChanges(): void {
    const key = `${this.lottery.id}:${this.lottery.fecha}`;
    if (this.loadedKey === key) return;
    this.loadedKey = key;
    this.status = null;
    this.stateChange.emit(null);
    this.request?.unsubscribe();
    this.downloadRequest?.unsubscribe();
    this.downloadRequest = null;
    this.downloading = false;
    if (this.timer) clearInterval(this.timer);
    this.refresh();
    this.timer = setInterval(() => this.refresh(), 15000);
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.downloadRequest?.unsubscribe();
    if (this.timer) clearInterval(this.timer);
  }

  downloadPdf(): void {
    if (!this.showDownload || this.downloading || this.status?.estado !== 'publicado' ||
        this.status.fechaProgramada !== this.lottery.fecha) return;
    const lotteryId = this.lottery.id;
    this.downloading = true;
    this.downloadRequest = this.http.get(`${environment.apiUrl}/api/sorteo-transparencia/${lotteryId}/pdf`, {
      responseType: 'blob',
    }).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `sorteo-${lotteryId}-numeros-no-vendidos.pdf`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 60000);
        this.downloading = false;
        this.downloadRequest = null;
      },
      error: () => {
        this.downloading = false;
        this.downloadRequest = null;
        this.notifications.show('Documento no disponible', 'No fue posible descargar el PDF de transparencia.', 'error');
      },
    });
  }

  private refresh(): void {
    this.request?.unsubscribe();
    this.request = this.http.get<TransparencyStatus>(
      `${environment.apiUrl}/api/sorteo-transparencia/${this.lottery.id}`
    ).subscribe({
      next: result => {
        if (result.fechaProgramada !== this.lottery.fecha) {
          this.status = null;
          this.stateChange.emit(null);
          return;
        }
        this.status = result;
        this.stateChange.emit(result.estado);
      },
      error: () => {
        this.status = null;
        this.stateChange.emit(null);
      },
    });
  }

}

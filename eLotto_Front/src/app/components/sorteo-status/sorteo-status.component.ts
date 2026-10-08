import { Component, Input } from '@angular/core';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { MaterialModule } from '../../material.module';
import { SorteoRescheduleMessageComponent } from '../sorteo-reschedule-message/sorteo-reschedule-message.component';
import { SorteoTransparencyComponent } from '../sorteo-transparency/sorteo-transparency.component';

@Component({
  selector: 'app-sorteo-status',
  standalone: true,
  imports: [MaterialModule, SorteoRescheduleMessageComponent, SorteoTransparencyComponent],
  templateUrl: './sorteo-status.component.html',
  styleUrl: './sorteo-status.component.scss',
})
export class SorteoStatusComponent {
  @Input({ required: true }) lottery!: CurrentLottery;
  @Input({ required: true }) secondsToStart = 0;
  transparencyState: string | null = null;

  get rescheduleReason(): 'reprogramar' | 'incidencia' | null {
    if (this.transparencyState === 'incidencia') return 'incidencia';
    const belowMinimum = this.lottery.cantidadBoletos > 0 &&
      this.lottery.boletosVendidos * 100 < this.lottery.cantidadBoletos * this.lottery.porcentajeMinimoVenta;
    return this.transparencyState === 'reprogramar' || belowMinimum ? 'reprogramar' : null;
  }

  get liveUrl(): string | null {
    return this.lottery.urlTransmisionEnVivo?.trim() || null;
  }

  get countdown(): string {
    const seconds = Math.max(0, this.secondsToStart);
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor(seconds % 3600 / 60);
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
  }
}

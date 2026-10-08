import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';

@Component({
  selector: 'app-sorteo-reschedule-message',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sorteo-reschedule-message.component.html',
  styleUrl: './sorteo-reschedule-message.component.scss',
})
export class SorteoRescheduleMessageComponent {
  @Input({ required: true }) lottery!: CurrentLottery;
  @Input({ required: true }) reason!: 'reprogramar' | 'incidencia';

  get scheduledDate(): string {
    const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(this.lottery.fecha);
    return match ? `${match[3]}/${match[2]}/${match[1]} a las ${match[4]}:${match[5]} h` : this.lottery.fecha;
  }

  get soldPercentage(): number {
    if (this.lottery.cantidadBoletos <= 0) return 0;
    return Math.floor(this.lottery.boletosVendidos * 10000 / this.lottery.cantidadBoletos) / 100;
  }
}

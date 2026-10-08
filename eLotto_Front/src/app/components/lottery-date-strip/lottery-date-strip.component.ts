import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';

@Component({
  selector: 'app-lottery-date-strip',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './lottery-date-strip.component.html',
  styleUrl: './lottery-date-strip.component.scss',
})
export class LotteryDateStripComponent {
  private static readonly DateFormatter = new Intl.DateTimeFormat('es-MX', {
    weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC',
  });

  @Input({ required: true }) lottery!: CurrentLottery;

  get formattedDate(): string {
    const parts = /^(\d{4})-(\d{2})-(\d{2})/.exec(this.lottery.fecha);
    if (!parts) return this.lottery.fecha;
    return LotteryDateStripComponent.DateFormatter.format(
      new Date(Date.UTC(Number(parts[1]), Number(parts[2]) - 1, Number(parts[3])))
    );
  }

  get formattedTime(): string {
    const parts = /T(\d{2}):(\d{2})/.exec(this.lottery.fecha);
    return parts ? `${parts[1]}:${parts[2]}` : '';
  }
}

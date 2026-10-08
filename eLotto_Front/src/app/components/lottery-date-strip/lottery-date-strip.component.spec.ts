import { TestBed } from '@angular/core/testing';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { LotteryDateStripComponent } from './lottery-date-strip.component';

describe('LotteryDateStripComponent', () => {
  it('muestra la fecha, la hora guardada y la zona configurada sin convertir la hora', async () => {
    await TestBed.configureTestingModule({ imports: [LotteryDateStripComponent] }).compileComponents();
    const fixture = TestBed.createComponent(LotteryDateStripComponent);
    fixture.componentInstance.lottery = {
      fecha: '2026-10-20T20:30:00',
      zonaHoraria: 'Tijuana',
    } as CurrentLottery;
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('20 de octubre de 2026');
    expect(text).toContain('20:30 · Hora: Tijuana');
  });
});

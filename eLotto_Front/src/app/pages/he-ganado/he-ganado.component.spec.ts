import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { UserPrizeHistory } from '../../core/winnings/user-prize.models';
import { LoadingService } from '../../services/loading.service';
import { SorteosService } from '../../services/sorteos.service';
import { UserPrizeService } from '../../services/user-prize.service';
import { HeGanadoComponent } from './he-ganado.component';

describe('HeGanadoComponent', () => {
  let fixture: ComponentFixture<HeGanadoComponent>;

  beforeEach(async () => {
    const history: UserPrizeHistory = {
      totalPremios: 3,
      totalSorteos: 1,
      totalRascaditos: 1,
      totalReferidos: 1,
      importeRascaditos: 500,
      importeReferidos: 5000,
      premios: [
        {
          id: 1,
          tipo: 'rascadito',
          origen: 'historico',
          fecha: '2026-09-24T12:30:00',
          sorteoId: 10,
          sorteoNombre: 'Sorteo que no debe mostrarse',
          sorteoImagen: null,
          numeroGanador: null,
          rascaditoId: 15,
          rascaditoFolio: 'ABCDEF1234567890',
          importePremio: 500,
          matrizResultado: null,
          lineaGanadora: null,
          referidoGanadorNombre: null,
          boletosRequeridos: null,
          boletosComprados: null,
        },
        {
          id: 2,
          tipo: 'sorteo',
          origen: 'sorteo',
          fecha: '2026-09-23T18:45:00',
          sorteoId: 9,
          sorteoNombre: 'Sorteo principal',
          sorteoImagen: null,
          numeroGanador: '12345',
          rascaditoId: null,
          rascaditoFolio: null,
          importePremio: null,
          matrizResultado: null,
          lineaGanadora: null,
          referidoGanadorNombre: null,
          boletosRequeridos: null,
          boletosComprados: null,
        },
        {
          id: 3,
          tipo: 'referido',
          origen: 'referido',
          fecha: '2026-09-22T10:15:00',
          sorteoId: 8,
          sorteoNombre: 'Sorteo del referido',
          sorteoImagen: null,
          numeroGanador: null,
          rascaditoId: null,
          rascaditoFolio: null,
          importePremio: 5000,
          matrizResultado: null,
          lineaGanadora: null,
          referidoGanadorNombre: 'Carlos López',
          boletosRequeridos: 200,
          boletosComprados: 2300,
        },
      ],
    };

    await TestBed.configureTestingModule({
      imports: [HeGanadoComponent],
      providers: [
        { provide: UserPrizeService, useValue: { getHistory: () => of(history) } },
        { provide: SorteosService, useValue: { imageUrl: () => null } },
        { provide: LoadingService, useValue: jasmine.createSpyObj('LoadingService', ['show', 'hide']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['show']) },
        { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HeGanadoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('hides the lottery name for scratchcards and shows dates without time', () => {
    const element = fixture.nativeElement as HTMLElement;
    const scratchcard = element.querySelector('.prize-card--scratchcard');

    expect(scratchcard?.textContent).not.toContain('Sorteo que no debe mostrarse');
    expect(element.textContent).toContain('Sorteo principal');
    expect(element.textContent).toContain('24/09/2026');
    expect(element.textContent).toContain('23/09/2026');
    expect(element.textContent).not.toContain('12:30');
    expect(element.textContent).not.toContain('18:45');
    expect(element.textContent).toContain('Ganado por referidos');
    expect(element.textContent).not.toContain('Premios de sorteos');
    expect(element.textContent).toContain('$5,000.00');
    expect(element.textContent).toContain('Carlos López');
    expect(element.textContent).toContain('Boletos requeridos');
    expect(element.textContent).toContain('2,300');
  });
});

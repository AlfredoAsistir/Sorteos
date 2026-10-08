import { FormBuilder } from '@angular/forms';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { FinalizedLotteryWinner, SorteoWinnerVerification } from '../../core/sorteos/sorteo.models';
import { CurrentLottery } from '../../core/user-lottery/user-lottery.models';
import { SorteosService } from '../../services/sorteos.service';
import { UserLotteryService } from '../../services/user-lottery.service';
import { LoadingService } from '../../services/loading.service';
import { EntregaPremioComponent } from './entrega-premio.component';

describe('EntregaPremioComponent referral transparency', () => {
  let fixture: ComponentFixture<EntregaPremioComponent>;
  let component: EntregaPremioComponent;
  let loading: jasmine.SpyObj<LoadingService>;

  beforeEach(async () => {
    loading = jasmine.createSpyObj<LoadingService>('LoadingService', ['show', 'hide']);
    await TestBed.configureTestingModule({
      imports: [EntregaPremioComponent],
      providers: [
        FormBuilder,
        {
          provide: UserLotteryService,
          useValue: { getCurrent: () => of(null), imageUrl: () => null },
        },
        {
          provide: SorteosService,
          useValue: {
            getLatestFinalizedWinner: () => of(null),
            verifyWinningNumber: () => of(null),
            rescheduleWithoutWinner: () => of(null),
            finalizeWinner: () => of(null),
          },
        },
        {
          provide: NotificationService,
          useValue: { show: () => undefined, confirm: () => Promise.resolve(false) },
        },
        { provide: LoadingService, useValue: loading },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EntregaPremioComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    component.loading = false;
    component.sorteo = null;
    component.verification = null;
  });

  it('places capture and lottery information on the left and reserves the right card for results', () => {
    component.sorteo = {
      id: 25,
      nombre: 'Sorteo actual',
      imagen1: '',
      fecha: '2026-09-23T10:00:00',
      precioBoleto: 10,
      precioPorMil: 5,
      cantidadBoletos: 60000,
      estado: 'en_proceso',
      ventaDisponible: false,
    } as never;
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const left = root.querySelector('.delivery-left');
    const result = root.querySelector('.result-card');
    expect(left?.querySelector('.capture-card')).not.toBeNull();
    expect(left?.querySelector('.lottery-card')).not.toBeNull();
    expect(result?.classList.contains('result-card--empty')).toBeTrue();
    expect(result?.textContent?.trim()).toBe('');
    expect(text()).not.toContain('Precio por boleto');
    expect(text()).not.toContain('Resultado oficial');
  });

  it('identifies a referred winner during preview without presenting a persisted reward', () => {
    component.sorteo = {
      id: 25,
      nombre: 'Sorteo actual',
      imagen1: '',
      imagen2: null,
      imagen3: null,
      fecha: '2026-09-23T10:00:00',
      precioBoleto: 10,
      precioPorMil: 5,
      cantidadBoletos: 1000,
      estado: 'en_proceso',
      ventaDisponible: false,
    } as never;
    component.verification = {
      sorteoId: 25,
      numeroGanador: '042',
      hayGanador: true,
      usuarioIdGanador: 2,
      usuarioGanador: 'Carlos',
      whatsAppGanador: '+5266*****000',
      folioCompra: 'FOLIO1',
      referencia: {
        referidorNombre: 'Alfredo López',
        finalizado: false,
        premioGenerado: true,
        boletosRequeridos: 100,
        boletosConfirmados: 127,
        importePremio: 5000,
        estado: null,
        fechaPago: null,
      },
    } satisfies SorteoWinnerVerification;
    fixture.detectChanges();

    const resultCard = (fixture.nativeElement as HTMLElement).querySelector('.result-card');
    expect(resultCard?.classList.contains('result-card--winner')).toBeTrue();
    expect(text()).not.toContain('Evaluación previa');
    expect(text()).toContain('Ganador por referido');
    expect(text()).toContain('Alfredo López');
    expect(text()).not.toContain('+5266*****001');
    expect(text()).toContain('100');
    expect(text()).toContain('127');
    expect(text()).toContain('$5,000.00');
  });

  it('omits the complete referral block when the preview is not eligible', () => {
    component.sorteo = {
      id: 25,
      nombre: 'Sorteo actual',
      imagen1: '',
      fecha: '2026-09-23T10:00:00',
      precioBoleto: 10,
      precioPorMil: 5,
      cantidadBoletos: 1000,
      estado: 'en_proceso',
      ventaDisponible: false,
    } as never;
    component.verification = {
      sorteoId: 25,
      numeroGanador: '042',
      hayGanador: true,
      usuarioIdGanador: 2,
      usuarioGanador: 'Carlos',
      whatsAppGanador: '+5266*****000',
      folioCompra: 'FOLIO1',
      referencia: {
        referidorNombre: 'Alfredo López',
        finalizado: false,
        premioGenerado: false,
        boletosRequeridos: 100,
        boletosConfirmados: 73,
        importePremio: null,
        estado: null,
        fechaPago: null,
      },
    };
    fixture.detectChanges();

    expect(text()).not.toContain('Ganador por referido');
    expect(text()).not.toContain('Alfredo López');
    expect(text()).not.toContain('Boletos requeridos');
    expect(text()).not.toContain('Boletos comprados');
  });

  it('shows the global loading indicator while rescheduling the lottery', async () => {
    setActiveLottery(component);
    component.verification = winnerVerification(false);
    const notifications = TestBed.inject(NotificationService);
    spyOn(notifications, 'confirm').and.resolveTo(true);
    const service = TestBed.inject(SorteosService);
    const reschedule = spyOn(service, 'rescheduleWithoutWinner').and.returnValue(of({
      sorteoId: 25,
      numeroGanador: '00042',
      nuevaFecha: '2026-09-30T10:00:00',
      sorteosReprogramados: 1,
    }));

    await component.confirmReschedule();

    expect(reschedule).toHaveBeenCalled();
    expect(loading.show).toHaveBeenCalledBefore(loading.hide);
    expect(loading.hide).toHaveBeenCalledTimes(1);
  });

  it('shows the minimum-sale message before capture and blocks the winning number', () => {
    component.sorteo = {
      ...activeLottery(),
      porcentajeVenta: 72.5,
      porcentajeMinimoVenta: 85,
      ventaMinimaAlcanzada: false,
    } as never;
    const service = TestBed.inject(SorteosService);
    const verify = spyOn(service, 'verifyWinningNumber');
    component.minimumSaleOutcomeVisible = component.minimumSaleRequiresReschedule;

    fixture.detectChanges();

    const rightCard = (fixture.nativeElement as HTMLElement).querySelector('.result-card');
    expect(rightCard?.textContent).toContain('Venta mínima no alcanzada');
    expect(rightCard?.textContent).toContain('72.5%');
    expect(rightCard?.textContent).toContain('85%');
    expect(rightCard?.textContent).toContain('Reprogramar sorteo');
    expect((fixture.nativeElement as HTMLElement).querySelector('.winner-number-field')).toBeNull();
    expect(component.capturaDisponible).toBeFalse();
    expect(verify).not.toHaveBeenCalled();
  });
  it('allows rescheduling when the minimum sale was not reached', async () => {
    component.sorteo = {
      ...activeLottery(),
      porcentajeVenta: 72.5,
      porcentajeMinimoVenta: 85,
      ventaMinimaAlcanzada: false,
    } as never;
    const notifications = TestBed.inject(NotificationService);
    spyOn(notifications, 'confirm').and.resolveTo(true);
    const service = TestBed.inject(SorteosService);
    const reschedule = spyOn(service, 'rescheduleWithoutWinner').and.returnValue(of({
      sorteoId: 25,
      numeroGanador: null,
      nuevaFecha: '2026-09-30T10:00:00',
      sorteosReprogramados: 1,
    }));

    await component.confirmReschedule();

    expect(reschedule).toHaveBeenCalledWith(25, null);

    expect(reschedule).toHaveBeenCalledWith(25, '00042');
  });

  it('shows the global loading indicator while finalizing the lottery', async () => {
    setActiveLottery(component);
    component.verification = winnerVerification(true);
    const lotteryService = TestBed.inject(UserLotteryService);
    spyOn(lotteryService, 'getCurrent').and.returnValue(of(activeLottery() as CurrentLottery));
    const notifications = TestBed.inject(NotificationService);
    spyOn(notifications, 'confirm').and.resolveTo(true);
    const service = TestBed.inject(SorteosService);
    const finalize = spyOn(service, 'finalizeWinner').and.returnValue(of({
      sorteoId: 25,
      numeroGanador: '00042',
      comprasArchivadas: 1,
      rascaditosGanadoresArchivados: 0,
      rascaditosCaducados: 0,
      ganador: finalizedWinner(null),
    }));

    await component.confirmFinalize();
    fixture.detectChanges();

    expect(finalize).toHaveBeenCalled();
    expect(loading.show).toHaveBeenCalledBefore(loading.hide);
    expect(loading.hide).toHaveBeenCalledTimes(1);
    const resultCard = (fixture.nativeElement as HTMLElement).querySelector('.result-card');
    expect(resultCard?.classList.contains('result-card--empty')).toBeTrue();
    expect(resultCard?.textContent?.trim()).toBe('');
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }
});

function winnerVerification(hayGanador: boolean): SorteoWinnerVerification {
  return {
    sorteoId: 25,
    numeroGanador: '00042',
    hayGanador,
    usuarioIdGanador: hayGanador ? 2 : null,
    usuarioGanador: hayGanador ? 'Carlos' : null,
    whatsAppGanador: hayGanador ? '+5266*****000' : null,
    folioCompra: hayGanador ? 'FOLIO1' : null,
    referencia: null,
  };
}

function setActiveLottery(component: EntregaPremioComponent): void {
  component.sorteo = activeLottery() as never;
}

function activeLottery(): Partial<CurrentLottery> {
  return {
    id: 25,
    nombre: 'Sorteo actual',
    imagen1: '',
    fecha: '2026-09-23T10:00:00',
    precioBoleto: 10,
    precioPorMil: 5,
    cantidadBoletos: 60000,
    estado: 'en_proceso',
    ventaDisponible: false,
    porcentajeVenta: 100,
    porcentajeMinimoVenta: 85,
    ventaMinimaAlcanzada: true,
  };
}

function finalizedWinner(
  referencia: FinalizedLotteryWinner['referencia']
): FinalizedLotteryWinner {
  return {
    sorteoId: 25,
    sorteoNombre: 'Sorteo histórico',
    numeroGanador: '042',
    usuarioGanador: 'Carlos',
    folioCompra: 'FOLIO1',
    fechaFinalizacion: '2026-09-23T12:00:00',
    referencia,
  };
}



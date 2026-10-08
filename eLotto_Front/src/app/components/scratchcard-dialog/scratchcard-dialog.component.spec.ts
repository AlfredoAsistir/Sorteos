import { ComponentFixture, fakeAsync, flushMicrotasks, TestBed, tick } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import {
  ScratchcardRevealResponse,
  ScratchcardSymbol,
} from '../../core/scratchcards/scratchcard.models';
import { ScratchcardService } from '../../services/scratchcard.service';
import { WalletService } from '../../services/wallet.service';
import { ScratchcardDialogComponent } from './scratchcard-dialog.component';

describe('ScratchcardDialogComponent', () => {
  let fixture: ComponentFixture<ScratchcardDialogComponent>;
  let component: ScratchcardDialogComponent;
  let scratchcards: jasmine.SpyObj<ScratchcardService>;
  let wallet: jasmine.SpyObj<WalletService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ScratchcardDialogComponent>>;

  const winningMatrix: ScratchcardSymbol[] = [
    'star', 'star', 'star',
    'gift', 'money', 'ticket',
    'money', 'gift', 'trophy',
  ];
  const losingMatrix: ScratchcardSymbol[] = [
    'money', 'star', 'gift',
    'trophy', 'ticket', 'crown',
    'star', 'gift', 'trophy',
  ];
  const winner: ScratchcardRevealResponse = {
    id: 7,
    folio: 'WIN123',
    revelado: true,
    reveladoAhora: true,
    esGanador: true,
    importePremio: 100,
    premioPosible: 100,
    fechaRevelado: '2026-08-31T12:00:00Z',
    saldoActual: 650,
    matrizResultado: winningMatrix,
    lineaGanadora: 'row:0',
  };
  const loser: ScratchcardRevealResponse = {
    ...winner,
    esGanador: false,
    importePremio: null,
    saldoActual: 550,
    matrizResultado: losingMatrix,
    lineaGanadora: null,
  };

  beforeEach(async () => {
    scratchcards = jasmine.createSpyObj<ScratchcardService>('ScratchcardService', [
      'reveal',
      'start',
    ]);
    wallet = jasmine.createSpyObj<WalletService>('WalletService', [
      'setBalanceFromServer',
      'showCredit',
      'formatAmount',
    ]);
    dialogRef = jasmine.createSpyObj<MatDialogRef<ScratchcardDialogComponent>>(
      'MatDialogRef',
      ['close']
    );
    wallet.formatAmount.and.returnValue('$100.00');
    scratchcards.start.and.returnValue(of({
      id: 7,
      folio: 'WIN123',
      premioPosible: 100,
      matrizResultado: winningMatrix,
    }));
    await TestBed.configureTestingModule({
      imports: [ScratchcardDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: {
          scratchcard: {
            id: 7,
            folio: 'WIN123',
            revelado: false,
            fechaGeneracion: '2026-08-31T11:00:00Z',
          },
          hasNext: true,
          autoPlay: false,
        } },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: ScratchcardService, useValue: scratchcards },
        { provide: WalletService, useValue: wallet },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['show']) },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ScratchcardDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('does not reveal merely by opening the dialog', () => {
    expect(scratchcards.reveal).not.toHaveBeenCalled();
    expect(component.state).toBe('ready');
    expect(fixture.nativeElement.textContent).not.toContain('Revelar resultado');
  });

  it('loads figures and the possible prize when scratching starts without revealing financially', async () => {
    component.onScratchStarted();
    await fixture.whenStable();
    expect(scratchcards.start).toHaveBeenCalledTimes(1);
    expect(scratchcards.reveal).not.toHaveBeenCalled();
    expect(component.symbolAt(0)).toBe('star');
    expect(component.possiblePrize()).toBe(100);
    expect(component.state).toBe('scratching');
  });

  it('keeps playing while at least one winning line remains possible', async () => {
    component.onScratchStarted();
    await fixture.whenStable();
    component.onZoneRevealed('cell-0');
    component.onZoneRevealed('cell-1');
    await fixture.whenStable();
    expect(scratchcards.reveal).not.toHaveBeenCalled();
    expect(component.visualComplete).toBeFalse();
  });

  it('detects a winning line and reveals automatically after its third cell', async () => {
    scratchcards.reveal.and.returnValue(of(winner));
    component.onScratchStarted();
    await fixture.whenStable();
    [0, 1, 2].forEach(index => component.onZoneRevealed('cell-' + index));
    await fixture.whenStable();
    expect(scratchcards.reveal).toHaveBeenCalledTimes(1);
    expect(component.visualComplete).toBeTrue();
    expect(component.forceReveal).toBeTrue();
  });

  it('detects a losing board before the ninth cell when no line can still win', async () => {
    scratchcards.start.and.returnValue(of({
      id: 7,
      folio: 'WIN123',
      premioPosible: 100,
      matrizResultado: losingMatrix,
    }));
    scratchcards.reveal.and.returnValue(of(loser));
    component.onScratchStarted();
    await fixture.whenStable();
    for (let index = 0; index < 8; index++) {
      component.onZoneRevealed('cell-' + index);
    }
    await fixture.whenStable();
    expect(scratchcards.reveal).toHaveBeenCalledTimes(1);
    expect(component.visualComplete).toBeTrue();
    expect(component.forceReveal).toBeTrue();
  });

  it('sends only one request when several thresholds complete the decision together', async () => {
    scratchcards.reveal.and.returnValue(of(winner));
    component.onScratchStarted();
    await fixture.whenStable();
    for (let index = 0; index < 9; index++) {
      component.onZoneRevealed('cell-' + index);
    }
    await fixture.whenStable();
    expect(scratchcards.reveal).toHaveBeenCalledTimes(1);
  });

  it('uses the server balance, shows the credit and highlights the winning line', async () => {
    scratchcards.reveal.and.returnValue(of(winner));
    component.onScratchStarted();
    await fixture.whenStable();
    [0, 1, 2].forEach(index => component.onZoneRevealed('cell-' + index));
    await fixture.whenStable();
    expect(wallet.setBalanceFromServer).toHaveBeenCalledOnceWith(650);
    expect(wallet.showCredit).toHaveBeenCalledOnceWith(100);
    expect(component.isWinningCell(0)).toBeTrue();
    expect(component.isWinningCell(1)).toBeTrue();
    expect(component.isWinningCell(2)).toBeTrue();
    expect(component.visualComplete).toBeTrue();
  });

  it('does not animate credit for a losing result', async () => {
    scratchcards.start.and.returnValue(of({
      id: 7,
      folio: 'WIN123',
      premioPosible: 100,
      matrizResultado: losingMatrix,
    }));
    scratchcards.reveal.and.returnValue(of(loser));
    component.onScratchStarted();
    await fixture.whenStable();
    for (let index = 0; index < 9; index++) {
      component.onZoneRevealed('cell-' + index);
    }
    await fixture.whenStable();
    expect(wallet.setBalanceFromServer).toHaveBeenCalledOnceWith(550);
    expect(wallet.showCredit).not.toHaveBeenCalled();
    expect(component.visualComplete).toBeTrue();
  });

  it('shows persisted history without repeating a new-credit effect', async () => {
    scratchcards.reveal.and.returnValue(of({ ...winner, reveladoAhora: false }));
    component.onScratchStarted();
    await fixture.whenStable();
    [0, 1, 2].forEach(index => component.onZoneRevealed('cell-' + index));
    await fixture.whenStable();
    expect(wallet.setBalanceFromServer).toHaveBeenCalledOnceWith(650);
    expect(wallet.showCredit).not.toHaveBeenCalled();
    expect(component.celebrationActive).toBeFalse();
  });

  it('opens a historical winner without calling start or reveal', () => {
    (wallet as unknown as { balance: () => number }).balance = () => 650;
    component.data.historyMode = true;
    component.data.historicalResult = {
      id: 7,
      folio: 'WIN123',
      importePremio: 100,
      fechaRevelado: '2026-08-31T12:00:00Z',
      matrizResultado: winningMatrix,
      lineaGanadora: 'row:0',
    };

    fixture.destroy();
    fixture = TestBed.createComponent(ScratchcardDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(scratchcards.start).not.toHaveBeenCalled();
    expect(scratchcards.reveal).not.toHaveBeenCalled();
    expect(wallet.setBalanceFromServer).not.toHaveBeenCalled();
    expect(wallet.showCredit).not.toHaveBeenCalled();
    expect(component.visualComplete).toBeTrue();
    expect(component.forceReveal).toBeTrue();
    expect(component.isWinningCell(0)).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Rascadito ganador');
    const layers = fixture.nativeElement.querySelectorAll('.scratch-layer');
    expect(layers.length).toBe(10);
    expect(Array.from(layers).every((layer: Element) => layer.classList.contains('scratch-layer--revealed'))).toBeTrue();
  });
  it('allows a safe retry after an HTTP error', async () => {
    scratchcards.reveal.and.returnValues(
      throwError(() => new Error('Sin conexión')),
      of(winner)
    );
    component.onScratchStarted();
    await fixture.whenStable();
    [0, 1, 2].forEach(index => component.onZoneRevealed('cell-' + index));
    await fixture.whenStable();
    expect(component.state).toBe('error');
    expect(component.response).toBeNull();
    await component.retryResult();
    expect(scratchcards.reveal).toHaveBeenCalledTimes(2);
    expect(component.visualComplete).toBeTrue();
  });

  it('returns the next action after a completed result', async () => {
    scratchcards.reveal.and.returnValue(of(winner));
    component.onScratchStarted();
    await fixture.whenStable();
    [0, 1, 2].forEach(index => component.onZoneRevealed('cell-' + index));
    await fixture.whenStable();
    component.next();
    expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
      scratchcardId: 7,
      revealed: true,
      openNext: true,
    }));
  });
  it('uses speed 1 to reveal the first automatic cell twice as fast', fakeAsync(() => {
    scratchcards.reveal.and.returnValue(of(winner));

    component.toggleAutoPlay();
    component.setAutoPlaySpeed(1);
    flushMicrotasks();
    fixture.detectChanges();

    expect(component.autoPlaySpeed).toBe(1);
    expect(fixture.nativeElement.querySelector('.auto-play-speed__button--active')?.textContent.trim()).toBe('1');
    tick(324);
    expect(component.shouldForceRevealCell(0)).toBeFalse();
    tick(1);
    flushMicrotasks();
    expect(component.shouldForceRevealCell(0)).toBeTrue();
  }));

  it('uses speed 1.5 as the fastest automatic option', fakeAsync(() => {
    scratchcards.reveal.and.returnValue(of(winner));

    component.toggleAutoPlay();
    component.setAutoPlaySpeed(1.5);
    flushMicrotasks();

    tick(216);
    expect(component.shouldForceRevealCell(0)).toBeFalse();
    tick(1);
    flushMicrotasks();
    expect(component.shouldForceRevealCell(0)).toBeTrue();
  }));

  it('waits three seconds before advancing when speed 1 is selected', fakeAsync(() => {
    scratchcards.reveal.and.returnValue(of(winner));

    component.toggleAutoPlay();
    component.setAutoPlaySpeed(1);
    flushMicrotasks();
    tick(1175);
    flushMicrotasks();

    expect(component.visualComplete).toBeTrue();
    expect(component.nextCountdown).toBe(3);
    tick(3000);
    expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
      openNext: true,
      autoPlaySpeed: 1,
    }));
  }));

  it('waits two seconds before advancing when speed 1.5 is selected', fakeAsync(() => {
    scratchcards.reveal.and.returnValue(of(winner));

    component.toggleAutoPlay();
    component.setAutoPlaySpeed(1.5);
    flushMicrotasks();
    tick(1066);
    flushMicrotasks();

    expect(component.visualComplete).toBeTrue();
    expect(component.nextCountdown).toBe(2);
    tick(2000);
    expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
      openNext: true,
      autoPlaySpeed: 1.5,
    }));
  }));

  it('reveals cells automatically and advances after the four-second countdown', fakeAsync(() => {
    scratchcards.reveal.and.returnValue(of(winner));

    component.toggleAutoPlay();
    flushMicrotasks();
    expect(component.autoPlayActive).toBeTrue();
    expect(component.autoPlaySpeed).toBe(0.5);
    expect(component.autoPrizeRevealed).toBeTrue();

    tick(650);
    flushMicrotasks();
    expect(component.shouldForceRevealCell(0)).toBeTrue();

    tick(1700);
    flushMicrotasks();
    fixture.detectChanges();
    expect(component.visualComplete).toBeTrue();
    expect(component.nextCountdown).toBe(4);
    expect(fixture.nativeElement.querySelector('.winner-message')).toBeTruthy();

    tick(1000);
    expect(component.nextCountdown).toBe(3);
    tick(3000);
    expect(dialogRef.close).toHaveBeenCalledWith(jasmine.objectContaining({
      scratchcardId: 7,
      openNext: true,
      autoPlay: true,
      autoPlaySpeed: 0.5,
    }));
  }));
});

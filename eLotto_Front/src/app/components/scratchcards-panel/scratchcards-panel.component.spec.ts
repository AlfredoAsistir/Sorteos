import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { provideRouter } from '@angular/router';
import { NotificationService } from '../../core/notifications/notification.service';
import { ScratchcardService } from '../../services/scratchcard.service';
import { ScratchcardsPanelComponent } from './scratchcards-panel.component';

describe('ScratchcardsPanelComponent', () => {
  let fixture: ComponentFixture<ScratchcardsPanelComponent>;
  let component: ScratchcardsPanelComponent;
  const service = jasmine.createSpyObj<ScratchcardService>('ScratchcardService', [
    'getScratchcards',
  ]);
  const dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

  beforeEach(async () => {
    service.getScratchcards.calls.reset();
    dialog.open.calls.reset();
    service.getScratchcards.and.returnValue(of({
      pendientes: 1,
      rascaditos: [{
        id: 4,
        folio: 'SAFE123',
        revelado: false,
        fechaGeneracion: '2026-08-31T12:00:00Z',
      }],
    }));
    await TestBed.configureTestingModule({
      imports: [ScratchcardsPanelComponent],
      providers: [
        provideRouter([]),
        { provide: ScratchcardService, useValue: service },
        { provide: MatDialog, useValue: dialog },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['show']) },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ScratchcardsPanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads pending scratchcards without displaying hidden result fields', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(service.getScratchcards).toHaveBeenCalledTimes(1);
    expect(text).toContain('Tienes 1 rascadito disponible');
    expect(text).not.toContain('EsGanador');
    expect(text).not.toContain('ImportePremio');
  });

  it('does not open or reveal a scratchcard while loading the list', () => {
    expect(dialog.open).not.toHaveBeenCalled();
  });
  it('opens the next pending scratchcard without returning to the panel', async () => {
    const first = {
      id: 4,
      folio: 'SAFE123',
      revelado: false,
      fechaGeneracion: '2026-08-31T12:00:00Z',
    };
    const second = {
      id: 5,
      folio: 'NEXT456',
      revelado: false,
      fechaGeneracion: '2026-08-31T12:01:00Z',
    };
    component.scratchcards = [first, second];
    component.pendingCount = 2;
    const componentDialog = (component as unknown as { dialog: MatDialog }).dialog;
    const openSpy = spyOn(componentDialog, 'open');
    openSpy.and.returnValues(
      { afterClosed: () => of({
        scratchcardId: 4,
        revealed: true,
        revealedAt: '2026-08-31T12:05:00Z',
        openNext: true,
        autoPlay: true,
        autoPlaySpeed: 1.5,
      }) } as never,
      { afterClosed: () => of(undefined) } as never
    );

    await component.openScratchcard(first);

    expect(openSpy).toHaveBeenCalledTimes(2);
    const calls = openSpy.calls.allArgs();
    const firstConfig = calls[0][1] as { data: { hasNext: boolean; autoPlay: boolean } };
    const secondConfig = calls[1][1] as {
      data: { hasNext: boolean; autoPlay: boolean; autoPlaySpeed: number; scratchcard: { id: number } };
    };
    expect(firstConfig.data.hasNext).toBeTrue();
    expect(firstConfig.data.autoPlay).toBeFalse();
    expect(secondConfig.data.scratchcard.id).toBe(5);
    expect(secondConfig.data.hasNext).toBeFalse();
    expect(secondConfig.data.autoPlay).toBeTrue();
    expect(secondConfig.data.autoPlaySpeed).toBe(1.5);
    expect(component.pendingCount).toBe(1);
  });
  it('keeps revealed scratchcards in API order when lotteries use different time zones', () => {
    component.scratchcards = [
      { id: 10, folio: 'CANCUN10', revelado: true,
        fechaGeneracion: '2026-09-29T11:00:00', fechaRevelado: '2026-09-29T12:00:00' },
      { id: 11, folio: 'TIJUANA11', revelado: true,
        fechaGeneracion: '2026-09-29T08:00:00', fechaRevelado: '2026-09-29T09:30:00' },
      { id: 12, folio: 'PENDING12', revelado: false,
        fechaGeneracion: '2026-09-29T09:40:00' },
    ];
    component.pendingCount = 1;

    (component as unknown as { applyRevealResult: (result: unknown) => void })
      .applyRevealResult({
        scratchcardId: 12,
        revealed: true,
        revealedAt: '2026-09-29T09:45:00',
      });

    expect(component.scratchcards.map(item => item.id)).toEqual([12, 11, 10]);
  });
  it('renders no promotional section when there are no pending scratchcards', () => {
    component.pendingCount = 0;
    component.scratchcards = [{
      id: 4,
      folio: 'SAFE123',
      revelado: true,
      fechaGeneracion: '2026-08-31T12:00:00Z',
      fechaRevelado: '2026-08-31T12:05:00Z',
    }];

    (component as unknown as { changeDetector: { detectChanges: () => void } })
      .changeDetector.detectChanges();

    expect(fixture.nativeElement.querySelector('.scratchcards-panel')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Rascaditos promocionales');
    expect(fixture.nativeElement.textContent).not.toContain('Resultados recientes');
  });
});

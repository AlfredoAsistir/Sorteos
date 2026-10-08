import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ReferralProgramSettingsService } from '../../services/referral-program-settings.service';
import { UserLotteryService } from '../../services/user-lottery.service';
import { FrequentlyAskedQuestionsComponent } from './frequently-asked-questions.component';

describe('FrequentlyAskedQuestionsComponent', () => {
  let fixture: ComponentFixture<FrequentlyAskedQuestionsComponent>;
  let component: FrequentlyAskedQuestionsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FrequentlyAskedQuestionsComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ReferralProgramSettingsService,
          useValue: {
            getBenefits: () => of({
              isActive: true,
              depositRewardPercentage: 10,
              maxRewardedDeposits: 5,
              winnerCashRewardAmount: 5000,
              minimumConfirmedTickets: 200,
            }),
          },
        },
        {
          provide: UserLotteryService,
          useValue: {
            getCurrent: () => of({
              id: 7,
              nombre: 'Gran Sorteo de Verano',
              fecha: '2026-09-30T20:00:00',
              zonaHoraria: 'Hermosillo',
              porcentajeMinimoVenta: 85,
            }),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FrequentlyAskedQuestionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('prioritizes the referral program', () => {
    expect(component.groups[0].id).toBe('referidos');
    expect(fixture.nativeElement.textContent).toContain('Si tu referido gana, tú también ganas');
  });

  it('shows the current referral benefits returned by the server', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('10% de bono para ti y 10% para tu invitado');
    expect(text).toContain('$5,000.00 MXN en efectivo (Transferencia SPEI)');
    expect(text).toContain('transferencia bancaria SPEI a una cuenta a tu nombre');
    expect(text).toContain('200 boletos confirmados');
  });

  it('explains that Scratchcard prizes are credited to the balance', () => {
    const scratchcards = component.groups.find(group => group.id === 'rascaditos');
    expect(scratchcards?.questions.some(item => item.answer.includes('directamente al saldo'))).toBeTrue();
  });

  it('shows the current lottery rules returned by the server', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Gran Sorteo de Verano');
    expect(text).toContain('(Hermosillo)');
    expect(text).toContain('85% de los boletos');
    expect(text).toContain('primer lugar del sorteo de la Lotería Nacional');
    expect(text).toContain('transferencia bancaria SPEI en un plazo máximo de 48 horas');
    expect(text).toContain('cuenta bancaria a su nombre');
  });

  it('highlights the transparency explanation in its own help category', () => {
    const section = fixture.nativeElement.querySelector('#transparencia') as HTMLElement;
    expect(section.querySelector('h2')?.textContent).toContain('Boletos no vendidos: transparencia antes del sorteo');
    expect(section.querySelector('.transparency-explainer__motto h3')?.textContent)
      .toContain('Primero se publica. Después se sortea.');
    expect(section.querySelector('.transparency-explainer__closing span')?.textContent).toBe('TRANSPARENCIA');
  });

  it('filters questions ignoring accents and case', () => {
    component.searchTerm = 'DEPOSITOS';
    const results = component.filteredGroups;
    expect(results.some(group => group.id === 'depositos')).toBeTrue();
  });

  it('scrolls the selected category into view', fakeAsync(() => {
    const target = fixture.nativeElement.querySelector('#rascaditos') as HTMLElement;
    const scrollSpy = spyOn(target, 'scrollIntoView');

    component.scrollToGroup('rascaditos');
    tick();

    expect(scrollSpy).toHaveBeenCalledWith({
      behavior: 'smooth',
      block: 'start',
    });
  }));
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { NotificationService } from '../../core/notifications/notification.service';
import { MyReferrals } from '../../core/referrals/my-referrals.models';
import { LoadingService } from '../../services/loading.service';
import { MyReferralsService } from '../../services/my-referrals.service';
import { MyReferralsComponent } from './my-referrals.component';

describe('MyReferralsComponent', () => {
  function createFixture(data: MyReferrals): ComponentFixture<MyReferralsComponent> {
    const service = jasmine.createSpyObj<MyReferralsService>('MyReferralsService', ['get']);
    service.get.and.returnValue(of(data));
    TestBed.configureTestingModule({
      imports: [MyReferralsComponent],
      providers: [
        provideRouter([]),
        { provide: MyReferralsService, useValue: service },
        { provide: LoadingService, useValue: jasmine.createSpyObj('LoadingService', ['show', 'hide']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['show']) },
      ],
    });

    const fixture = TestBed.createComponent(MyReferralsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders summary, direct referrals, historical bonuses and cash prizes', () => {
    const fixture = createFixture({
      directReferralsCount: 2,
      depositRewardsTotal: 250,
      pendingCashRewardsTotal: 5000,
      paidCashRewardsTotal: 3000,
      referrals: [
        {
          name: 'Carlos',
          registeredAt: '2026-09-20T12:00:00',
          depositRewardsCount: 2,
          depositRewardsTotal: 250,
          winnerCashRewardsCount: 2,
        },
        {
          name: 'Pedro',
          registeredAt: '2026-09-21T12:00:00',
          depositRewardsCount: 0,
          depositRewardsTotal: 0,
          winnerCashRewardsCount: 0,
        },
      ],
      depositRewards: [{
        referredUserName: 'Carlos',
        createdAt: '2026-09-22T12:00:00',
        depositAmount: 1000,
        percentageApplied: 10,
        rewardAmount: 100,
      }],
      cashRewards: [
        {
          winnerUserName: 'Carlos',
          lotteryName: 'Sorteo pendiente',
          createdAt: '2026-09-22T12:00:00',
          requiredTickets: 100,
          actualTickets: 125,
          rewardAmount: 5000,
          status: 1,
          paidAt: null,
        },
        {
          winnerUserName: 'Carlos',
          lotteryName: 'Sorteo pagado',
          createdAt: '2026-09-23T12:00:00',
          requiredTickets: 50,
          actualTickets: 70,
          rewardAmount: 3000,
          status: 2,
          paidAt: '2026-09-23T18:00:00',
        },
      ],
    });

    const text = fixture.nativeElement.textContent;
    const element = fixture.nativeElement as HTMLElement;
    expect(text).toContain('Carlos');
    expect(text).toContain('Pedro');
    expect(text).toContain('10%');
    expect(text).not.toContain('Sorteo pendiente');
    expect(text).not.toContain('Sorteo pagado');
    expect(text).not.toContain('Pagado el');
    expect(text).toContain('$5,000.00');
    expect(text).toContain('$3,000.00');
    expect(text).toContain('0 bonos generados');
    expect(text).not.toContain('en bonos');
    expect(text).toContain('Premios por referidos');
    expect(text).not.toContain('Premios pendientes');
    expect(text).not.toContain('Premios pagados');
    expect(text).not.toContain('12:00');
    expect(element.querySelectorAll('.summary article').length).toBe(3);
    expect(element.querySelector('.cash-reward__trophy')).toBeNull();
  });

  it('shows a friendly empty state linked to Invitar a un amigo', () => {
    const fixture = createFixture({
      directReferralsCount: 0,
      depositRewardsTotal: 0,
      pendingCashRewardsTotal: 0,
      paidCashRewardsTotal: 0,
      referrals: [],
      depositRewards: [],
      cashRewards: [],
    });

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Aún no tienes referidos');
    const link = element.querySelector<HTMLAnchorElement>('a[href="/starter/invitar-amigo"]');
    expect(link).not.toBeNull();
  });

});

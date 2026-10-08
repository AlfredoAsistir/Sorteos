import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MyReferralsService } from './my-referrals.service';

describe('MyReferralsService', () => {
  let service: MyReferralsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MyReferralsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the authenticated user referrals without sending a user id', () => {
    service.get().subscribe();

    const request = http.expectOne(`${environment.apiUrl}/api/my-referrals`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({
      directReferralsCount: 0,
      depositRewardsTotal: 0,
      pendingCashRewardsTotal: 0,
      paidCashRewardsTotal: 0,
      referrals: [],
      depositRewards: [],
      cashRewards: [],
    });
  });
});

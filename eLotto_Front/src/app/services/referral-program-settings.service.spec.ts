import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import {
  ReferralProgramBenefits,
  ReferralProgramSettings,
  UpdateReferralProgramSettingsRequest,
} from '../core/referrals/referral-program-settings.models';
import { ReferralProgramSettingsService } from './referral-program-settings.service';

describe('ReferralProgramSettingsService', () => {
  let service: ReferralProgramSettingsService;
  let http: HttpTestingController;
  const apiUrl = `${environment.apiUrl}/ReferralProgramSettings`;
  const benefitsApiUrl = `${environment.apiUrl}/ReferralProgramBenefits`;
  const settings: ReferralProgramSettings = {
    id: 1,
    isActive: true,
    depositRewardPercentage: 10,
    maxRewardedDeposits: 5,
    winnerCashRewardAmount: 5000,
    minimumConfirmedTickets: 100,
    updatedAt: '2026-09-23T12:00:00',
    rowVersion: 'AAAAAAAAAAE=',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ReferralProgramSettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the singleton settings', () => {
    service.get().subscribe(result => expect(result).toEqual(settings));

    const request = http.expectOne(apiUrl);
    expect(request.request.method).toBe('GET');
    request.flush(settings);
  });

  it('reads the public benefits without administrative fields', () => {
    const benefits: ReferralProgramBenefits = {
      isActive: true,
      depositRewardPercentage: 10,
      maxRewardedDeposits: 5,
      winnerCashRewardAmount: 5000,
      minimumConfirmedTickets: 200,
    };

    service.getBenefits().subscribe(result => expect(result).toEqual(benefits));

    const request = http.expectOne(benefitsApiUrl);
    expect(request.request.method).toBe('GET');
    request.flush(benefits);
  });

  it('updates the singleton including its RowVersion', () => {
    const update: UpdateReferralProgramSettingsRequest = {
      isActive: false,
      depositRewardPercentage: 5,
      maxRewardedDeposits: 3,
      winnerCashRewardAmount: 3000,
      minimumConfirmedTickets: 150,
      rowVersion: settings.rowVersion,
    };

    service.update(update).subscribe(result => expect(result.isActive).toBeFalse());

    const request = http.expectOne(apiUrl);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(update);
    request.flush({ ...settings, ...update, rowVersion: 'AAAAAAAAAAI=' });
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ReferralInvitationService } from './referral-invitation.service';

describe('ReferralInvitationService', () => {
  let service: ReferralInvitationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ReferralInvitationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the authenticated user referral code', () => {
    service.get().subscribe(result => expect(result.referralCode).toBe('ABC12345'));

    const request = http.expectOne(`${environment.apiUrl}/ReferralInvitation`);
    expect(request.request.method).toBe('GET');
    request.flush({ referralCode: 'ABC12345' });
  });
});

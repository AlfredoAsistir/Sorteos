import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { UserPrizeService } from './user-prize.service';

describe('UserPrizeService', () => {
  let service: UserPrizeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(UserPrizeService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the authenticated historical prize list', () => {
    service.getHistory().subscribe(response => {
      expect(response.totalPremios).toBe(2);
      expect(response.premios[0].tipo).toBe('rascadito');
    });

    const request = http.expectOne(`${environment.apiUrl}/api/user-prizes`);
    expect(request.request.method).toBe('GET');
    request.flush({
      totalPremios: 2,
      totalSorteos: 1,
      totalRascaditos: 1,
      totalReferidos: 0,
      importeRascaditos: 50,
      importeReferidos: 0,
      premios: [
        { tipo: 'rascadito' },
        { tipo: 'sorteo' },
      ],
    });
  });
});

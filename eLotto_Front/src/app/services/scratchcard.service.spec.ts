import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { ScratchcardService } from './scratchcard.service';

describe('ScratchcardService', () => {
  let service: ScratchcardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ScratchcardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the safe scratchcard list', () => {
    service.getScratchcards().subscribe(response => {
      expect(response.pendientes).toBe(1);
      expect(response.rascaditos[0].revelado).toBeFalse();
    });

    const request = http.expectOne(`${environment.apiUrl}/api/user-scratchcards`);
    expect(request.request.method).toBe('GET');
    request.flush({
      pendientes: 1,
      rascaditos: [{
        id: 10,
        folio: 'ABC123',
        revelado: false,
        fechaGeneracion: '2026-08-31T12:00:00Z',
      }],
    });
  });

  it('uses the authenticated visual-start endpoint', () => {
    service.start(10).subscribe();
    const request = http.expectOne(
      environment.apiUrl + '/api/user-scratchcards/10/start'
    );
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 10,
      folio: 'ABC123',
      premioPosible: 100,
      matrizResultado: [],
    });
  });
  it('uses the secure reveal endpoint', () => {
    service.reveal(10).subscribe();
    const request = http.expectOne(`${environment.apiUrl}/api/user-scratchcards/10/reveal`);
    expect(request.request.method).toBe('POST');
    request.flush({});
  });
});

import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionService } from '../core/auth/session.service';
import { CoreService } from './core.service';
import { environment } from '../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    service = TestBed.inject(AuthService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('revokes the captured token and clears the local session on logout', () => {
    const session = TestBed.inject(SessionService);
    const http = TestBed.inject(HttpTestingController);
    session.saveLogin({ token: 'test-token', userId: 5 }, false);

    service.logout();

    const request = http.expectOne(`${environment.apiUrl}/auth/logout`);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    expect(session.token).toBeNull();
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('applies the preference returned by login', () => {
    service.createSession({ token: 'test-token', userId: 5, isDark: false }, false);

    expect(TestBed.inject(CoreService).getOptions().theme).toBe('light');
    expect(TestBed.inject(SessionService).snapshot?.isDark).toBeFalse();
    TestBed.inject(SessionService).clear();
  });

  it('reads and saves the theme through the authenticated API', () => {
    const http = TestBed.inject(HttpTestingController);
    service.getTheme().subscribe(response => expect(response.isDark).toBeFalse());
    const get = http.expectOne(`${environment.apiUrl}/auth/theme`);
    expect(get.request.method).toBe('GET');
    get.flush({ isDark: false });

    service.updateTheme(true).subscribe();
    const put = http.expectOne(`${environment.apiUrl}/auth/theme`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ isDark: true });
    put.flush(null, { status: 204, statusText: 'No Content' });
  });
});

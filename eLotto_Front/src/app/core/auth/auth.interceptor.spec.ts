import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, flushMicrotasks, TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { SessionService } from './session.service';

function token(sessionId: string): string {
  const payload = btoa(JSON.stringify({ jti: sessionId, exp: Math.floor(Date.now() / 1000) + 3600 }))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `header.${payload}.signature`;
}

describe('authInterceptor session replacement', () => {
  let http: HttpClient;
  let requests: HttpTestingController;
  let session: SessionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
  });

  afterEach(() => {
    requests.verify();
    session.clear();
  });

  it('ends the current session when the backend rejects its token', fakeAsync(() => {
    const currentToken = token('11111111-1111-4111-8111-111111111111');
    session.saveLogin({ token: currentToken, userId: 1001 }, false);
    http.get(`${environment.apiUrl}/auth/session`).subscribe({ error: () => undefined });
    flushMicrotasks();

    const request = requests.expectOne(`${environment.apiUrl}/auth/session`);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${currentToken}`);
    request.flush({ code: 'session_replaced' }, { status: 401, statusText: 'Unauthorized' });
    expect(session.token).toBeNull();
  }));

  it('does not clear a newer login when an old request fails late', fakeAsync(() => {
    const oldToken = token('22222222-2222-4222-8222-222222222222');
    const newToken = token('33333333-3333-4333-8333-333333333333');
    session.saveLogin({ token: oldToken, userId: 1002 }, false);
    http.get(`${environment.apiUrl}/auth/session`).subscribe({ error: () => undefined });
    flushMicrotasks();
    const request = requests.expectOne(`${environment.apiUrl}/auth/session`);

    session.saveLogin({ token: newToken, userId: 1002 }, false);
    request.flush({ code: 'session_replaced' }, { status: 401, statusText: 'Unauthorized' });
    expect(session.token).toBe(newToken);
  }));

  it('sends the current token with both theme requests', fakeAsync(() => {
    const currentToken = token('44444444-4444-4444-8444-444444444444');
    session.saveLogin({ token: currentToken, userId: 1003 }, false);
    const url = `${environment.apiUrl}/auth/theme`;

    http.get(url).subscribe();
    flushMicrotasks();
    const getRequest = requests.expectOne(url);
    expect(getRequest.request.headers.get('Authorization')).toBe(`Bearer ${currentToken}`);
    getRequest.flush({ isDark: true });

    http.put(url, { isDark: false }).subscribe();
    flushMicrotasks();
    const putRequest = requests.expectOne(url);
    expect(putRequest.request.headers.get('Authorization')).toBe(`Bearer ${currentToken}`);
    expect(putRequest.request.body).toEqual({ isDark: false });
    putRequest.flush(null, { status: 204, statusText: 'No Content' });
  }));
});

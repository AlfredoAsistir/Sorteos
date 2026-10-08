import { TestBed } from '@angular/core/testing';
import { fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Router } from '@angular/router';
import { AppComponent } from './app.component';
import { APP_BRANDING } from './config/branding.config';
import { SessionService } from './core/auth/session.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it(`should use the configured application title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance.title).toBe(APP_BRANDING.name);
  });

  it('redirects when a stored token has expired', fakeAsync(() => {
    const session = TestBed.inject(SessionService);
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    const payload = btoa(JSON.stringify({ jti: crypto.randomUUID(), exp: 1 }));
    session.saveLogin({ token: `header.${payload}.signature`, userId: 9999 }, false);

    TestBed.createComponent(AppComponent);
    flushMicrotasks();

    expect(session.token).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/authentication/login'], { replaceUrl: true });
  }));
});
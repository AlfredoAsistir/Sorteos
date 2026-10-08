import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { isInstalledMobileApp } from '../pwa/app-installation.utils';
import { authGuard } from './auth.guard';
import { guestGuard } from './guest.guard';
import { SessionService } from './session.service';

interface PlatformCase {
  name: string;
  userAgent: string;
  standalone: boolean;
  iosStandalone?: boolean;
  defaultKeepSession: boolean;
}

const platforms: PlatformCase[] = [
  { name: 'web de escritorio', userAgent: 'Mozilla/5.0 (Windows NT 10.0)', standalone: false, defaultKeepSession: false },
  { name: 'web móvil Android', userAgent: 'Mozilla/5.0 (Linux; Android 14) Chrome/140.0', standalone: false, defaultKeepSession: false },
  { name: 'web móvil iOS', userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) Safari/604.1', standalone: false, defaultKeepSession: false },
  { name: 'app instalada Android', userAgent: 'Mozilla/5.0 (Linux; Android 14) Chrome/140.0', standalone: true, defaultKeepSession: true },
  { name: 'app instalada iOS', userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) Safari/604.1', standalone: false, iosStandalone: true, defaultKeepSession: true },
];

const sessionKeys = ['token', 'expire', 'languaje', 'rolId', 'rolName', 'user', 'userId', 'keepSession', 'isDark'];

function token(expiresInSeconds: number): string {
  const payload = btoa(JSON.stringify({ jti: crypto.randomUUID(), exp: Math.floor(Date.now() / 1000) + expiresInSeconds }))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `header.${payload}.signature`;
}

function closeTab(session: SessionService): void {
  (session as unknown as { channel: BroadcastChannel | null }).channel?.close();
}

describe('Mantener sesión en web y app instalada', () => {
  beforeEach(() => {
    for (const key of sessionKeys) {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    }
  });

  afterEach(() => {
    for (const key of sessionKeys) {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    }
  });

  for (const platform of platforms) {
    it(`${platform.name}: detecta el valor inicial de la casilla`, () => {
      const appWindow = { matchMedia: () => ({ matches: platform.standalone }) as MediaQueryList };
      const appNavigator = {
        userAgent: platform.userAgent,
        platform: '',
        maxTouchPoints: 0,
        standalone: platform.iosStandalone ?? false,
      };
      expect(isInstalledMobileApp(appWindow, appNavigator)).toBe(platform.defaultKeepSession);
    });

    it(`${platform.name}: recupera la sesión marcada al abrir otra pestaña`, async () => {
      const first = new SessionService();
      const savedToken = token(3600);
      first.saveLogin({ token: savedToken, userId: crypto.randomUUID() }, true);
      expect(localStorage.getItem('token')).toBe(savedToken);
      closeTab(first);

      const reopened = new SessionService();
      await reopened.whenReady();
      expect(reopened.isAuthenticated()).toBeTrue();
      expect(reopened.snapshot?.keepSession).toBeTrue();

      TestBed.configureTestingModule({
        providers: [provideRouter([]), { provide: SessionService, useValue: reopened }],
      });
      const privateResult = await TestBed.runInInjectionContext(() =>
        authGuard({} as ActivatedRouteSnapshot, { url: '/starter/index' } as RouterStateSnapshot));
      const loginResult = await TestBed.runInInjectionContext(() =>
        guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
      expect(privateResult).toBeTrue();
      expect(loginResult).toEqual(TestBed.inject(Router).parseUrl('/starter/index'));
      closeTab(reopened);
    });

    it(`${platform.name}: no recupera la sesión sin marcar la casilla`, async () => {
      const first = new SessionService();
      first.saveLogin({ token: token(3600), userId: crypto.randomUUID() }, false);
      expect(localStorage.getItem('token')).toBeNull();
      expect(sessionStorage.getItem('token')).not.toBeNull();
      closeTab(first);

      for (const key of sessionKeys) sessionStorage.removeItem(key);
      const reopened = new SessionService();
      await reopened.whenReady();
      expect(reopened.isAuthenticated()).toBeFalse();
      closeTab(reopened);
    });

    it(`${platform.name}: rechaza un token vencido aunque se haya marcado la casilla`, async () => {
      const first = new SessionService();
      first.saveLogin({ token: token(-1), userId: crypto.randomUUID() }, true);
      closeTab(first);

      const reopened = new SessionService();
      await reopened.whenReady();
      expect(reopened.isAuthenticated()).toBeFalse();
      expect(localStorage.getItem('token')).toBeNull();
      closeTab(reopened);
    });
  }
});

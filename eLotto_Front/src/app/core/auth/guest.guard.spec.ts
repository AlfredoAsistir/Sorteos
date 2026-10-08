import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { routes } from '../../app.routes';
import { AuthenticationRoutes } from '../../pages/authentication/authentication.routes';
import { guestGuard } from './guest.guard';
import { SessionService } from './session.service';

describe('Persistent session entry routes', () => {
  let authenticated: boolean;

  beforeEach(() => {
    authenticated = false;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            whenReady: () => Promise.resolve(),
            isAuthenticated: () => authenticated,
          },
        },
      ],
    });
  });

  it('opens the protected area from the site root', () => {
    const root = routes.find(route => route.path === '' && route.children?.some(child => child.path === ''));
    expect(root?.children?.find(child => child.path === '')?.redirectTo).toBe('/starter/index');
  });

  it('redirects an authenticated user away from the login form', async () => {
    authenticated = true;
    const login = AuthenticationRoutes[0].children?.find(route => route.path === 'login');
    expect(login?.canActivate).toContain(guestGuard);

    const result = await TestBed.runInInjectionContext(() =>
      guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    expect(result).toEqual(TestBed.inject(Router).parseUrl('/starter/index'));
  });

  it('allows the login form without an active session', async () => {
    const result = await TestBed.runInInjectionContext(() =>
      guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    expect(result).toBeTrue();
  });
});

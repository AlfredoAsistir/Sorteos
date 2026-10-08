import { userGuard } from '../core/auth/user.guard';
import { PagesRoutes } from './pages.routes';

describe('PagesRoutes referrals', () => {
  it('exposes preguntas frecuentes as a lazy route', () => {
    const route = PagesRoutes.find(item => item.path === 'preguntas-frecuentes');
    expect(route).toBeDefined();
    expect(route?.loadComponent).toBeDefined();
    expect(route?.data?.['title']).toBe('Preguntas frecuentes');
  });

  it('protects Invitar a un amigo as a User-only lazy route', () => {
    const route = PagesRoutes.find(item => item.path === 'invitar-amigo');
    expect(route).toBeDefined();
    expect(route?.canActivate).toContain(userGuard);
    expect(route?.loadComponent).toBeDefined();
  });

  it('protects Mis referidos as a User-only lazy route', () => {
    const route = PagesRoutes.find(item => item.path === 'mis-referidos');
    expect(route).toBeDefined();
    expect(route?.canActivate).toContain(userGuard);
    expect(route?.loadComponent).toBeDefined();
  });
});

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

export const authGuard: CanActivateFn = async (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);

  await session.whenReady();
  if (session.isAuthenticated()) return true;

  return router.createUrlTree(['/authentication/login'], {
    queryParams: { returnUrl: state.url },
  });
};
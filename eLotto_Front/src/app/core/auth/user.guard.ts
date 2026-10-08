import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

export const userGuard: CanActivateFn = async () => {
  const session = inject(SessionService);
  const router = inject(Router);

  await session.whenReady();
  return session.isAuthenticated() && session.roleName === 'User'
    ? true
    : router.createUrlTree(['/starter/index']);
};

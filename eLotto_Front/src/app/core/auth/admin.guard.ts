import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

export const adminGuard: CanActivateFn = async () => {
  const session = inject(SessionService);
  const router = inject(Router);

  await session.whenReady();
  return session.isAuthenticated() && session.roleName === 'Admin'
    ? true
    : router.createUrlTree(['/starter/index']);
};
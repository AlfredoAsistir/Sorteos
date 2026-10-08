import { Routes } from '@angular/router';
import { BlankComponent } from './layouts/blank/blank.component';
import { FullComponent } from './layouts/full/full.component';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    component: FullComponent,
    children: [
      {
        path: '',
        redirectTo: '/starter/index',
        pathMatch: 'full',
      },
      {
        path: 'starter',
        canActivate: [authGuard],
        loadChildren: () =>
          import('./pages/pages.routes').then((m) => m.PagesRoutes),
      },
    ],
  },
  {
    path: '',
    component: BlankComponent,
    children: [
      {
        path: 'authentication',
        loadChildren: () =>
          import('./pages/authentication/authentication.routes').then(
            (m) => m.AuthenticationRoutes
          ),
      },
    ],
  },
  {
    path: 'p/estado/:status',
    loadComponent: () =>
      import('./pages/payment-instruction-status/payment-instruction-status.component').then(
        (m) => m.PaymentInstructionStatusComponent
      ),
  },
  {
    path: 'p/:code',
    loadComponent: () =>
      import('./pages/payment-instruction-redirect/payment-instruction-redirect.component').then(
        (m) => m.PaymentInstructionRedirectComponent
      ),
  },
  {
    path: '**',
    redirectTo: 'authentication/error',
  },
];

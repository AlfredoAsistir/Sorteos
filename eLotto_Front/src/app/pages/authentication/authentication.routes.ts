import { Routes } from '@angular/router';

import { AppErrorComponent } from './error/error.component';
import { AppSideForgotPasswordComponent } from './side-forgot-password/side-forgot-password.component';
import { AppSideLoginComponent } from './side-login/side-login.component';
import { AppSideRegisterComponent } from './side-register/side-register.component';
import { guestGuard } from '../../core/auth/guest.guard';

export const AuthenticationRoutes: Routes = [
  {
    path: '',
    children: [
      {
        path: 'error',
        component: AppErrorComponent,
      },
      {
        path: 'side-forgot-password',
        component: AppSideForgotPasswordComponent,
      },
      {
        path: 'login',
        canActivate: [guestGuard],
        component: AppSideLoginComponent,
      },
      {
        path: 'side-register',
        component: AppSideRegisterComponent,
      },
    ],
  },
];

import { Routes } from '@angular/router';
import { StarterComponent } from './starter/starter.component';
import { adminGuard } from '../core/auth/admin.guard';
import { userGuard } from '../core/auth/user.guard';

export const PagesRoutes: Routes = [
  {
    path: '',
    redirectTo: 'index',
    pathMatch: 'full',
  },
  {
    path: 'index',
    component: StarterComponent,
    data: { title: 'Inicio' },
  },
  {
    path: 'invitar-amigo',
    canActivate: [userGuard],
    loadComponent: () =>
      import('./invite-friend/invite-friend.component').then((m) => m.InviteFriendComponent),
    data: { title: 'Invitar a un amigo' },
  },
  {
    path: 'mis-referidos',
    canActivate: [userGuard],
    loadComponent: () =>
      import('./my-referrals/my-referrals.component').then((m) => m.MyReferralsComponent),
    data: { title: 'Mis referidos' },
  },
    {
    path: 'mis-boletos',
    loadComponent: () =>
      import('./mis-boletos/mis-boletos.component').then((m) => m.MisBoletosComponent),
    data: { title: 'Mis Boletos' },
  },
  {
    path: 'contacto',
    loadComponent: () =>
      import('./contacto/contacto.component').then((m) => m.ContactoComponent),
    data: { title: 'Contacto' },
  },
  {
    path: 'preguntas-frecuentes',
    loadComponent: () =>
      import('./frequently-asked-questions/frequently-asked-questions.component')
        .then((m) => m.FrequentlyAskedQuestionsComponent),
    data: { title: 'Preguntas frecuentes' },
  },
  {
    path: 'he-ganado',
    loadComponent: () =>
      import('./he-ganado/he-ganado.component').then((m) => m.HeGanadoComponent),
    data: { title: '¿He ganado?' },
  },
  {
    path: 'wallet',
    loadComponent: () =>
      import('./wallet/wallet.component').then((m) => m.WalletComponent),
    data: { title: 'Saldo' },
  },
  {
    path: 'sorteos',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./sorteos/sorteos.component').then((m) => m.SorteosComponent),
    data: { title: 'Sorteos' },
  },
  {
    path: 'entrega-premio',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./entrega-premio/entrega-premio.component').then((m) => m.EntregaPremioComponent),
    data: { title: 'Entrega del premio' },
  },
  {
    path: 'programa-referidos',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./referral-program-settings/referral-program-settings.component')
        .then((m) => m.ReferralProgramSettingsComponent),
    data: { title: 'Programa de referidos' },
  },
];

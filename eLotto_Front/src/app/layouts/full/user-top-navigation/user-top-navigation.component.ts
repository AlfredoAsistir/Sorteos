import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../../material.module';
import { APP_BRANDING } from '../../../config/branding.config';

interface UserMenuItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-user-top-navigation',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './user-top-navigation.component.html',
  styleUrl: './user-top-navigation.component.scss'
})
export class UserTopNavigationComponent {
  readonly appName = APP_BRANDING.name;
  readonly items: UserMenuItem[] = [
    { label: 'Sorteo', icon: 'confirmation_number', route: '/starter/index' },
    { label: 'Mis Boletos', icon: 'local_activity', route: '/starter/mis-boletos' },
    { label: 'Saldo', icon: 'account_balance_wallet', route: '/starter/wallet' },
    { label: '¿He ganado?', icon: 'emoji_events', route: '/starter/he-ganado' }
  ];
}

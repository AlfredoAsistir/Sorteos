import { NavItem } from '../../vertical/sidebar/nav-item/nav-item';

export const navItems: NavItem[] = [
  {
    displayName: 'Sorteos',
    iconName: 'ticket',
    route: '/starter/sorteos',
    roles: ['Admin'],
  },
  {
    displayName: 'Entrega del premio',
    iconName: 'trophy',
    route: '/starter/entrega-premio',
    roles: ['Admin'],
  },
  {
    displayName: 'Programa de referidos',
    iconName: 'users',
    route: '/starter/programa-referidos',
    roles: ['Admin'],
  },
];

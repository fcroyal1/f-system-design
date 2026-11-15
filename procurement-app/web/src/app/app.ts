import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme';
import { Icon, IconName } from './shared/icon';

interface NavLink {
  path: string;
  label: string;
  exact: boolean;
  icon: IconName;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly theme = inject(ThemeService);

  protected readonly links: NavLink[] = [
    { path: '/', label: 'Dashboard', exact: true, icon: 'dashboard' },
    { path: '/analytics', label: 'Analytics', exact: false, icon: 'analytics' },
    { path: '/suppliers', label: 'Suppliers', exact: false, icon: 'suppliers' },
    { path: '/purchase-orders', label: 'Purchase orders', exact: false, icon: 'orders' },
    { path: '/invoices', label: 'Invoices', exact: false, icon: 'invoices' },
  ];

  protected readonly helpLink: NavLink = { path: '/help', label: 'Help & FAQ', exact: false, icon: 'help' };
}

import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly links = [
    { path: '/', label: 'Dashboard', exact: true },
    { path: '/suppliers', label: 'Suppliers', exact: false },
    { path: '/purchase-orders', label: 'Purchase orders', exact: false },
    { path: '/invoices', label: 'Invoices', exact: false },
  ];
}

import { Routes } from '@angular/router';
import { DashboardPage } from './dashboard/dashboard.page';
import { PlaceholderPage } from './shared/placeholder.page';
import { SupplierListPage } from './suppliers/supplier-list.page';
import { SupplierNewPage } from './suppliers/supplier-new.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: DashboardPage, title: 'Dashboard' },
  { path: 'suppliers', component: SupplierListPage, title: 'Suppliers' },
  { path: 'suppliers/new', component: SupplierNewPage, title: 'New supplier' },
  // Purchase order and invoice screens replace these placeholders in the next UI task.
  { path: 'purchase-orders', component: PlaceholderPage, title: 'Purchase orders' },
  { path: 'invoices', component: PlaceholderPage, title: 'Invoices' },
  { path: 'invoices/:id', component: PlaceholderPage, title: 'Invoice' },
  { path: '**', redirectTo: '' },
];

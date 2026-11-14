import { Routes } from '@angular/router';
import { DashboardPage } from './dashboard/dashboard.page';
import { InvoiceDetailPage } from './invoices/invoice-detail.page';
import { InvoiceListPage } from './invoices/invoice-list.page';
import { InvoiceNewPage } from './invoices/invoice-new.page';
import { PoDetailPage } from './purchase-orders/po-detail.page';
import { PoListPage } from './purchase-orders/po-list.page';
import { PoNewPage } from './purchase-orders/po-new.page';
import { SupplierListPage } from './suppliers/supplier-list.page';
import { SupplierNewPage } from './suppliers/supplier-new.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: DashboardPage, title: 'Dashboard' },
  { path: 'suppliers', component: SupplierListPage, title: 'Suppliers' },
  { path: 'suppliers/new', component: SupplierNewPage, title: 'New supplier' },
  { path: 'purchase-orders', component: PoListPage, title: 'Purchase orders' },
  { path: 'purchase-orders/new', component: PoNewPage, title: 'New purchase order' },
  { path: 'purchase-orders/:id', component: PoDetailPage, title: 'Purchase order' },
  { path: 'invoices', component: InvoiceListPage, title: 'Invoices' },
  { path: 'invoices/new', component: InvoiceNewPage, title: 'New invoice' },
  { path: 'invoices/:id', component: InvoiceDetailPage, title: 'Invoice' },
  { path: '**', redirectTo: '' },
];

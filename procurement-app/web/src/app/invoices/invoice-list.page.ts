import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InvoiceStatus } from '../core/models';
import { ErrorBanner } from '../shared/error-banner';
import { StatusBadge } from '../shared/status-badge';
import { InvoicesStore } from './invoices.store';

@Component({
  selector: 'app-invoice-list-page',
  imports: [DecimalPipe, RouterLink, ErrorBanner, StatusBadge],
  template: `
    <header class="page-header">
      <h1>Invoices</h1>
      <a class="button" routerLink="/invoices/new">New invoice</a>
    </header>

    <div class="filters">
      <label>
        Status
        <select [value]="store.filter() ?? ''" (change)="filter($any($event.target).value)">
          <option value="">All</option>
          <option value="Blocked">Blocked</option>
          <option value="Matched">Matched</option>
        </select>
      </label>
    </div>

    <app-error-banner [message]="store.error()" />

    @if (store.loading() && store.count() === 0) {
      <p class="muted">Loading…</p>
    } @else if (store.count() === 0) {
      <p class="muted">No invoices.</p>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Invoice</th>
            <th>Supplier</th>
            <th>PO</th>
            <th>Invoice date</th>
            <th>Due</th>
            <th class="num">Total</th>
            <th>Status</th>
            <th>Reason</th>
          </tr>
        </thead>
        <tbody>
          @for (i of store.items(); track i.id) {
            <tr>
              <td><a [routerLink]="['/invoices', i.id]">{{ i.invoiceNumber }}</a></td>
              <td>{{ i.supplierName }}</td>
              <td>
                @if (i.poId) {
                  <a [routerLink]="['/purchase-orders', i.poId]">{{ i.poNumber }}</a>
                } @else {
                  {{ i.poNumber ?? '—' }}
                }
              </td>
              <td>{{ i.invoiceDate }}</td>
              <td>{{ i.dueDate }}</td>
              <td class="num">{{ i.total | number: '1.2-2' }} {{ i.currency }}</td>
              <td><app-status-badge [status]="i.status" /></td>
              <td>{{ i.blockReason ?? '—' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class InvoiceListPage {
  protected readonly store = inject(InvoicesStore);

  constructor() {
    void this.store.load(this.store.filter());
  }

  protected filter(value: string): void {
    void this.store.load(value === 'Blocked' || value === 'Matched' ? (value as InvoiceStatus) : null);
  }
}

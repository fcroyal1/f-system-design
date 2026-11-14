import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorBanner } from '../shared/error-banner';
import { StatusBadge } from '../shared/status-badge';
import { PurchaseOrdersStore } from './purchase-orders.store';

@Component({
  selector: 'app-po-list-page',
  imports: [DecimalPipe, RouterLink, ErrorBanner, StatusBadge],
  template: `
    <header class="page-header">
      <h1>Purchase orders</h1>
      <a class="button" routerLink="/purchase-orders/new">New purchase order</a>
    </header>

    <app-error-banner [message]="store.error()" />

    @if (store.loading() && store.count() === 0) {
      <p class="muted">Loading…</p>
    } @else if (store.count() === 0) {
      <p class="muted">No purchase orders yet.</p>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>PO</th>
            <th>Supplier</th>
            <th>Requester</th>
            <th>Cost center</th>
            <th>PO date</th>
            <th class="num">Total</th>
            <th>Status</th>
            <th>Approver role</th>
          </tr>
        </thead>
        <tbody>
          @for (po of store.items(); track po.id) {
            <tr>
              <td><a [routerLink]="['/purchase-orders', po.id]">{{ po.number }}</a></td>
              <td>{{ po.supplierName }}</td>
              <td>{{ po.requesterName }}</td>
              <td>{{ po.costCenterCode }}</td>
              <td>{{ po.poDate }}</td>
              <td class="num">{{ po.total | number: '1.2-2' }} {{ po.currency }}</td>
              <td><app-status-badge [status]="po.status" /></td>
              <td>{{ po.approverRole ?? '—' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class PoListPage {
  protected readonly store = inject(PurchaseOrdersStore);

  constructor() {
    void this.store.load();
  }
}

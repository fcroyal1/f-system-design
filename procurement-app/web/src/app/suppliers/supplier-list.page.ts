import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorBanner } from '../shared/error-banner';
import { StatusBadge } from '../shared/status-badge';
import { SuppliersStore } from './suppliers.store';

@Component({
  selector: 'app-supplier-list-page',
  imports: [RouterLink, ErrorBanner, StatusBadge],
  template: `
    <header class="page-header">
      <h1>Suppliers</h1>
      <a class="button" routerLink="/suppliers/new">New supplier</a>
    </header>

    <app-error-banner [message]="store.error()" />

    @if (store.loading() && store.count() === 0) {
      <p class="muted">Loading…</p>
    } @else if (store.count() === 0) {
      <p class="muted">No suppliers yet.</p>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Code</th>
            <th>Legal name</th>
            <th>Category</th>
            <th class="num">Terms (days)</th>
            <th>Tax ID</th>
            <th>Bank account</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          @for (s of store.items(); track s.id) {
            <tr>
              <td>{{ s.code }}</td>
              <td>{{ s.legalName }}</td>
              <td>{{ s.categoryName }}</td>
              <td class="num">{{ s.paymentTermsDays }}</td>
              <td class="mono">{{ s.taxIdMasked ?? '—' }}</td>
              <td class="mono">{{ s.bankMasked ?? '—' }}</td>
              <td><app-status-badge [status]="s.isBlocked ? 'Blocked' : 'Active'" /></td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class SupplierListPage {
  protected readonly store = inject(SuppliersStore);

  constructor() {
    void this.store.load();
  }
}

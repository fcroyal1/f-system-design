import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorBanner } from '../shared/error-banner';
import { StatusBadge } from '../shared/status-badge';
import { DashboardStore } from './dashboard.store';

@Component({
  selector: 'app-dashboard-page',
  imports: [DecimalPipe, RouterLink, ErrorBanner, StatusBadge],
  template: `
    <header class="page-header">
      <h1>Dashboard</h1>
      <button type="button" class="button secondary" (click)="store.load()" [disabled]="store.loading()">
        Refresh
      </button>
    </header>

    <app-error-banner [message]="store.error()" />

    @if (store.kpis(); as kpis) {
      <section class="kpi-grid" aria-label="Key performance indicators">
        <div class="kpi">
          <span class="kpi-label">PO compliance (spend)</span>
          <span class="kpi-value">{{ kpis.poComplianceBySpendPct | number: '1.0-1' }}%</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">PO compliance (count)</span>
          <span class="kpi-value">{{ kpis.poComplianceByCountPct | number: '1.0-1' }}%</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">First-pass match rate</span>
          <span class="kpi-value">{{ kpis.firstPassMatchRatePct | number: '1.0-1' }}%</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Retro PO rate</span>
          <span class="kpi-value">{{ kpis.retroPoRatePct | number: '1.0-1' }}%</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Open exceptions</span>
          <span class="kpi-value">{{ kpis.openExceptions }}</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Invoices</span>
          <span class="kpi-value">{{ kpis.invoiceCount }}</span>
        </div>
      </section>
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }

    <section>
      <h2>Open exceptions</h2>
      @if (store.hasExceptions()) {
        <table class="table">
          <thead>
            <tr>
              <th>Invoice</th>
              <th>Supplier</th>
              <th class="num">Total</th>
              <th>Status</th>
              <th>Reason</th>
            </tr>
          </thead>
          <tbody>
            @for (item of store.exceptions(); track item.id) {
              <tr data-testid="exception">
                <td><a [routerLink]="['/invoices', item.id]">{{ item.invoiceNumber }}</a></td>
                <td>{{ item.supplierName }}</td>
                <td class="num">{{ item.total | number: '1.2-2' }}</td>
                <td><app-status-badge status="Blocked" /></td>
                <td>{{ item.blockReason ?? 'No reason recorded' }}</td>
              </tr>
            }
          </tbody>
        </table>
      } @else if (!store.loading() && store.kpis()) {
        <p class="muted">No open exceptions. Every invoice is matched.</p>
      }
    </section>
  `,
})
export class DashboardPage {
  protected readonly store = inject(DashboardStore);

  constructor() {
    void this.store.load();
  }
}

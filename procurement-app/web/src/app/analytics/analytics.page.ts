import { DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { money, monthLabel } from '../core/format';
import { EmptyState } from '../shared/empty-state';
import { ErrorBanner } from '../shared/error-banner';
import { AnalyticsStore } from './analytics.store';
import { Bar, BarList } from './bar-list';

/**
 * Plain analytics: flat bars in one accent colour, every value printed, no decoration. Red and amber are
 * kept for the two places that mean trouble (blocked invoices, overdue payables).
 */
@Component({
  selector: 'app-analytics-page',
  imports: [DecimalPipe, RouterLink, BarList, ErrorBanner, EmptyState],
  template: `
    <header class="page-header">
      <div>
        <h1>Analytics</h1>
        <p class="subtitle">Committed spend and invoice health across every supplier.</p>
      </div>
      <button type="button" class="button secondary" (click)="store.load()" [disabled]="store.loading()">Refresh</button>
    </header>

    <app-error-banner [message]="store.error()" />

    @if (store.data(); as a) {
      @if (a.monthly.length === 0) {
        <app-empty-state title="No activity yet" text="Analytics fill in as purchase orders are approved and invoices arrive.">
          <a class="button" routerLink="/purchase-orders/new">Create a purchase order</a>
        </app-empty-state>
      } @else {
        <section class="stats" aria-label="Totals">
          <div class="stat">
            <span class="stat-label">Committed spend</span>
            <span class="stat-value">{{ money(store.totalSpend()) }}</span>
            <span class="stat-note">Approved purchase orders</span>
          </div>
          <div class="stat">
            <span class="stat-label">Invoiced</span>
            <span class="stat-value">{{ money(store.invoiceAmount()) }}</span>
            <span class="stat-note">{{ store.invoiceCount() }} invoices</span>
          </div>
          <div class="stat">
            <span class="stat-label">Invoices blocked</span>
            <span class="stat-value" [class.bad]="store.blockedShare() > 0">{{ store.blockedShare() | number: '1.0-0' }}%</span>
            <span class="stat-note">Held from payment</span>
          </div>
          <div class="stat">
            <span class="stat-label">Suppliers</span>
            <span class="stat-value">{{ a.suppliers.total }}</span>
            <span class="stat-note">{{ a.suppliers.blocked }} blocked</span>
          </div>
        </section>

        <section class="card monthly" aria-labelledby="monthly-title">
          <div class="card-head">
            <h2 id="monthly-title">Monthly activity</h2>
            <p class="legend">
              <span><i class="key po"></i>Purchase orders</span>
              <span><i class="key inv"></i>Invoices</span>
            </p>
          </div>
          <div class="columns" role="table" aria-label="Purchase order and invoice amounts by month">
            @for (m of a.monthly; track m.month) {
              <div class="month" role="row">
                <div class="pair" aria-hidden="true">
                  <span class="col po" [style.height.%]="monthHeight(m.poAmount)" [attr.title]="'POs ' + money(m.poAmount)"></span>
                  <span class="col inv" [style.height.%]="monthHeight(m.invoiceAmount)" [attr.title]="'Invoices ' + money(m.invoiceAmount)"></span>
                </div>
                <span class="month-label" role="rowheader">{{ monthLabel(m.month, true) }}</span>
                <span class="visually-hidden" role="cell">
                  {{ monthLabel(m.month) }}: {{ m.poCount }} purchase orders, {{ money(m.poAmount) }};
                  {{ m.invoiceCount }} invoices, {{ money(m.invoiceAmount) }}
                </span>
              </div>
            }
          </div>
          <p class="scale">Tallest column {{ money(monthMax()) }}</p>
        </section>

        <div class="grid">
          <section class="card" aria-labelledby="cat-title">
            <h2 id="cat-title">Spend by category</h2>
            <app-bar-list [bars]="categoryBars()" />
          </section>

          <section class="card" aria-labelledby="sup-title">
            <h2 id="sup-title">Top suppliers</h2>
            <app-bar-list [bars]="supplierBars()" />
          </section>

          <section class="card" aria-labelledby="status-title">
            <h2 id="status-title">Invoices by status</h2>
            <app-bar-list [bars]="statusBars()" />
            <h3>Why invoices are blocked</h3>
            <app-bar-list [bars]="reasonBars()" />
          </section>

          <section class="card" aria-labelledby="aging-title">
            <h2 id="aging-title">Matched invoices by due date</h2>
            <app-bar-list [bars]="agingBars()" />
            <h3>Approvals by role</h3>
            <app-bar-list [bars]="approvalBars()" />
          </section>

          <section class="card wide" aria-labelledby="base-title">
            <h2 id="base-title">Supplier base</h2>
            <app-bar-list [bars]="supplierBaseBars()" />
          </section>
        </div>
      }
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .subtitle { margin: 0.25rem 0 0; color: var(--ink-3); }
    .stats { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1px; margin-bottom: 1.25rem;
      background: var(--line); border: 1px solid var(--line); border-radius: var(--radius-lg); overflow: hidden; }
    .stat { display: grid; gap: 0.2rem; padding: 1rem 1.25rem; background: var(--surface); }
    .stat-label { font-size: 0.8125rem; color: var(--ink-2); }
    .stat-value { font-size: 1.625rem; font-weight: 700; letter-spacing: -0.02em; }
    .stat-value.bad { color: var(--danger); }
    .stat-note { font-size: 0.8125rem; color: var(--ink-3); }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; }
    .card { padding: 1.25rem 1.5rem; background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius-lg); }
    .card.wide { grid-column: 1 / -1; }
    .card.monthly { margin-bottom: 1.25rem; }
    .card h2 { margin: 0 0 1rem; font-size: 0.9375rem; }
    .card h3 { margin: 1.5rem 0 0.85rem; font-size: 0.8125rem; font-weight: 600; color: var(--ink-2); }
    .card-head { display: flex; justify-content: space-between; align-items: baseline; gap: 1rem; flex-wrap: wrap; }
    .legend { display: flex; gap: 1rem; margin: 0; font-size: 0.8125rem; color: var(--ink-2); }
    .legend span { display: inline-flex; align-items: center; gap: 0.4rem; }
    .key { display: inline-block; width: 10px; height: 10px; border-radius: 2px; }
    .key.po, .col.po { background: var(--accent); }
    .key.inv, .col.inv { background: var(--ink-3); opacity: 0.55; }
    .columns { display: flex; align-items: stretch; gap: 0.5rem; height: 200px; padding-top: 0.5rem;
      border-bottom: 1px solid var(--line); }
    .month { flex: 1; display: grid; grid-template-rows: 1fr auto; min-width: 0; }
    .pair { display: flex; align-items: flex-end; justify-content: center; gap: 3px; }
    .col { width: min(18px, 40%); border-radius: 3px 3px 0 0; min-height: 2px; }
    .month-label { padding: 0.4rem 0; text-align: center; font-size: 0.75rem; color: var(--ink-3); }
    .scale { margin: 0.5rem 0 0; font-size: 0.75rem; color: var(--ink-3); }
    @media (max-width: 960px) {
      .grid { grid-template-columns: minmax(0, 1fr); }
      .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
  `,
})
export class AnalyticsPage {
  protected readonly store = inject(AnalyticsStore);
  protected readonly money = money;
  protected readonly monthLabel = monthLabel;

  protected readonly monthMax = computed(() =>
    Math.max(0, ...(this.store.data()?.monthly ?? []).flatMap((m) => [m.poAmount, m.invoiceAmount])),
  );

  protected readonly categoryBars = computed<Bar[]>(() =>
    (this.store.data()?.spendByCategory ?? []).map((c) => ({
      label: c.name, value: c.amount, display: money(c.amount), note: `${c.poCount} POs`,
    })),
  );

  protected readonly supplierBars = computed<Bar[]>(() =>
    (this.store.data()?.topSuppliers ?? []).map((s) => ({
      label: s.name, value: s.amount, display: money(s.amount), note: `${s.code}, ${s.poCount} POs`,
    })),
  );

  protected readonly statusBars = computed<Bar[]>(() =>
    (this.store.data()?.invoicesByStatus ?? []).map((s) => ({
      label: s.status, value: s.count, display: `${s.count}`, note: money(s.amount),
      tone: s.status === 'Blocked' ? 'danger' : 'accent',
    })),
  );

  protected readonly reasonBars = computed<Bar[]>(() =>
    (this.store.data()?.blockReasons ?? []).map((r) => ({
      label: r.reason, value: r.count, display: `${r.count}`, note: money(r.amount), tone: 'danger',
    })),
  );

  protected readonly agingBars = computed<Bar[]>(() =>
    (this.store.data()?.payablesAging ?? []).map((b) => ({
      label: b.bucket, value: b.amount, display: money(b.amount), note: `${b.count} invoices`,
      tone: b.bucket === 'Overdue' ? 'warning' : 'accent',
    })),
  );

  protected readonly approvalBars = computed<Bar[]>(() =>
    (this.store.data()?.approvalsByRole ?? []).map((r) => ({
      label: r.role, value: r.amount, display: money(r.amount), note: `${r.poCount} POs`,
    })),
  );

  protected readonly supplierBaseBars = computed<Bar[]>(() =>
    (this.store.data()?.suppliers.byCategory ?? []).map((c) => ({
      label: c.name, value: c.count, display: `${c.count}`,
      note: [c.isPoExempt ? 'No PO needed' : 'PO required', c.blocked ? `${c.blocked} blocked` : ''].filter(Boolean).join(', '),
      tone: c.isPoExempt ? 'muted' : 'accent',
    })),
  );

  constructor() {
    void this.store.load();
  }

  protected monthHeight(amount: number): number {
    return this.monthMax() === 0 ? 0 : (amount / this.monthMax()) * 100;
  }
}

import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReferenceStore } from '../core/reference.store';
import { Category } from '../shared/category';
import { EmptyState } from '../shared/empty-state';
import { ErrorBanner } from '../shared/error-banner';
import { Icon } from '../shared/icon';
import { StatusBadge } from '../shared/status-badge';
import { SupplierLogo } from '../shared/supplier-logo';
import { SupplierStatusFilter, filterSuppliers } from './supplier-filter';
import { SuppliersStore } from './suppliers.store';

@Component({
  selector: 'app-supplier-list-page',
  imports: [RouterLink, ErrorBanner, StatusBadge, SupplierLogo, Category, Icon, EmptyState],
  template: `
    <header class="page-header">
      <div>
        <h1>Suppliers</h1>
        @if (store.count() > 0) {
          <p class="subtitle">{{ store.count() }} suppliers, {{ store.blockedCount() }} blocked</p>
        }
      </div>
      <a class="button" routerLink="/suppliers/new"><app-icon name="plus" [size]="18" /> New supplier</a>
    </header>

    <app-error-banner [message]="store.error()" />

    @if (store.count() > 0) {
      <div class="toolbar" role="search">
        <label class="search">
          <app-icon name="search" [size]="18" />
          <span class="visually-hidden">Search suppliers</span>
          <input type="search" placeholder="Search by name or code" [value]="query()" (input)="query.set($any($event.target).value)" />
        </label>
        <label>
          <span class="visually-hidden">Category</span>
          <select [value]="category()" (change)="category.set($any($event.target).value)">
            <option value="">All categories</option>
            @for (c of reference.categories(); track c.code) {
              <option [value]="c.code">{{ c.name }}</option>
            }
          </select>
        </label>
        <div class="segmented" role="group" aria-label="Status">
          @for (option of statusOptions; track option.value) {
            <button type="button" [class.on]="status() === option.value" [attr.aria-pressed]="status() === option.value"
              (click)="status.set(option.value)">{{ option.label }}</button>
          }
        </div>
      </div>
    }

    @if (store.loading() && store.count() === 0) {
      <p class="muted">Loading…</p>
    } @else if (store.count() === 0) {
      <app-empty-state title="No suppliers yet" text="Add the companies you buy from, with their payment terms and bank details.">
        <a class="button" routerLink="/suppliers/new">Add a supplier</a>
      </app-empty-state>
    } @else if (visible().length === 0) {
      <app-empty-state title="No suppliers match" text="Try a different name, or clear the category and status filters.">
        <button type="button" class="button secondary" (click)="clear()">Clear filters</button>
      </app-empty-state>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Supplier</th>
            <th>Category</th>
            <th class="num">Terms</th>
            <th>Tax ID</th>
            <th>Bank account</th>
            <th>Status</th>
            <th><span class="visually-hidden">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          @for (s of visible(); track s.id) {
            <tr>
              <td>
                <a class="identity" [routerLink]="['/suppliers', s.id]">
                  <app-supplier-logo [name]="s.legalName" />
                  <span>
                    <span class="name">{{ s.legalName }}</span>
                    <span class="code">{{ s.code }}</span>
                  </span>
                </a>
              </td>
              <td><app-category [code]="s.categoryCode" [name]="s.categoryName" /></td>
              <td class="num">Net {{ s.paymentTermsDays }}</td>
              <td class="mono">{{ s.taxIdMasked ?? '—' }}</td>
              <td class="mono">{{ s.bankMasked ?? '—' }}</td>
              <td><app-status-badge [status]="s.isBlocked ? 'Blocked' : 'Active'" /></td>
              <td class="actions">
                <a class="button secondary small" [routerLink]="['/suppliers', s.id]" [attr.aria-label]="'Edit ' + s.legalName">
                  <app-icon name="edit" [size]="16" /> Edit
                </a>
              </td>
            </tr>
          }
        </tbody>
      </table>
      <p class="muted count">Showing {{ visible().length }} of {{ store.count() }}</p>
    }
  `,
  styles: `
    .subtitle { margin: 0.25rem 0 0; color: var(--ink-3); }
    .toolbar { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; margin-bottom: 1rem; }
    .search {
      flex: 1 1 260px; display: flex; align-items: center; gap: 0.5rem; padding: 0 0.75rem; min-height: 38px;
      background: var(--surface); border: 1px solid var(--line-strong); border-radius: 999px; color: var(--ink-3);
    }
    .search:focus-within { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-ring); }
    .search input { flex: 1; border: none; outline: none; background: transparent; color: var(--ink); font: inherit; min-width: 0; }
    select {
      min-height: 38px; padding: 0 0.75rem; border: 1px solid var(--line-strong); border-radius: 999px;
      background: var(--surface); color: var(--ink); font: inherit;
    }
    .segmented { display: inline-flex; padding: 3px; border-radius: 999px; background: var(--surface-sunken); border: 1px solid var(--line); }
    .segmented button {
      border: none; background: transparent; color: var(--ink-2); font: inherit; font-weight: 500;
      padding: 0.3rem 0.85rem; border-radius: 999px; cursor: pointer;
    }
    .segmented button.on { background: var(--surface); color: var(--ink); box-shadow: var(--shadow); font-weight: 600; }
    .identity { display: inline-flex; align-items: center; gap: 0.75rem; color: inherit; }
    .identity:hover { text-decoration: none; }
    .identity:hover .name { color: var(--accent); }
    .name { display: block; font-weight: 600; }
    .code { display: block; font-size: 0.8125rem; color: var(--ink-3); font-weight: 400; }
    .actions { text-align: right; }
    .count { margin-top: 0.75rem; font-size: 0.8125rem; }
  `,
})
export class SupplierListPage {
  protected readonly store = inject(SuppliersStore);
  protected readonly reference = inject(ReferenceStore);

  protected readonly query = signal('');
  protected readonly category = signal('');
  protected readonly status = signal<SupplierStatusFilter>('all');
  protected readonly statusOptions: { value: SupplierStatusFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'active', label: 'Active' },
    { value: 'blocked', label: 'Blocked' },
  ];

  protected readonly visible = computed(() =>
    filterSuppliers(this.store.items(), { query: this.query(), category: this.category(), status: this.status() }),
  );

  constructor() {
    void this.store.load();
    void this.reference.load();
  }

  protected clear(): void {
    this.query.set('');
    this.category.set('');
    this.status.set('all');
  }
}

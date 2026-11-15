import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SupplierHistoryEntry } from '../core/models';
import { ReferenceStore } from '../core/reference.store';
import { Category } from '../shared/category';
import { ErrorBanner } from '../shared/error-banner';
import { Icon, IconName } from '../shared/icon';
import { StatusBadge } from '../shared/status-badge';
import { SupplierLogo } from '../shared/supplier-logo';
import { SupplierDetailStore } from './supplier-detail.store';

const HISTORY_LOOK: Record<string, { icon: IconName; label: string; tone: string }> = {
  SupplierUpdated: { icon: 'edit', label: 'Details changed', tone: 'neutral' },
  SupplierBankChanged: { icon: 'bank', label: 'Bank account changed', tone: 'warning' },
  SupplierBlocked: { icon: 'ban', label: 'Blocked', tone: 'danger' },
  SupplierUnblocked: { icon: 'check', label: 'Unblocked', tone: 'success' },
};

@Component({
  selector: 'app-supplier-edit-page',
  imports: [ReactiveFormsModule, RouterLink, DatePipe, ErrorBanner, SupplierLogo, Category, StatusBadge, Icon],
  template: `
    <p><a routerLink="/suppliers">Back to suppliers</a></p>

    <app-error-banner [message]="store.error() ?? reference.error()" [dismissible]="true" (dismissed)="store.clearMessages()" />

    @if (store.supplier(); as s) {
      <header class="hero">
        <app-supplier-logo [name]="s.legalName" [size]="60" />
        <div class="hero-text">
          <h1>{{ s.legalName }} <app-status-badge [status]="s.isBlocked ? 'Blocked' : 'Active'" /></h1>
          <p class="meta">
            <span class="mono">{{ s.code }}</span>
            <app-category [code]="s.categoryCode" [name]="s.categoryName" />
            <span>Net {{ s.paymentTermsDays }} days</span>
          </p>
        </div>
      </header>

      @if (store.notice(); as notice) {
        <div class="notice" role="status">{{ notice }}</div>
      }

      @if (store.pendingBankChange(); as change) {
        <div class="callout warning" role="note">
          <app-icon name="bank" />
          <div>
            <strong>Bank account changed {{ change.occurredAt | date: 'mediumDate' }} by {{ change.actor }}.</strong>
            Confirm the new account with the supplier, using contact details you already hold, before the next payment.
          </div>
        </div>
      }

      <div class="layout">
        <form class="form wide panel" [formGroup]="form" (ngSubmit)="save(s.id)">
          <h2>Details</h2>
          <label>
            Legal name
            <input formControlName="legalName" required maxlength="200" />
          </label>
          <div class="form-row">
            <label>
              Category
              <select formControlName="categoryCode" required>
                @for (c of reference.categories(); track c.code) {
                  <option [value]="c.code">{{ c.name }}</option>
                }
              </select>
            </label>
            <label>
              Payment terms (days)
              <input type="number" formControlName="paymentTermsDays" min="0" required />
            </label>
          </div>

          <h2>Tax and banking</h2>
          <div class="form-row">
            <label>
              New tax ID
              <input formControlName="taxId" autocomplete="off" spellcheck="false" [placeholder]="s.taxIdMasked ?? 'None on file'" />
              <small>Leave blank to keep {{ s.taxIdMasked ?? 'it empty' }}.</small>
            </label>
            <label>
              New bank account
              <input formControlName="bankAccount" autocomplete="off" spellcheck="false" [placeholder]="s.bankMasked ?? 'None on file'" />
              <small>Leave blank to keep {{ s.bankMasked ?? 'it empty' }}.</small>
            </label>
          </div>
          @if (form.controls.bankAccount.value.trim()) {
            <p class="field-warning"><app-icon name="alert" [size]="16" /> Changing bank details is recorded and flagged for review. Only the last 4 characters are stored.</p>
          }

          <h2>Status</h2>
          <label class="switch">
            <input type="checkbox" formControlName="isBlocked" role="switch" />
            <span class="track" aria-hidden="true"><span class="thumb"></span></span>
            <span>
              <strong>Block this supplier</strong>
              <small>New invoices and re-matches are blocked until the supplier is unblocked.</small>
            </span>
          </label>

          <div class="form-actions">
            <button type="submit" class="button" [disabled]="form.invalid || form.pristine || store.saving()">
              {{ store.saving() ? 'Saving…' : 'Save changes' }}
            </button>
            <button type="button" class="button secondary" [disabled]="form.pristine || store.saving()" (click)="reset()">Discard</button>
          </div>
        </form>

        <section class="panel history" aria-labelledby="history-title">
          <h2 id="history-title">History</h2>
          @if (store.history().length === 0) {
            <p class="muted">No changes since this supplier was added.</p>
          } @else {
            <ol class="timeline">
              @for (h of store.history(); track $index) {
                <li [class]="look(h).tone">
                  <span class="dot"><app-icon [name]="look(h).icon" [size]="14" /></span>
                  <div>
                    <p class="what">{{ look(h).label }}</p>
                    @if (h.detail) {
                      <p class="detail">{{ h.detail }}</p>
                    }
                    <p class="when">{{ h.actor }}, {{ h.occurredAt | date: 'medium' }}</p>
                  </div>
                </li>
              }
            </ol>
          }
        </section>
      </div>
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .hero { display: flex; align-items: center; gap: 1rem; margin-bottom: 1.5rem; }
    .hero h1 { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .meta { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; margin: 0.4rem 0 0; color: var(--ink-2); }
    .callout {
      display: flex; gap: 0.75rem; align-items: flex-start; margin: 0 0 1rem; padding: 0.85rem 1rem;
      border-radius: var(--radius); border: 1px solid var(--warning-line); background: var(--warning-soft); color: var(--warning);
    }
    .callout strong { color: inherit; }
    .layout { display: grid; grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr); gap: 1.25rem; align-items: start; }
    .panel h2 { margin: 0.25rem 0 0; font-size: 0.9375rem; }
    .panel h2 + * { margin-top: 0; }
    .field-warning { display: flex; gap: 0.4rem; align-items: center; margin: -0.25rem 0 0; color: var(--warning); font-size: 0.8125rem; }
    .switch { flex-direction: row !important; align-items: flex-start; gap: 0.75rem !important; cursor: pointer; color: var(--ink) !important; }
    .switch input { position: absolute; opacity: 0; width: 1px; height: 1px; }
    .switch small { display: block; font-weight: 400; color: var(--ink-3); }
    .track {
      flex: none; position: relative; width: 40px; height: 22px; margin-top: 1px; border-radius: 999px;
      background: var(--line-strong); transition: background-color 160ms;
    }
    .thumb {
      position: absolute; top: 3px; left: 3px; width: 16px; height: 16px; border-radius: 50%;
      background: #fff; box-shadow: 0 1px 2px rgb(0 0 0 / 0.3); transition: transform 160ms;
    }
    .switch input:checked + .track { background: var(--danger); }
    .switch input:checked + .track .thumb { transform: translateX(18px); }
    .switch input:focus-visible + .track { outline: 2px solid var(--accent); outline-offset: 2px; }
    .history { padding: 1.25rem 1.5rem; background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius-lg); box-shadow: var(--shadow); }
    .timeline { list-style: none; margin: 1rem 0 0; padding: 0; display: grid; gap: 1.1rem; }
    .timeline li { display: grid; grid-template-columns: 28px 1fr; gap: 0.75rem; position: relative; }
    .timeline li:not(:last-child)::after {
      content: ''; position: absolute; left: 13.5px; top: 30px; bottom: -1.1rem; width: 1px; background: var(--line);
    }
    .dot { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 50%; background: var(--surface-sunken); color: var(--ink-2); }
    .warning .dot { background: var(--warning-soft); color: var(--warning); }
    .danger .dot { background: var(--danger-soft); color: var(--danger); }
    .success .dot { background: var(--success-soft); color: var(--success); }
    .what { margin: 0.2rem 0 0; font-weight: 600; }
    .detail { margin: 0.15rem 0 0; color: var(--ink-2); font-size: 0.8125rem; overflow-wrap: anywhere; }
    .when { margin: 0.2rem 0 0; color: var(--ink-3); font-size: 0.75rem; }
    @media (max-width: 960px) { .layout { grid-template-columns: minmax(0, 1fr); } }
  `,
})
export class SupplierEditPage {
  protected readonly store = inject(SupplierDetailStore);
  protected readonly reference = inject(ReferenceStore);
  readonly id = input.required<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    legalName: ['', [Validators.required, Validators.maxLength(200)]],
    categoryCode: ['', Validators.required],
    paymentTermsDays: [0, [Validators.required, Validators.min(0)]],
    taxId: [''],
    bankAccount: [''],
    isBlocked: [false],
  });

  /** The supplier id the form was last filled from, so a reload of the same supplier keeps unsaved edits. */
  private readonly filledFor = signal<string | null>(null);

  constructor() {
    void this.reference.load();
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.store.clearMessages();
        void this.store.load(id);
      });
    });
    effect(() => {
      const supplier = this.store.supplier();
      if (supplier && supplier.id === this.id() && untracked(this.filledFor) !== supplier.id) {
        untracked(() => this.fill());
      }
    });
  }

  protected look(entry: SupplierHistoryEntry) {
    return HISTORY_LOOK[entry.action] ?? { icon: 'clock' as IconName, label: entry.action, tone: 'neutral' };
  }

  protected reset(): void {
    this.fill();
  }

  protected async save(id: string): Promise<void> {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.getRawValue();
    const saved = this.store.save(id, {
      legalName: value.legalName.trim(),
      categoryCode: value.categoryCode,
      paymentTermsDays: value.paymentTermsDays,
      taxId: value.taxId.trim() || null,
      bankAccount: value.bankAccount.trim() || null,
      isBlocked: value.isBlocked,
    });
    // The full numbers have been sent; never keep them in the form, whatever the outcome.
    this.form.patchValue({ taxId: '', bankAccount: '' });
    if (await saved) {
      this.fill();
    }
  }

  private fill(): void {
    const s = this.store.supplier();
    if (!s) {
      return;
    }
    this.form.reset({
      legalName: s.legalName,
      categoryCode: s.categoryCode,
      paymentTermsDays: s.paymentTermsDays,
      taxId: '',
      bankAccount: '',
      isBlocked: s.isBlocked,
    });
    this.filledFor.set(s.id);
  }
}

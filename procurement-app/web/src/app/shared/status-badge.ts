import { Component, computed, input } from '@angular/core';

export type BadgeTone = 'danger' | 'success' | 'neutral' | 'muted';

const TONES: Record<string, BadgeTone> = {
  Blocked: 'danger',
  Matched: 'success',
  Approved: 'success',
  Active: 'success',
  Draft: 'neutral',
  Closed: 'muted',
};

/** A status pill for invoice, PO and supplier statuses. Blocked is shown in the danger style. */
@Component({
  selector: 'app-status-badge',
  template: `<span [class]="'badge badge-' + tone()">{{ status() }}</span>`,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.125rem 0.6rem 0.125rem 0.5rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      line-height: 1.5;
      white-space: nowrap;
      vertical-align: middle;
    }
    .badge::before {
      content: '';
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }
    .badge-danger { background: var(--danger-soft, #fdeeed); color: var(--danger, #b42318); }
    .badge-success { background: var(--success-soft, #e7f4ec); color: var(--success, #107c41); }
    .badge-neutral { background: var(--accent-soft, #e8f1fb); color: var(--accent, #0b5cad); }
    .badge-muted { background: var(--surface-sunken, #eef0f3); color: var(--ink-3, #6b7582); }
  `,
})
export class StatusBadge {
  readonly status = input.required<string>();
  readonly tone = computed<BadgeTone>(() => TONES[this.status()] ?? 'neutral');
}

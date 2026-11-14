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
      display: inline-block;
      padding: 0.1rem 0.55rem;
      border-radius: 999px;
      font-size: 0.8rem;
      font-weight: 600;
      line-height: 1.4;
      white-space: nowrap;
      border: 1px solid transparent;
    }
    .badge-danger { background: #fdecec; color: #a61b1b; border-color: #f3b4b4; }
    .badge-success { background: #e8f6ee; color: #18663b; border-color: #a9dcbe; }
    .badge-neutral { background: #eef2f7; color: #334155; border-color: #cbd5e1; }
    .badge-muted { background: #f4f4f5; color: #6b7280; border-color: #e4e4e7; }
  `,
})
export class StatusBadge {
  readonly status = input.required<string>();
  readonly tone = computed<BadgeTone>(() => TONES[this.status()] ?? 'neutral');
}

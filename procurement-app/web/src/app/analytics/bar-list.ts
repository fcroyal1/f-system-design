import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface Bar {
  label: string;
  value: number;
  /** Shown at the right instead of the raw value. */
  display: string;
  /** Smaller text under the label. */
  note?: string;
  tone?: 'accent' | 'danger' | 'warning' | 'muted';
}

/**
 * A plain horizontal bar chart: label, bar, value. Bars share one scale (the largest value), so lengths
 * compare honestly. The numbers are always printed, so the chart reads without the bars too.
 */
@Component({
  selector: 'app-bar-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul>
      @for (bar of bars(); track bar.label) {
        <li>
          <span class="label">
            <span class="text">{{ bar.label }}</span>
            @if (bar.note) {
              <span class="note">{{ bar.note }}</span>
            }
          </span>
          <span class="track" aria-hidden="true">
            <span class="fill" [class]="'fill ' + (bar.tone ?? 'accent')" [style.width.%]="width(bar.value)"></span>
          </span>
          <span class="value">{{ bar.display }}</span>
        </li>
      } @empty {
        <li class="empty">Nothing to show yet.</li>
      }
    </ul>
  `,
  styles: `
    ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.7rem; }
    li { display: grid; grid-template-columns: minmax(0, 11rem) minmax(0, 1fr) auto; align-items: center; gap: 0.85rem; }
    .label { min-width: 0; }
    .text { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .note { display: block; font-size: 0.75rem; color: var(--ink-3); }
    .track { height: 8px; border-radius: 4px; background: var(--surface-sunken); overflow: hidden; }
    .fill { display: block; height: 100%; border-radius: 4px; min-width: 2px; }
    .fill.accent { background: var(--accent); }
    .fill.danger { background: var(--danger); }
    .fill.warning { background: var(--warning); }
    .fill.muted { background: var(--ink-3); }
    .value { font-weight: 600; text-align: right; white-space: nowrap; min-width: 4.5rem; }
    .empty { display: block; color: var(--ink-3); }
    @media (max-width: 560px) { li { grid-template-columns: minmax(0, 8rem) minmax(0, 1fr) auto; } }
  `,
})
export class BarList {
  readonly bars = input.required<Bar[]>();
  private readonly max = computed(() => Math.max(0, ...this.bars().map((b) => b.value)));

  protected width(value: number): number {
    return this.max() === 0 ? 0 : (value / this.max()) * 100;
  }
}

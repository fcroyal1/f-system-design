import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** An illustrated empty or "all clear" state. Projected content (e.g. a button) goes under the text. */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      @if (tone() === 'clear') {
        <svg viewBox="0 0 120 96" aria-hidden="true">
          <ellipse cx="60" cy="86" rx="40" ry="5" class="shadow" />
          <circle cx="60" cy="44" r="32" class="soft-ok" />
          <circle cx="60" cy="44" r="22" class="ok" />
          <path d="M49 44.5l7.5 7.5L72 36.5" class="tick" />
          <circle cx="100" cy="20" r="3" class="spark" /><circle cx="18" cy="30" r="2" class="spark" />
        </svg>
      } @else {
        <svg viewBox="0 0 120 96" aria-hidden="true">
          <ellipse cx="60" cy="88" rx="42" ry="5" class="shadow" />
          <rect x="30" y="18" width="52" height="64" rx="7" class="paper back" />
          <rect x="38" y="10" width="52" height="64" rx="7" class="paper" />
          <path d="M48 26h32M48 36h32M48 46h20" class="lines" />
          <circle cx="86" cy="64" r="14" class="lens" />
          <path d="M96 74l9 9" class="handle" />
        </svg>
      }
      <p class="title">{{ title() }}</p>
      @if (text()) {
        <p class="text">{{ text() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: grid; justify-items: center; gap: 0.35rem; padding: 2.5rem 1.5rem; text-align: center;
      background: var(--surface); border: 1px dashed var(--line-strong); border-radius: var(--radius-lg, 12px);
    }
    svg { width: 120px; height: 96px; margin-bottom: 0.5rem; }
    .shadow { fill: var(--line); }
    .paper { fill: var(--surface); stroke: var(--line-strong); stroke-width: 1.5; }
    .paper.back { fill: var(--surface-sunken); }
    .lines { stroke: var(--line-strong); stroke-width: 3; stroke-linecap: round; }
    .lens { fill: var(--accent-soft); stroke: var(--accent); stroke-width: 3; }
    .handle { stroke: var(--accent); stroke-width: 4; stroke-linecap: round; }
    .soft-ok { fill: var(--success-soft); }
    .ok { fill: var(--success); }
    .tick { fill: none; stroke: #fff; stroke-width: 5; stroke-linecap: round; stroke-linejoin: round; }
    .spark { fill: var(--success); opacity: 0.5; }
    .title { margin: 0; font-weight: 650; font-size: 1rem; }
    .text { margin: 0 0 0.5rem; max-width: 42ch; color: var(--ink-3); }
  `,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly text = input('');
  /** 'empty' for nothing-here-yet, 'clear' for good news (no exceptions). */
  readonly tone = input<'empty' | 'clear'>('empty');
}

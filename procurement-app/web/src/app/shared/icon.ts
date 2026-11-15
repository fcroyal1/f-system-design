import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** 24px outline icons (1.75 stroke), drawn for this app. One path string per icon; "M" starts a new stroke. */
export const ICON_PATHS = {
  dashboard: 'M4 13h6V4H4zm10 7h6V11h-6zM4 20h6v-4H4zm10-11h6V4h-6z',
  suppliers: 'M3 21V8l6-4 6 4v13M15 21h6V11l-6-3M7 12h4M7 16h4',
  orders: 'M7 3h8l4 4v14H7zM15 3v4h4M10 12h6M10 16h6',
  invoices: 'M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6M9 16h3',
  analytics: 'M4 20h16M7 16v-5M12 16V6M17 16v-8',
  help: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM9.5 9a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6v.6M12 17h.01',
  search: 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM21 21l-5-5',
  edit: 'M4 20h4L19 9l-4-4L4 16zM14 6l4 4',
  plus: 'M12 5v14M5 12h14',
  sun: 'M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4',
  moon: 'M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z',
  alert: 'M12 3l10 18H2zM12 10v4M12 17.5h.01',
  check: 'M5 12.5l4.5 4.5L19 7',
  bank: 'M3 10l9-6 9 6M5 10v8M9.5 10v8M14.5 10v8M19 10v8M3 21h18',
  clock: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2',
  arrow: 'M5 12h14M13 6l6 6-6 6',
  shield: 'M12 3l8 3v6c0 4.5-3.4 8.2-8 9-4.6-.8-8-4.5-8-9V6zM8.5 12l2.5 2.5 4.5-5',
  receipt: 'M4 7h16v13H4zM8 7V4h8v3M8 12h8M8 16h5',
  ban: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM5.6 5.6l12.8 12.8',
  // Spend categories
  medical: 'M9 3h6v6h6v6h-6v6H9v-6H3V9h6z',
  lab: 'M9 3h6M10 3v6L4.5 18.5A1.7 1.7 0 0 0 6 21h12a1.7 1.7 0 0 0 1.5-2.5L14 9V3M7 15h10',
  office: 'M6 3h9l3 3v15H6zM9 9h6M9 13h6M9 17h4',
  it: 'M3 5h18v11H3zM8 20h8M12 16v4',
  facilities: 'M14.5 6.5a4 4 0 0 0-5 5L3 18l3 3 6.5-6.5a4 4 0 0 0 5-5L15 12l-3-3z',
  utilities: 'M13 2L4 14h7l-1 8 9-12h-7z',
  rent: 'M3 11l9-7 9 7M5 10v11h14V10M10 21v-6h4v6',
  taxes: 'M5 19L19 5M7.5 9a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zM16.5 18a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z',
  insurance: 'M12 3l8 3v6c0 4.5-3.4 8.2-8 9-4.6-.8-8-4.5-8-9V6z',
  legal: 'M12 3v18M7 21h10M5 7h14M5 7l-3 7a3 3 0 0 0 6 0zM19 7l-3 7a3 3 0 0 0 6 0z',
  refunds: 'M4 9h12a4 4 0 0 1 0 8H8M4 9l4-4M4 9l4 4',
} as const;

export type IconName = keyof typeof ICON_PATHS;

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 24 24" [attr.width]="size()" [attr.height]="size()" aria-hidden="true" focusable="false">
      <path [attr.d]="path()" />
    </svg>
  `,
  styles: `
    :host { display: inline-flex; flex: none; line-height: 0; }
    svg { fill: none; stroke: currentColor; stroke-width: 1.75; stroke-linecap: round; stroke-linejoin: round; }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(20);
  protected readonly path = computed(() => ICON_PATHS[this.name()]);
}

import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon, IconName } from './icon';

/** Each spend category's icon and hue. Unknown categories fall back to a neutral tag. */
const CATEGORY_STYLE: Record<string, { icon: IconName; hue: number }> = {
  'MED-SUPPLIES': { icon: 'medical', hue: 352 },
  LAB: { icon: 'lab', hue: 268 },
  OFFICE: { icon: 'office', hue: 212 },
  IT: { icon: 'it', hue: 190 },
  FACILITIES: { icon: 'facilities', hue: 28 },
  UTILITIES: { icon: 'utilities', hue: 45 },
  RENT: { icon: 'rent', hue: 160 },
  TAXES: { icon: 'taxes', hue: 0 },
  INSURANCE: { icon: 'insurance', hue: 230 },
  LEGAL: { icon: 'legal', hue: 300 },
  REFUNDS: { icon: 'refunds', hue: 135 },
};

export function categoryStyle(code: string): { icon: IconName; hue: number } {
  return CATEGORY_STYLE[code] ?? { icon: 'receipt', hue: 215 };
}

/** A spend category as a tinted icon chip, optionally with its name. */
@Component({
  selector: 'app-category',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="chip" [style.--hue]="style().hue" [attr.title]="label() ? null : name()">
      <app-icon [name]="style().icon" [size]="15" />
    </span>
    @if (label()) {
      <span class="name">{{ name() }}</span>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 0.5rem; white-space: nowrap; }
    .chip {
      display: grid; place-items: center; width: 26px; height: 26px; border-radius: 7px;
      color: hsl(var(--hue) 62% var(--cat-fg-l, 38%));
      background: hsl(var(--hue) 80% var(--cat-bg-l, 95%));
    }
  `,
})
export class Category {
  readonly code = input.required<string>();
  readonly name = input('');
  readonly label = input(true);
  protected readonly style = computed(() => categoryStyle(this.code()));
}

import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Up to two initials from the meaningful words of a company name ("Papermill & Co" → "PC"). */
export function initials(name: string): string {
  const skip = new Set(['and', 'of', 'the', 'llc', 'llp', 'inc', 'ltd', '&', '-', '–']);
  const words = name.split(/\s+/).filter((w) => w && !skip.has(w.toLowerCase()));
  return (words.length >= 2 ? words[0][0] + words[1][0] : (words[0] ?? '?').slice(0, 2)).toUpperCase();
}

/** A stable hue (0–359) per supplier name, so a supplier has the same colour on every screen. */
export function hueFor(key: string): number {
  let hash = 0;
  for (const ch of key) {
    hash = (hash * 31 + ch.charCodeAt(0)) % 360;
  }
  return hash;
}

/**
 * The supplier's generated logo: a gradient tile with its initials. The companies are fictional, so this
 * stands in for a real logo and is the same colour on every screen.
 */
@Component({
  selector: 'app-supplier-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="logo" [style.--hue]="hue()" [style.--size.px]="size()" aria-hidden="true">{{ letters() }}</span>`,
  styles: `
    :host { display: inline-flex; flex: none; }
    .logo {
      display: grid; place-items: center;
      width: var(--size); height: var(--size); border-radius: calc(var(--size) * 0.28);
      background: linear-gradient(135deg, hsl(var(--hue) 70% 52%), hsl(calc(var(--hue) + 35) 75% 42%));
      color: #fff; font-weight: 700; font-size: calc(var(--size) * 0.38); letter-spacing: 0.02em;
      box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.18);
    }
  `,
})
export class SupplierLogo {
  /** The supplier's legal name; it gives both the initials and the colour (every screen has the name). */
  readonly name = input.required<string>();
  readonly size = input(36);
  protected readonly letters = computed(() => initials(this.name()));
  protected readonly hue = computed(() => hueFor(this.name()));
}

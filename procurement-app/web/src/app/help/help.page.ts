import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon, IconName } from '../shared/icon';

interface Faq {
  q: string;
  /** Paragraphs of the answer. */
  a: string[];
}

interface FaqGroup {
  id: string;
  title: string;
  icon: IconName;
  items: Faq[];
}

/**
 * Answers written from the app's actual rules (MatchEngine, Tolerance.Default, the DoA tiers in the
 * reference data, RetroPoRule, KpiCalculator). Change them together with those rules.
 */
const GROUPS: FaqGroup[] = [
  {
    id: 'matching',
    title: 'Matching and blocked invoices',
    icon: 'receipt',
    items: [
      {
        q: 'What is a three-way match?',
        a: [
          'Every invoice is compared with its purchase order (what was agreed) and the goods receipts (what actually arrived). If all three agree, the invoice is Matched and can be paid. If anything disagrees, it is Blocked and shows the reason.',
          'The checks are: supplier, currency, PO status, PO line, quantity, unit price and line amount.',
        ],
      },
      {
        q: 'Why is my invoice blocked?',
        a: [
          'The red bar at the top of the invoice names the first failing check, and the 3-way match table shows every check with the PO, receipt and invoice values side by side. The common reasons are billing more than was received, a unit price above tolerance, a PO number that does not exist or is not approved, and a blocked supplier.',
        ],
      },
      {
        q: 'How much difference is allowed?',
        a: [
          'Quantity: none. An invoice line may bill at most what was received (or, for lines that need no receipt such as services, what was ordered), less anything already invoiced on that line.',
          'Unit price: up to 3% above the PO price and no more than $50 extra across the line. Going over either limit blocks the invoice. Charging less than the PO price always passes.',
        ],
      },
      {
        q: 'How do I unblock an invoice?',
        a: [
          'Fix the cause, not the invoice. If goods arrived after the invoice, record the receipt on the purchase order; every blocked invoice on that PO is re-matched automatically. If the supplier was blocked and is now cleared, unblock the supplier and press Re-match on the invoice. A price or quantity the supplier got wrong needs a corrected invoice from them.',
        ],
      },
      {
        q: 'Can the same invoice be entered twice?',
        a: [
          'No. Invoice numbers are compared per supplier after removing the letter prefix, punctuation and leading zeros, so "INV-0077812" and "77812" from the same supplier count as the same invoice and the second is refused.',
        ],
      },
    ],
  },
  {
    id: 'orders',
    title: 'Purchase orders and approvals',
    icon: 'orders',
    items: [
      {
        q: 'Who approves a purchase order?',
        a: [
          'The PO total decides the approver: up to $5,000 the cost center owner, up to $25,000 a director, up to $100,000 the VP Finance, and above that the CFO. The required role is shown on the PO, and approval is recorded in the audit trail.',
        ],
      },
      {
        q: 'What is a retro PO?',
        a: [
          'A purchase order raised after the supplier had already invoiced: the invoice date is earlier than the PO date. The invoice can still match, but it is counted in the retro PO rate because the spend was committed before it was approved.',
        ],
      },
      {
        q: 'Which spend does not need a purchase order?',
        a: [
          'Utilities, rent, taxes, insurance, legal fees and refunds are PO-exempt. They are left out of PO compliance so they neither help nor hurt the score. Medical, laboratory, office, IT and facilities spend all need a PO.',
        ],
      },
    ],
  },
  {
    id: 'suppliers',
    title: 'Suppliers',
    icon: 'suppliers',
    items: [
      {
        q: 'What happens when I block a supplier?',
        a: [
          'New invoices from that supplier, and any re-match of their existing invoices, are blocked with the reason "supplier is blocked" until you unblock them. Purchase orders and receipts are not affected. Blocking and unblocking are both recorded in the supplier history.',
        ],
      },
      {
        q: 'Why is a bank account change highlighted?',
        a: [
          'Changing where a supplier is paid is the most common route for invoice fraud. Every change is recorded separately in the supplier history and shown as a warning on the supplier. Confirm a new account with the supplier using contact details you already hold, never ones from the email that asked for the change.',
        ],
      },
      {
        q: 'Why can I only see the last 4 digits of tax IDs and bank accounts?',
        a: [
          'Only the last 4 characters are ever stored. The full number you type is used once to take those 4 characters and is then discarded, so it cannot leak from the database or the screen. To replace one, type the new full value on the supplier page.',
        ],
      },
      {
        q: 'Can I change a supplier code?',
        a: ['No. Purchase orders, invoices and reports refer to the code, so it stays fixed. Everything else can be edited.'],
      },
    ],
  },
  {
    id: 'numbers',
    title: 'Dashboard and analytics',
    icon: 'analytics',
    items: [
      {
        q: 'What do the dashboard numbers mean?',
        a: [
          'Open exceptions: invoices currently blocked from payment. First-pass match rate: the share of PO-backed invoices that matched the first time, before any fix. PO compliance: the share of PO-required spend (by value and by count) that had a purchase order. Retro PO rate: the share of PO-backed invoices dated before their PO.',
        ],
      },
      {
        q: 'What counts as committed spend in Analytics?',
        a: [
          'The line totals of every purchase order that has been approved. Drafts are not counted. Invoice amounts include tax.',
        ],
      },
      {
        q: 'Where does the demo data come from?',
        a: [
          'The suppliers are fictional. The months of purchase orders, receipts and invoices were created through the app’s own API when the stack first started, so every match result and approval was decided by the real rules.',
        ],
      },
    ],
  },
];

@Component({
  selector: 'app-help-page',
  imports: [RouterLink, Icon],
  template: `
    <header class="hero">
      <div>
        <h1>Help and FAQ</h1>
        <p>How matching, approvals and supplier controls work in this app.</p>
      </div>
      <label class="search">
        <app-icon name="search" [size]="18" />
        <span class="visually-hidden">Search the FAQ</span>
        <input type="search" placeholder="Search questions" [value]="query()" (input)="query.set($any($event.target).value)" />
      </label>
    </header>

    <nav class="chips" aria-label="FAQ sections">
      @for (g of groups(); track g.id) {
        <a [href]="'help#' + g.id" (click)="jump($event, g.id)"><app-icon [name]="g.icon" [size]="16" /> {{ g.title }}</a>
      }
    </nav>

    @for (g of groups(); track g.id) {
      <section class="group" [id]="g.id" [attr.aria-labelledby]="g.id + '-title'">
        <h2 [id]="g.id + '-title'"><span class="group-icon"><app-icon [name]="g.icon" [size]="18" /></span>{{ g.title }}</h2>
        @for (item of g.items; track item.q) {
          <details [open]="query().trim().length > 0">
            <summary>{{ item.q }}</summary>
            @for (p of item.a; track $index) {
              <p>{{ p }}</p>
            }
          </details>
        }
      </section>
    } @empty {
      <p class="muted">No questions match "{{ query() }}".</p>
    }

    <aside class="more">
      <p><strong>Still stuck?</strong> Open the item in question: blocked invoices show every check, and suppliers show their full change history.</p>
      <a class="button secondary" routerLink="/invoices">Go to invoices</a>
    </aside>
  `,
  styles: `
    .hero {
      display: flex; justify-content: space-between; align-items: flex-end; gap: 1.5rem; flex-wrap: wrap;
      padding: 1.75rem 2rem; margin-bottom: 1rem; border-radius: var(--radius-lg); color: #fff;
      background:
        radial-gradient(120% 140% at 100% 0%, rgb(255 255 255 / 0.16), transparent 55%),
        linear-gradient(135deg, #0b5cad, #0a3d78);
    }
    .hero h1 { color: #fff; }
    .hero p { margin: 0.35rem 0 0; color: rgb(255 255 255 / 0.85); }
    .search {
      flex: 0 1 320px; display: flex; align-items: center; gap: 0.5rem; padding: 0 0.9rem; min-height: 42px;
      border-radius: 999px; background: rgb(255 255 255 / 0.95); color: #48525e;
    }
    .search input { flex: 1; border: none; outline: none; background: transparent; font: inherit; color: #1a1f26; min-width: 0; }
    .chips { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1.5rem; }
    .chips a {
      display: inline-flex; align-items: center; gap: 0.4rem; padding: 0.35rem 0.85rem; border-radius: 999px;
      border: 1px solid var(--line); background: var(--surface); color: var(--ink-2); font-size: 0.8125rem;
    }
    .chips a:hover { border-color: var(--accent); color: var(--accent); text-decoration: none; }
    .group { margin-bottom: 1.75rem; scroll-margin-top: 72px; }
    .group h2 { display: flex; align-items: center; gap: 0.6rem; margin: 0 0 0.75rem; }
    .group-icon { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 8px; background: var(--accent-soft); color: var(--accent); }
    details {
      background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius);
      margin-bottom: 0.5rem; transition: border-color 120ms;
    }
    details[open] { border-color: var(--line-strong); box-shadow: var(--shadow); }
    summary {
      display: flex; justify-content: space-between; align-items: center; gap: 1rem;
      padding: 0.9rem 1.1rem; font-weight: 600; cursor: pointer; list-style: none;
    }
    summary::-webkit-details-marker { display: none; }
    summary::after {
      content: ''; flex: none; width: 8px; height: 8px; margin-right: 4px;
      border-right: 2px solid var(--ink-3); border-bottom: 2px solid var(--ink-3); transform: rotate(45deg);
      transition: transform 160ms;
    }
    details[open] summary::after { transform: rotate(-135deg); }
    details p { margin: 0 1.1rem 0.9rem; max-width: 75ch; color: var(--ink-2); line-height: 1.6; }
    .more {
      display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap;
      margin-top: 2rem; padding: 1.1rem 1.25rem; border-radius: var(--radius-lg); background: var(--surface-sunken); border: 1px solid var(--line);
    }
    .more p { margin: 0; color: var(--ink-2); }
    @media (prefers-reduced-motion: reduce) { summary::after { transition: none; } }
  `,
})
export class HelpPage {
  protected readonly query = signal('');

  protected readonly groups = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) {
      return GROUPS;
    }
    return GROUPS.map((g) => ({
      ...g,
      items: g.items.filter((i) => i.q.toLowerCase().includes(q) || i.a.some((p) => p.toLowerCase().includes(q))),
    })).filter((g) => g.items.length > 0);
  });

  protected jump(event: Event, id: string): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}

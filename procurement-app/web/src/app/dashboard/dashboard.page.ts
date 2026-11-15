import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmptyState } from '../shared/empty-state';
import { ErrorBanner } from '../shared/error-banner';
import { Icon } from '../shared/icon';
import { SupplierLogo } from '../shared/supplier-logo';
import { DashboardStore } from './dashboard.store';

@Component({
  selector: 'app-dashboard-page',
  imports: [DecimalPipe, DatePipe, RouterLink, ErrorBanner, Icon, SupplierLogo, EmptyState],
  template: `
    <section class="hero">
      <div class="hero-copy">
        <p class="date">{{ today | date: 'EEEE, MMMM d' }}</p>
        <h1>{{ greeting }}, Demo User</h1>
        <p class="summary">
          @if (store.kpis(); as kpis) {
            @if (kpis.openExceptions > 0) {
              {{ kpis.openExceptions }} {{ kpis.openExceptions === 1 ? 'invoice is' : 'invoices are' }} held from payment. The newest are listed below.
            } @else {
              Every invoice is matched. Nothing is waiting on you.
            }
          } @else {
            Checking today's invoices…
          }
        </p>
        <div class="actions">
          <a class="button light" routerLink="/purchase-orders/new"><app-icon name="plus" [size]="18" /> New purchase order</a>
          <a class="button ghost" routerLink="/invoices/new"><app-icon name="invoices" [size]="18" /> Record an invoice</a>
          <a class="button ghost" routerLink="/analytics"><app-icon name="analytics" [size]="18" /> Analytics</a>
        </div>
      </div>

      <!-- The three-way match: a PO, a goods receipt and an invoice that agree. -->
      <svg class="hero-art" viewBox="0 0 320 190" aria-hidden="true">
        <defs>
          <linearGradient id="card-face" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stop-color="#ffffff" />
            <stop offset="1" stop-color="#e9f1fb" />
          </linearGradient>
        </defs>
        <path d="M78 96 C 110 96, 112 70, 146 70 M 196 70 C 228 70, 230 112, 262 112" class="link" />
        <g transform="translate(18 52) rotate(-6 30 40)">
          <rect width="64" height="84" rx="9" fill="url(#card-face)" class="card" />
          <rect x="10" y="12" width="26" height="6" rx="3" class="ink-strong" />
          <rect x="10" y="28" width="44" height="4" rx="2" class="ink" /><rect x="10" y="38" width="38" height="4" rx="2" class="ink" />
          <rect x="10" y="48" width="42" height="4" rx="2" class="ink" />
          <text x="10" y="74" class="label">PO</text>
        </g>
        <g transform="translate(130 26)">
          <rect width="70" height="90" rx="9" fill="url(#card-face)" class="card" />
          <rect x="12" y="14" width="22" height="22" rx="5" class="box" />
          <path d="M16 25l4.5 4.5L30 20" class="tick-dark" />
          <rect x="40" y="16" width="20" height="4" rx="2" class="ink" /><rect x="40" y="26" width="16" height="4" rx="2" class="ink" />
          <rect x="12" y="46" width="46" height="4" rx="2" class="ink" /><rect x="12" y="56" width="40" height="4" rx="2" class="ink" />
          <text x="12" y="80" class="label">Receipt</text>
        </g>
        <g transform="translate(244 68) rotate(6 32 42)">
          <rect width="64" height="86" rx="9" fill="url(#card-face)" class="card" />
          <rect x="10" y="12" width="30" height="6" rx="3" class="ink-strong" />
          <rect x="10" y="28" width="44" height="4" rx="2" class="ink" /><rect x="10" y="38" width="36" height="4" rx="2" class="ink" />
          <rect x="34" y="52" width="20" height="6" rx="3" class="ink-strong" />
          <text x="10" y="76" class="label">Invoice</text>
        </g>
        <g transform="translate(150 140)">
          <circle r="20" cx="15" cy="15" class="seal-glow" />
          <circle r="14" cx="15" cy="15" class="seal" />
          <path d="M8.5 15.5l4.5 4.5 9-9.5" class="tick" />
        </g>
      </svg>
    </section>

    <app-error-banner [message]="store.error()" />

    @if (store.kpis(); as kpis) {
      <section class="kpi-grid" aria-label="Key performance indicators">
        <div class="kpi" [class.alert]="kpis.openExceptions > 0" [class.clear]="kpis.openExceptions === 0">
          <span class="kpi-label">Open exceptions</span>
          <span class="kpi-value">{{ kpis.openExceptions }}</span>
          <span class="kpi-note">{{ kpis.openExceptions > 0 ? 'Invoices blocked from payment' : 'Nothing blocked' }}</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">First-pass match rate</span>
          <span class="kpi-value">{{ kpis.firstPassMatchRatePct | number: '1.0-1' }}%</span>
          <span class="kpi-meter" aria-hidden="true"><span [style.width.%]="kpis.firstPassMatchRatePct"></span></span>
        </div>
        <div class="kpi">
          <span class="kpi-label">PO compliance (spend)</span>
          <span class="kpi-value">{{ kpis.poComplianceBySpendPct | number: '1.0-1' }}%</span>
          <span class="kpi-meter" aria-hidden="true"><span [style.width.%]="kpis.poComplianceBySpendPct"></span></span>
        </div>
        <div class="kpi">
          <span class="kpi-label">PO compliance (count)</span>
          <span class="kpi-value">{{ kpis.poComplianceByCountPct | number: '1.0-1' }}%</span>
          <span class="kpi-meter" aria-hidden="true"><span [style.width.%]="kpis.poComplianceByCountPct"></span></span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Retro PO rate</span>
          <span class="kpi-value">{{ kpis.retroPoRatePct | number: '1.0-1' }}%</span>
          <span class="kpi-note">Invoices raised before their PO</span>
        </div>
        <div class="kpi">
          <span class="kpi-label">Invoices</span>
          <span class="kpi-value">{{ kpis.invoiceCount }}</span>
          <span class="kpi-note">Received to date</span>
        </div>
      </section>
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }

    <section class="attention" aria-labelledby="attention-title">
      <div class="section-head">
        <h2 id="attention-title">Needs your attention</h2>
        @if (store.hasExceptions() && (store.kpis()?.openExceptions ?? 0) > store.exceptions().length) {
          <a routerLink="/invoices">See all {{ store.kpis()?.openExceptions }} blocked invoices</a>
        }
      </div>
      @if (store.hasExceptions()) {
        <ul class="exceptions">
          @for (item of store.exceptions(); track item.id) {
            <li data-testid="exception">
              <a [routerLink]="['/invoices', item.id]">
                <app-supplier-logo [name]="item.supplierName" [size]="40" />
                <span class="who">
                  <span class="invoice">{{ item.invoiceNumber }}</span>
                  <span class="supplier">{{ item.supplierName }}</span>
                </span>
                <span class="reason"><app-icon name="alert" [size]="15" /> {{ item.blockReason ?? 'No reason recorded' }}</span>
                <span class="amount">{{ item.total | number: '1.2-2' }}</span>
                <app-icon class="go" name="arrow" [size]="18" />
              </a>
            </li>
          }
        </ul>
      } @else if (!store.loading() && store.kpis()) {
        <app-empty-state tone="clear" title="All clear" text="No open exceptions. Every invoice is matched." />
      }
    </section>
  `,
  styles: `
    .hero {
      position: relative; overflow: hidden; display: grid; grid-template-columns: minmax(0, 1fr) 320px; align-items: center;
      gap: 1.5rem; padding: 1.9rem 2.1rem; margin-bottom: 1.5rem; border-radius: 16px; color: #fff;
      background:
        radial-gradient(90% 120% at 85% 10%, rgb(124 196 255 / 0.35), transparent 60%),
        radial-gradient(70% 90% at 0% 100%, rgb(18 46 104 / 0.65), transparent 70%),
        linear-gradient(135deg, #0d63bd 0%, #0a4a93 55%, #08356c 100%);
      box-shadow: var(--shadow-lg);
    }
    .hero::after {
      content: ''; position: absolute; inset: 0; pointer-events: none; opacity: 0.12;
      background-image: radial-gradient(rgb(255 255 255 / 0.9) 1px, transparent 1px);
      background-size: 18px 18px;
      mask-image: linear-gradient(to left, #000, transparent 70%);
    }
    .hero-copy { position: relative; z-index: 1; }
    .date { margin: 0; font-size: 0.8125rem; color: rgb(255 255 255 / 0.75); }
    .hero h1 { margin-top: 0.3rem; color: #fff; font-size: 1.875rem; }
    .summary { margin: 0.5rem 0 1.25rem; max-width: 48ch; font-size: 1rem; color: rgb(255 255 255 / 0.9); }
    .actions { display: flex; flex-wrap: wrap; gap: 0.6rem; }
    .button.light { background: #fff; border-color: #fff; color: #0a4a93; }
    .button.light:hover:not(:disabled) { background: #eaf2fc; border-color: #eaf2fc; }
    .button.ghost { background: rgb(255 255 255 / 0.12); border-color: rgb(255 255 255 / 0.28); color: #fff; }
    .button.ghost:hover:not(:disabled) { background: rgb(255 255 255 / 0.2); border-color: rgb(255 255 255 / 0.45); }
    .hero-art { position: relative; z-index: 1; width: 100%; height: auto; filter: drop-shadow(0 12px 20px rgb(0 20 60 / 0.35)); }
    .hero-art .card { stroke: rgb(255 255 255 / 0.6); stroke-width: 1; }
    .hero-art .ink { fill: #c9d8ea; }
    .hero-art .ink-strong { fill: #6b8db5; }
    .hero-art .box { fill: #e3f3ea; }
    .hero-art .tick-dark { fill: none; stroke: #107c41; stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; }
    .hero-art .label { font: 600 10px var(--font); fill: #0a4a93; }
    .hero-art .link { fill: none; stroke: rgb(255 255 255 / 0.55); stroke-width: 2; stroke-dasharray: 4 5; }
    .hero-art .seal-glow { fill: rgb(76 195 138 / 0.3); }
    .hero-art .seal { fill: #1f9d5c; stroke: #fff; stroke-width: 2; }
    .hero-art .tick { fill: none; stroke: #fff; stroke-width: 3; stroke-linecap: round; stroke-linejoin: round; }
    @media (prefers-reduced-motion: no-preference) {
      .hero-art .link { animation: flow 1.6s linear infinite; }
      @keyframes flow { to { stroke-dashoffset: -18; } }
    }

    .attention { margin-top: 2rem; }
    .section-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 0.75rem; }
    .section-head h2 { margin: 0; }
    .exceptions {
      list-style: none; margin: 0; padding: 0; background: var(--surface); border: 1px solid var(--line);
      border-radius: var(--radius-lg); box-shadow: var(--shadow); overflow: hidden;
    }
    .exceptions li + li { border-top: 1px solid var(--line); }
    .exceptions a {
      display: grid; grid-template-columns: auto minmax(11rem, 15rem) minmax(0, 1fr) auto auto; align-items: center;
      gap: 1rem; padding: 0.8rem 1.1rem; color: inherit; transition: background-color 120ms;
    }
    .exceptions a:hover { background: var(--row-hover); text-decoration: none; }
    .exceptions a:hover .go { color: var(--accent); transform: translateX(3px); }
    .who { min-width: 0; }
    .invoice { display: block; font-weight: 650; color: var(--accent); }
    .supplier { display: block; font-size: 0.8125rem; color: var(--ink-2); }
    .reason {
      display: inline-flex; align-items: center; gap: 0.4rem; justify-self: start; max-width: 100%;
      padding: 0.25rem 0.65rem; border-radius: 999px; background: var(--danger-soft); color: var(--danger);
      font-size: 0.8125rem; font-weight: 500;
    }
    .amount { font-weight: 650; white-space: nowrap; }
    .go { color: var(--ink-3); transition: transform 160ms, color 120ms; }
    @media (max-width: 1100px) {
      .hero { grid-template-columns: minmax(0, 1fr); }
      .hero-art { display: none; }
    }
    @media (max-width: 720px) {
      .hero { padding: 1.4rem 1.25rem; }
      .hero h1 { font-size: 1.5rem; }
      .exceptions a { grid-template-columns: auto minmax(0, 1fr) auto; }
      .reason { grid-column: 2 / -1; grid-row: 2; }
      .go { display: none; }
    }
  `,
})
export class DashboardPage {
  protected readonly store = inject(DashboardStore);
  protected readonly today = new Date();
  protected readonly greeting = greetingFor(this.today.getHours());

  constructor() {
    void this.store.load();
  }
}

function greetingFor(hour: number): string {
  return hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
}

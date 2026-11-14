import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorBanner } from '../shared/error-banner';
import { MatchTable } from '../shared/match-table';
import { StatusBadge } from '../shared/status-badge';
import { InvoicesStore } from './invoices.store';

@Component({
  selector: 'app-invoice-detail-page',
  imports: [DecimalPipe, RouterLink, ErrorBanner, MatchTable, StatusBadge],
  template: `
    <p><a routerLink="/invoices">Back to invoices</a></p>

    <app-error-banner [message]="store.error()" [dismissible]="true" (dismissed)="store.clearError()" />

    @if (store.current(); as inv) {
      <header class="page-header">
        <h1>
          Invoice {{ inv.invoiceNumber }}
          <app-status-badge [status]="inv.status" />
        </h1>
        @if (store.isBlocked()) {
          <button type="button" class="button" (click)="rematch()" [disabled]="store.saving()">Re-match</button>
        }
      </header>

      @if (inv.blockReason) {
        <p class="block-reason" data-testid="block-reason"><strong>Blocked:</strong> {{ inv.blockReason }}</p>
      }

      <dl class="details">
        <div><dt>Supplier</dt><dd>{{ inv.supplierName }} ({{ inv.supplierCode }})</dd></div>
        <div>
          <dt>Purchase order</dt>
          <dd>
            @if (inv.poId) {
              <a [routerLink]="['/purchase-orders', inv.poId]">{{ inv.poNumber }}</a>
            } @else {
              {{ inv.poNumber ?? '—' }}
            }
          </dd>
        </div>
        <div><dt>Invoice date</dt><dd>{{ inv.invoiceDate }}</dd></div>
        <div><dt>Due date</dt><dd>{{ inv.dueDate }}</dd></div>
        <div><dt>Subtotal</dt><dd>{{ inv.subtotal | number: '1.2-2' }} {{ inv.currency }}</dd></div>
        <div><dt>Tax</dt><dd>{{ inv.taxAmount | number: '1.2-2' }} {{ inv.currency }}</dd></div>
        <div><dt>Total</dt><dd class="total">{{ inv.total | number: '1.2-2' }} {{ inv.currency }}</dd></div>
        @if (inv.isRetroPo) {
          <div><dt>Retro PO</dt><dd>PO raised after the invoice date</dd></div>
        }
      </dl>

      <h2>Lines</h2>
      <table class="table">
        <thead>
          <tr>
            <th class="num">Line</th>
            <th class="num">PO line</th>
            <th>Item</th>
            <th>Description</th>
            <th class="num">Quantity</th>
            <th>UoM</th>
            <th class="num">Unit price</th>
            <th class="num">Amount</th>
          </tr>
        </thead>
        <tbody>
          @for (line of inv.lines; track line.lineNo) {
            <tr>
              <td class="num">{{ line.lineNo }}</td>
              <td class="num">{{ line.poLineNo ?? '—' }}</td>
              <td class="mono">{{ line.itemCode }}</td>
              <td>{{ line.description }}</td>
              <td class="num">{{ line.quantity | number: '1.0-3' }}</td>
              <td>{{ line.uom }}</td>
              <td class="num">{{ line.unitPrice | number: '1.2-4' }}</td>
              <td class="num">{{ line.amount | number: '1.2-2' }}</td>
            </tr>
          }
        </tbody>
      </table>

      <h2>3-way match</h2>
      <app-match-table [checks]="inv.checks" />
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }
  `,
})
export class InvoiceDetailPage {
  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly store = inject(InvoicesStore);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.store.clearError();
        void this.store.loadOne(id);
      });
    });
  }

  protected rematch(): void {
    void this.store.rematch(this.id());
  }
}

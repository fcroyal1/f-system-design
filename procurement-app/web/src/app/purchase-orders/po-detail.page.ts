import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CreateReceiptRequest } from '../core/models';
import { ErrorBanner } from '../shared/error-banner';
import { StatusBadge } from '../shared/status-badge';
import { PurchaseOrdersStore } from './purchase-orders.store';
import { ReceiveForm } from './receive-form';

@Component({
  selector: 'app-po-detail-page',
  imports: [DatePipe, DecimalPipe, RouterLink, ErrorBanner, StatusBadge, ReceiveForm],
  template: `
    <p><a routerLink="/purchase-orders">Back to purchase orders</a></p>

    <app-error-banner [message]="store.error()" [dismissible]="true" (dismissed)="store.clearError()" />

    @for (notice of store.notices(); track notice) {
      <div class="notice" role="status" data-testid="notice">{{ notice }}</div>
    }

    @if (store.current(); as po) {
      <header class="page-header">
        <h1>
          PO {{ po.number }}
          <app-status-badge [status]="po.status" />
        </h1>
        @if (store.canApprove()) {
          <button type="button" class="button" (click)="approve(po.id)" [disabled]="store.saving() || po.id !== id()">Approve</button>
        }
      </header>

      <dl class="details">
        <div><dt>Supplier</dt><dd>{{ po.supplierName }} ({{ po.supplierCode }})</dd></div>
        <div><dt>Requester</dt><dd>{{ po.requesterName }}</dd></div>
        <div><dt>Cost center</dt><dd>{{ po.costCenterCode }}</dd></div>
        <div><dt>PO date</dt><dd>{{ po.poDate }}</dd></div>
        <div><dt>Total</dt><dd>{{ po.total | number: '1.2-2' }} {{ po.currency }}</dd></div>
        <div><dt>Approver role</dt><dd>{{ po.approverRole ?? '—' }}</dd></div>
        @if (po.approvedBy) {
          <div><dt>Approved</dt><dd>{{ po.approvedBy }}, {{ po.approvedAt | date: 'medium' }}</dd></div>
        }
      </dl>

      <h2>Lines</h2>
      <table class="table">
        <thead>
          <tr>
            <th class="num">Line</th>
            <th>Item</th>
            <th>Description</th>
            <th class="num">Quantity</th>
            <th>UoM</th>
            <th class="num">Unit price</th>
            <th class="num">Amount</th>
            <th>GL</th>
            <th>Receipt required</th>
          </tr>
        </thead>
        <tbody>
          @for (line of po.lines; track line.lineNo) {
            <tr>
              <td class="num">{{ line.lineNo }}</td>
              <td class="mono">{{ line.itemCode }}</td>
              <td>{{ line.description }}</td>
              <td class="num">{{ line.quantity | number: '1.0-3' }}</td>
              <td>{{ line.uom }}</td>
              <td class="num">{{ line.unitPrice | number: '1.2-4' }}</td>
              <td class="num">{{ line.amount | number: '1.2-2' }}</td>
              <td class="mono">{{ line.glAccount }}</td>
              <td>{{ line.receiptRequired ? 'Yes' : 'No' }}</td>
            </tr>
          }
        </tbody>
      </table>

      <h2>Goods receipts</h2>
      @if (po.receipts.length === 0) {
        <p class="muted">Nothing received yet.</p>
      } @else {
        <table class="table">
          <thead>
            <tr>
              <th>GR number</th>
              <th>Received</th>
              <th>Received by</th>
              <th>Packing slip</th>
              <th class="num">Quantity</th>
            </tr>
          </thead>
          <tbody>
            @for (r of po.receipts; track r.id) {
              <tr>
                <td class="mono">{{ r.grNumber }}</td>
                <td>{{ r.receivedAt | date: 'medium' }}</td>
                <td>{{ r.receivedBy }}</td>
                <td>{{ r.packingSlip ?? '—' }}</td>
                <td class="num">{{ r.quantityReceived | number: '1.0-3' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (store.canReceive()) {
        <h2>Receive goods</h2>
        <app-receive-form [lines]="po.lines" [saving]="store.saving() || po.id !== id()" (submitted)="receive(po.id, $event)" />
      }

      <h2>Invoices</h2>
      @if (po.invoices.length === 0) {
        <p class="muted">No invoices against this PO.</p>
      } @else {
        <table class="table">
          <thead>
            <tr>
              <th>Invoice</th>
              <th>Invoice date</th>
              <th>Due</th>
              <th class="num">Total</th>
              <th>Status</th>
              <th>Reason</th>
            </tr>
          </thead>
          <tbody>
            @for (i of po.invoices; track i.id) {
              <tr>
                <td><a [routerLink]="['/invoices', i.id]">{{ i.invoiceNumber }}</a></td>
                <td>{{ i.invoiceDate }}</td>
                <td>{{ i.dueDate }}</td>
                <td class="num">{{ i.total | number: '1.2-2' }} {{ i.currency }}</td>
                <td><app-status-badge [status]="i.status" /></td>
                <td>{{ i.blockReason ?? '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    } @else if (store.loading()) {
      <p class="muted">Loading…</p>
    }
  `,
})
export class PoDetailPage {
  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly store = inject(PurchaseOrdersStore);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.store.clearError();
        void this.store.loadOne(id);
      });
    });
  }

  /** Acts on the PO actually displayed, and only once it is the PO the route asks for. */
  protected approve(poId: string): void {
    if (poId !== this.id()) return;
    void this.store.approve(poId);
  }

  /** The receipt's line numbers come from the displayed PO, so it is posted to that PO and no other. */
  protected receive(poId: string, request: CreateReceiptRequest): void {
    if (poId !== this.id()) return;
    void this.store.receive(poId, request);
  }
}

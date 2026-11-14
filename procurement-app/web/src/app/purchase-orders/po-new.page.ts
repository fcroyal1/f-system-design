import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { today } from '../core/dates';
import { ReferenceStore } from '../core/reference.store';
import { ErrorBanner } from '../shared/error-banner';
import { SuppliersStore } from '../suppliers/suppliers.store';
import { PurchaseOrdersStore } from './purchase-orders.store';

@Component({
  selector: 'app-po-new-page',
  imports: [ReactiveFormsModule, RouterLink, ErrorBanner],
  template: `
    <header class="page-header">
      <h1>New purchase order</h1>
      <a routerLink="/purchase-orders">Back to purchase orders</a>
    </header>

    <app-error-banner [message]="reference.error() ?? suppliers.error()" />

    <form class="form wide" [formGroup]="form" (ngSubmit)="submit()">
      <div class="form-row">
        <label>
          Supplier
          <select formControlName="supplierId" required>
            <option value="" disabled>Select a supplier</option>
            @for (s of suppliers.items(); track s.id) {
              <option [value]="s.id">{{ s.code }} · {{ s.legalName }}{{ s.isBlocked ? ' (blocked)' : '' }}</option>
            }
          </select>
        </label>
        <label>
          Requester
          <input formControlName="requesterName" required maxlength="100" />
        </label>
        <label>
          Cost center
          <select formControlName="costCenterCode" required>
            <option value="" disabled>Select a cost center</option>
            @for (c of reference.costCenters(); track c.code) {
              <option [value]="c.code">{{ c.code }} · {{ c.name }}</option>
            }
          </select>
        </label>
        <label>
          Currency
          <input formControlName="currency" required minlength="3" maxlength="3" class="short" />
        </label>
        <label>
          PO date
          <input type="date" formControlName="poDate" required />
        </label>
      </div>

      <fieldset>
        <legend>Lines</legend>
        <table class="table line-table" formArrayName="lines">
          <thead>
            <tr>
              <th>Item</th>
              <th>Description</th>
              <th>UoM</th>
              <th class="num">Quantity</th>
              <th class="num">Unit price</th>
              <th>GL account</th>
              <th>Receipt required</th>
              <th><span class="visually-hidden">Remove</span></th>
            </tr>
          </thead>
          <tbody>
            @for (line of lines.controls; track line; let i = $index) {
              <tr [formGroupName]="i">
                <td>
                  <label><span class="visually-hidden">Line {{ i + 1 }} item code</span>
                    <input formControlName="itemCode" required maxlength="40" /></label>
                </td>
                <td>
                  <label><span class="visually-hidden">Line {{ i + 1 }} description</span>
                    <input formControlName="description" required maxlength="200" /></label>
                </td>
                <td>
                  <label><span class="visually-hidden">Line {{ i + 1 }} unit of measure</span>
                    <input formControlName="uom" required maxlength="10" class="short" /></label>
                </td>
                <td class="num">
                  <label><span class="visually-hidden">Line {{ i + 1 }} quantity</span>
                    <input type="number" formControlName="quantity" required min="0" step="any" /></label>
                </td>
                <td class="num">
                  <label><span class="visually-hidden">Line {{ i + 1 }} unit price</span>
                    <input type="number" formControlName="unitPrice" required min="0" step="any" /></label>
                </td>
                <td>
                  <label><span class="visually-hidden">Line {{ i + 1 }} GL account</span>
                    <select formControlName="glAccount" required>
                      <option value="" disabled>Select</option>
                      @for (g of reference.glAccounts(); track g.number) {
                        <option [value]="g.number">{{ g.number }} · {{ g.name }}</option>
                      }
                    </select></label>
                </td>
                <td>
                  <label class="inline"><input type="checkbox" formControlName="receiptRequired" />
                    <span class="visually-hidden">Line {{ i + 1 }}</span> Required</label>
                </td>
                <td>
                  <button type="button" class="button secondary" (click)="removeLine(i)" [disabled]="lines.length === 1">
                    Remove
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <button type="button" class="button secondary" (click)="addLine()">Add line</button>
      </fieldset>

      <app-error-banner [message]="store.error()" />

      <div class="form-actions">
        <button type="submit" class="button" [disabled]="form.invalid || store.saving()">
          {{ store.saving() ? 'Saving…' : 'Create purchase order' }}
        </button>
      </div>
    </form>
  `,
})
export class PoNewPage {
  protected readonly store = inject(PurchaseOrdersStore);
  protected readonly reference = inject(ReferenceStore);
  protected readonly suppliers = inject(SuppliersStore);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    requesterName: ['', [Validators.required, Validators.maxLength(100)]],
    costCenterCode: ['', Validators.required],
    currency: ['USD', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    poDate: [today(), Validators.required],
    lines: this.fb.array([this.newLine()]),
  });

  protected get lines() {
    return this.form.controls.lines;
  }

  constructor() {
    this.store.clearError();
    void this.reference.load();
    void this.suppliers.load();
  }

  protected addLine(): void {
    this.lines.push(this.newLine());
  }

  protected removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.getRawValue();
    const po = await this.store.create({
      supplierId: value.supplierId,
      requesterName: value.requesterName.trim(),
      costCenterCode: value.costCenterCode,
      currency: value.currency.trim().toUpperCase(),
      poDate: value.poDate,
      lines: value.lines.map((l) => ({
        itemCode: l.itemCode.trim(),
        description: l.description.trim(),
        uom: l.uom.trim(),
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        glAccount: l.glAccount,
        receiptRequired: l.receiptRequired,
      })),
    });
    if (po) {
      await this.router.navigate(['/purchase-orders', po.id]);
    }
  }

  private newLine() {
    return this.fb.group({
      itemCode: ['', [Validators.required, Validators.maxLength(40)]],
      description: ['', [Validators.required, Validators.maxLength(200)]],
      uom: ['', [Validators.required, Validators.maxLength(10)]],
      quantity: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
      unitPrice: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
      glAccount: ['', Validators.required],
      receiptRequired: [true],
    });
  }
}

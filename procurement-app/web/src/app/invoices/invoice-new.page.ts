import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { today } from '../core/dates';
import { ErrorBanner } from '../shared/error-banner';
import { SuppliersStore } from '../suppliers/suppliers.store';
import { InvoicesStore } from './invoices.store';

@Component({
  selector: 'app-invoice-new-page',
  imports: [ReactiveFormsModule, RouterLink, ErrorBanner],
  template: `
    <header class="page-header">
      <h1>New invoice</h1>
      <a routerLink="/invoices">Back to invoices</a>
    </header>

    <app-error-banner [message]="suppliers.error()" />

    <form class="form wide" [formGroup]="form" (ngSubmit)="submit()">
      <div class="form-row">
        <label>
          Supplier
          <select formControlName="supplierId" required>
            <option value="" disabled>Select a supplier</option>
            @for (s of suppliers.items(); track s.id) {
              <option [value]="s.id">{{ s.code }} · {{ s.legalName }}</option>
            }
          </select>
        </label>
        <label>
          Invoice number
          <input formControlName="invoiceNumber" required maxlength="40" />
        </label>
        <label>
          Invoice date
          <input type="date" formControlName="invoiceDate" required />
        </label>
        <label>
          Currency
          <input formControlName="currency" required minlength="3" maxlength="3" class="short" />
        </label>
        <label>
          PO number
          <input formControlName="poNumber" required maxlength="20" />
        </label>
        <label>
          Tax amount
          <input type="number" formControlName="taxAmount" min="0" step="0.01" />
        </label>
      </div>

      <fieldset>
        <legend>Lines</legend>
        <table class="table line-table" formArrayName="lines">
          <thead>
            <tr>
              <th class="num">PO line</th>
              <th>Item</th>
              <th>Description</th>
              <th>UoM</th>
              <th class="num">Quantity</th>
              <th class="num">Unit price</th>
              <th><span class="visually-hidden">Remove</span></th>
            </tr>
          </thead>
          <tbody>
            @for (line of lines.controls; track line; let i = $index) {
              <tr [formGroupName]="i">
                <td class="num">
                  <label><span class="visually-hidden">Line {{ i + 1 }} PO line number</span>
                    <input type="number" formControlName="poLineNo" min="1" step="1" class="short" /></label>
                </td>
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

      <!-- 400 validation and 409 duplicate problems are shown here, next to the submit button. -->
      <app-error-banner [message]="store.error()" />

      <div class="form-actions">
        <button type="submit" class="button" [disabled]="form.invalid || store.saving()">
          {{ store.saving() ? 'Saving…' : 'Save and match' }}
        </button>
      </div>
    </form>
  `,
})
export class InvoiceNewPage {
  protected readonly store = inject(InvoicesStore);
  protected readonly suppliers = inject(SuppliersStore);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    invoiceNumber: ['', [Validators.required, Validators.maxLength(40)]],
    invoiceDate: [today(), Validators.required],
    currency: ['USD', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    poNumber: ['', [Validators.required, Validators.maxLength(20)]],
    taxAmount: this.fb.control<number | null>(0, Validators.min(0)),
    lines: this.fb.array([this.newLine(1)]),
  });

  protected get lines() {
    return this.form.controls.lines;
  }

  constructor() {
    this.store.clearError();
    void this.suppliers.load();
  }

  protected addLine(): void {
    this.lines.push(this.newLine(this.lines.length + 1));
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
    const invoice = await this.store.create({
      supplierId: value.supplierId,
      invoiceNumber: value.invoiceNumber.trim(),
      invoiceDate: value.invoiceDate,
      currency: value.currency.trim().toUpperCase(),
      poNumber: value.poNumber.trim(),
      taxAmount: value.taxAmount,
      lines: value.lines.map((l) => ({
        poLineNo: l.poLineNo,
        itemCode: l.itemCode.trim(),
        uom: l.uom.trim(),
        description: l.description.trim(),
        quantity: l.quantity,
        unitPrice: l.unitPrice,
      })),
    });
    if (invoice) {
      await this.router.navigate(['/invoices', invoice.id]);
    }
  }

  private newLine(poLineNo: number) {
    return this.fb.group({
      poLineNo: this.fb.control<number | null>(poLineNo, Validators.min(1)),
      itemCode: ['', [Validators.required, Validators.maxLength(40)]],
      description: ['', [Validators.required, Validators.maxLength(200)]],
      uom: ['', [Validators.required, Validators.maxLength(10)]],
      quantity: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
      unitPrice: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
    });
  }
}

import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ReferenceStore } from '../core/reference.store';
import { ErrorBanner } from '../shared/error-banner';
import { SuppliersStore } from './suppliers.store';

@Component({
  selector: 'app-supplier-new-page',
  imports: [ReactiveFormsModule, RouterLink, ErrorBanner],
  template: `
    <header class="page-header">
      <h1>New supplier</h1>
      <a routerLink="/suppliers">Back to suppliers</a>
    </header>

    <app-error-banner [message]="store.error() ?? reference.error()" />

    <form class="form" [formGroup]="form" (ngSubmit)="submit()">
      <label>
        Code
        <input formControlName="code" required maxlength="20" />
      </label>
      <label>
        Legal name
        <input formControlName="legalName" required maxlength="200" />
      </label>
      <label>
        Category
        <select formControlName="categoryCode" required>
          <option value="" disabled>Select a category</option>
          @for (c of reference.categories(); track c.code) {
            <option [value]="c.code">{{ c.name }}</option>
          }
        </select>
      </label>
      <label>
        Payment terms (days)
        <input type="number" formControlName="paymentTermsDays" min="0" required />
      </label>
      <label>
        Tax ID
        <input formControlName="taxId" autocomplete="off" spellcheck="false" />
        <small class="muted">Only the last 4 characters are stored.</small>
      </label>
      <label>
        Bank account
        <input formControlName="bankAccount" autocomplete="off" spellcheck="false" />
        <small class="muted">Only the last 4 characters are stored.</small>
      </label>

      <div class="form-actions">
        <button type="submit" class="button" [disabled]="form.invalid || store.saving()">
          {{ store.saving() ? 'Saving…' : 'Create supplier' }}
        </button>
      </div>
    </form>
  `,
})
export class SupplierNewPage {
  protected readonly store = inject(SuppliersStore);
  protected readonly reference = inject(ReferenceStore);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    legalName: ['', [Validators.required, Validators.maxLength(200)]],
    categoryCode: ['', Validators.required],
    paymentTermsDays: [30, [Validators.required, Validators.min(0)]],
    taxId: [''],
    bankAccount: [''],
  });

  constructor() {
    this.store.clearError();
    void this.reference.load();
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.getRawValue();
    const request = this.store.create({
      code: value.code.trim(),
      legalName: value.legalName.trim(),
      categoryCode: value.categoryCode,
      paymentTermsDays: value.paymentTermsDays,
      taxId: value.taxId.trim() || null,
      bankAccount: value.bankAccount.trim() || null,
    });
    // The full numbers have been sent; never keep them in the form, whatever the outcome.
    this.form.patchValue({ taxId: '', bankAccount: '' });

    if (await request) {
      await this.router.navigate(['/suppliers']);
    }
  }
}

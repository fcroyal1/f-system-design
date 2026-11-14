import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateReceiptRequest, PoLine } from '../core/models';

/**
 * Goods receipt entry for an Approved PO: who received the goods, the packing slip and a quantity per PO line.
 * Only lines with a quantity above zero are sent. The form is rebuilt (blank) whenever the PO's lines change,
 * which is what happens when the PO is reloaded after a receipt.
 */
@Component({
  selector: 'app-receive-form',
  imports: [ReactiveFormsModule, DecimalPipe],
  template: `
    @let f = form();
    <form class="form wide" data-testid="receive-form" [formGroup]="f" (ngSubmit)="submit()">
      <div class="form-row">
        <label>
          Received by
          <input formControlName="receivedBy" data-testid="received-by" required maxlength="100" />
        </label>
        <label>
          Packing slip
          <input formControlName="packingSlip" data-testid="packing-slip" maxlength="40" />
        </label>
      </div>

      <table class="table">
        <thead>
          <tr>
            <th class="num">Line</th>
            <th>Item</th>
            <th class="num">Ordered</th>
            <th class="num">Receive now</th>
          </tr>
        </thead>
        <tbody formArrayName="quantities">
          @for (line of lines(); track line.lineNo; let i = $index) {
            <tr>
              <td class="num">{{ line.lineNo }}</td>
              <td>{{ line.itemCode }} · {{ line.description }}</td>
              <td class="num">{{ line.quantity | number: '1.0-3' }} {{ line.uom }}</td>
              <td class="num">
                <label>
                  <span class="visually-hidden">Quantity to receive on line {{ line.lineNo }}</span>
                  <input
                    type="number"
                    min="0"
                    step="any"
                    class="qty-input"
                    [formControlName]="i"
                    [attr.data-testid]="'receive-qty-' + line.lineNo"
                  />
                </label>
              </td>
            </tr>
          }
        </tbody>
      </table>

      <div class="form-actions">
        <button type="submit" class="button" [disabled]="!canSubmit() || saving()">
          {{ saving() ? 'Recording…' : 'Record receipt' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .qty-input {
      width: 8rem;
      text-align: right;
    }
  `,
})
export class ReceiveForm {
  readonly lines = input.required<PoLine[]>();
  readonly saving = input(false);
  readonly submitted = output<CreateReceiptRequest>();

  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly form = computed(() =>
    this.fb.group({
      receivedBy: ['', [Validators.required, Validators.maxLength(100)]],
      packingSlip: ['', Validators.maxLength(40)],
      quantities: this.fb.array(
        this.lines().map(() => new FormControl<number | null>(null, Validators.min(0))),
      ),
    }),
  );

  protected canSubmit(): boolean {
    return this.toRequest() !== null;
  }

  protected submit(): void {
    const request = this.toRequest();
    if (request) {
      this.submitted.emit(request);
    }
  }

  /** The receipt request, or null when the form is invalid or no line has a quantity above zero. */
  private toRequest(): CreateReceiptRequest | null {
    const form = this.form();
    if (form.invalid) {
      return null;
    }
    const value = form.getRawValue();
    const lines = this.lines()
      .map((line, i) => ({ poLineNo: line.lineNo, quantity: value.quantities[i] }))
      .filter((l) => l.quantity !== null && l.quantity > 0);
    if (lines.length === 0 || value.receivedBy.trim() === '') {
      return null;
    }
    return {
      receivedBy: value.receivedBy.trim(),
      packingSlip: value.packingSlip.trim() || null,
      lines,
    };
  }
}

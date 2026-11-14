import { Component, input } from '@angular/core';
import { MatchCheck } from '../core/models';

/**
 * The 3-way match result: one row per check, in the order the engine ran them. Values are shown exactly as the
 * API sends them ("—" where a document has no value for the check). Failed rows carry `row-fail`.
 */
@Component({
  selector: 'app-match-table',
  template: `
    <table class="table match-table">
      <thead>
        <tr>
          <th>Check</th>
          <th>Purchase order</th>
          <th>Goods receipt</th>
          <th>Invoice</th>
          <th>Result</th>
        </tr>
      </thead>
      <tbody>
        @for (check of checks(); track check.seq) {
          <tr [class.row-fail]="check.outcome === 'Fail'" [class.row-pass]="check.outcome === 'Pass'">
            <td>
              <span data-testid="check-name">{{ check.name }}</span>
              @if (check.lineNo !== null) {
                <small class="muted line-no">Line {{ check.lineNo }}</small>
              }
            </td>
            <td class="mono">{{ check.poValue }}</td>
            <td class="mono">{{ check.receiptValue }}</td>
            <td class="mono">{{ check.invoiceValue }}</td>
            <td>
              <span class="outcome" data-testid="outcome">{{ check.outcome }}</span>
              <span class="message" data-testid="message">{{ check.message }}</span>
            </td>
          </tr>
        } @empty {
          <tr>
            <td colspan="5" class="muted">No match checks recorded.</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    .line-no {
      display: block;
      font-size: 0.75rem;
    }
    .outcome {
      display: block;
      font-weight: 700;
    }
    .message {
      display: block;
      font-size: 0.85rem;
      color: #475569;
    }
    .row-pass .outcome {
      color: #18663b;
    }
    .row-fail {
      background: #fdecec;
    }
    .row-fail .outcome,
    .row-fail .message {
      color: #a61b1b;
    }
  `,
})
export class MatchTable {
  readonly checks = input.required<MatchCheck[]>();
}

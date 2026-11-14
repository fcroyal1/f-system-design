import { TestBed } from '@angular/core/testing';
import { MatchTable } from './match-table';
import { guideChecks } from '../testing/fixtures';

describe('MatchTable', () => {
  it('MatchTable renders the guide example rows in order with Quantity and Amount failed', async () => {
    const fixture = TestBed.createComponent(MatchTable);
    fixture.componentRef.setInput('checks', guideChecks);
    await fixture.whenStable();

    const table = fixture.nativeElement as HTMLElement;
    const headers = Array.from(table.querySelectorAll('thead th')).map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Check', 'Purchase order', 'Goods receipt', 'Invoice', 'Result']);

    const rows = Array.from(table.querySelectorAll('tbody tr'));
    expect(rows.map((r) => r.querySelector('[data-testid="check-name"]')?.textContent?.trim())).toEqual([
      'Supplier',
      'Currency',
      'PO status',
      'PO line',
      'Quantity',
      'Unit price',
      'Amount',
    ]);

    const failed = rows.filter((r) => r.classList.contains('row-fail'));
    expect(failed.map((r) => r.querySelector('[data-testid="check-name"]')?.textContent?.trim())).toEqual([
      'Quantity',
      'Amount',
    ]);

    // Quantity: PO 100, received 98, invoiced 100, Fail, with the engine's message under the result.
    const quantity = Array.from(rows[4].querySelectorAll('td')).map((td) => td.textContent?.trim());
    expect(quantity[1]).toBe('100');
    expect(quantity[2]).toBe('98');
    expect(quantity[3]).toBe('100');
    const result = rows[4].querySelectorAll('td')[4];
    expect(result.querySelector('[data-testid="outcome"]')?.textContent?.trim()).toBe('Fail');
    expect(result.querySelector('[data-testid="message"]')?.textContent?.trim()).toBe(
      'billed 2 more than received (line 1)',
    );

    // "—" is shown exactly as the API sends it.
    const supplier = Array.from(rows[0].querySelectorAll('td')).map((td) => td.textContent?.trim());
    expect(supplier.slice(1, 4)).toEqual(['—', '—', '—']);
    expect(rows[6].querySelectorAll('td')[4].textContent).toContain('$40.00 more than the value received');
  });
});

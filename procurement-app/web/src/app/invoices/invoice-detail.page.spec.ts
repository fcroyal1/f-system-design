import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { InvoiceDetailPage } from './invoice-detail.page';
import { invoice, invoiceId, invoiceView } from '../testing/fixtures';

function rematchButton(page: HTMLElement): HTMLButtonElement | undefined {
  return Array.from(page.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Re-match');
}

describe('InvoiceDetailPage', () => {
  it('Invoice detail shows Re-match only when Blocked', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(InvoiceDetailPage);
    fixture.componentRef.setInput('id', invoiceId);
    fixture.detectChanges();

    http.expectOne(`/bff/invoices/${invoiceId}`).flush(invoiceView());
    await fixture.whenStable();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('INV-77812');
    expect(page.textContent).toContain('Quantity: billed 2 more than received (line 1)');
    expect(page.querySelectorAll('app-match-table tbody tr').length).toBe(7);
    const button = rematchButton(page);
    expect(button).toBeTruthy();

    // Re-matching after the missing 2 boxes arrive turns the invoice Matched; the button goes away.
    button!.click();
    http
      .expectOne({ method: 'POST', url: `/api/invoices/${invoiceId}/rematch` })
      .flush(
        invoice({
          status: 'Matched',
          blockReason: null,
          checks: invoice().checks.map((c) => ({ ...c, outcome: 'Pass' as const })),
        }),
      );
    await fixture.whenStable();

    expect(rematchButton(page)).toBeUndefined();
    expect(page.querySelector('.badge')?.textContent?.trim()).toBe('Matched');
    expect(page.querySelectorAll('app-match-table tr.row-fail').length).toBe(0);

    http.verify();
  });
});

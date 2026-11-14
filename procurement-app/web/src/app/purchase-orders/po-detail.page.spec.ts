import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { PoDetailPage } from './po-detail.page';
import { poId, purchaseOrderView, receiptFor2More } from '../testing/fixtures';

/** Lets awaited HTTP continuations run so the store issues its next request. */
const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

function type(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

describe('PoDetailPage', () => {
  it('PO detail after receiving reports the invoice that changed to Matched', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(PoDetailPage);
    fixture.componentRef.setInput('id', poId);
    fixture.detectChanges();

    http.expectOne(`/bff/purchase-orders/${poId}`).flush(purchaseOrderView());
    await fixture.whenStable();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('4500123');
    expect(page.textContent).not.toContain('is now Matched');

    type(page.querySelector<HTMLInputElement>('[data-testid="received-by"]')!, 'Sam Okafor');
    type(page.querySelector<HTMLInputElement>('[data-testid="packing-slip"]')!, 'PS-4471');
    type(page.querySelector<HTMLInputElement>('[data-testid="receive-qty-1"]')!, '2');
    fixture.detectChanges();
    page.querySelector<HTMLFormElement>('[data-testid="receive-form"]')!.dispatchEvent(new Event('submit'));

    const post = http.expectOne({ method: 'POST', url: `/api/purchase-orders/${poId}/receipts` });
    expect(post.request.body).toEqual({
      receivedBy: 'Sam Okafor',
      packingSlip: 'PS-4471',
      lines: [{ poLineNo: 1, quantity: 2 }],
    });
    post.flush(receiptFor2More, { status: 201, statusText: 'Created' });
    await settle();

    // The page reloads the PO, which now shows the second receipt and the invoice as Matched.
    const before = purchaseOrderView().po;
    http.expectOne({ method: 'GET', url: `/bff/purchase-orders/${poId}` }).flush(
      purchaseOrderView({
        receipts: [
          ...before.receipts,
          {
            id: receiptFor2More.id,
            grNumber: '5000988',
            receivedAt: receiptFor2More.receivedAt,
            receivedBy: 'Sam Okafor',
            packingSlip: 'PS-4471',
            quantityReceived: 2,
          },
        ],
        invoices: before.invoices.map((i) => ({ ...i, status: 'Matched' as const, blockReason: null })),
      }),
    );
    await fixture.whenStable();

    const notice = page.querySelector('[data-testid="notice"]');
    expect(notice?.textContent).toContain('Invoice INV-77812 is now Matched');
    expect(page.textContent).toContain('5000988');

    http.verify();
  });
});

import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PurchaseOrdersStore } from './purchase-orders.store';
import { poId, problem, purchaseOrder, purchaseOrderView } from '../testing/fixtures';

describe('PurchaseOrdersStore', () => {
  let store: InstanceType<typeof PurchaseOrdersStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(PurchaseOrdersStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('PurchaseOrdersStore approve updates status and approver role', async () => {
    const draft = { status: 'Draft' as const, approverRole: null, approvedBy: null, approvedAt: null, receipts: [], invoices: [] };
    const loaded = store.loadOne(poId);
    http.expectOne(`/bff/purchase-orders/${poId}`).flush(purchaseOrderView(draft));
    await loaded;
    expect(store.current()?.status).toBe('Draft');

    const done = store.approve(poId);
    expect(store.saving()).toBe(true);
    const req = http.expectOne({ method: 'POST', url: `/api/purchase-orders/${poId}/approve` });
    req.flush(
      purchaseOrder({
        ...draft,
        status: 'Approved',
        approverRole: 'Cost center owner',
        approvedBy: 'Demo User',
        approvedAt: '2026-10-02T09:00:00',
      }),
    );

    expect((await done)?.status).toBe('Approved');
    expect(store.current()?.status).toBe('Approved');
    expect(store.current()?.approverRole).toBe('Cost center owner');
    expect(store.current()?.approvedBy).toBe('Demo User');
    expect(store.saving()).toBe(false);
    expect(store.error()).toBeNull();
  });

  it('PurchaseOrdersStore receive 422 sets error "Line 1: receiving 3 would bring the total to 101, more than the 100 ordered"', async () => {
    const loaded = store.loadOne(poId);
    http.expectOne(`/bff/purchase-orders/${poId}`).flush(purchaseOrderView());
    await loaded;

    const done = store.receive(poId, { receivedBy: 'Sam Okafor', packingSlip: null, lines: [{ poLineNo: 1, quantity: 3 }] });
    const { body, init } = problem(
      422,
      'Rule violation',
      'Line 1: receiving 3 would bring the total to 101, more than the 100 ordered',
    );
    http.expectOne({ method: 'POST', url: `/api/purchase-orders/${poId}/receipts` }).flush(body, init);

    expect(await done).toBeNull();
    expect(store.error()).toBe('Line 1: receiving 3 would bring the total to 101, more than the 100 ordered');
    expect(store.notices()).toEqual([]);
    expect(store.saving()).toBe(false);
    // The PO is not reloaded after a rejected receipt.
    expect(store.current()?.receipts.length).toBe(1);
  });
});

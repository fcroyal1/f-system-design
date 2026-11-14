import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InvoicesStore } from './invoices.store';
import { CreateInvoiceRequest } from '../core/models';
import { invoiceView, problem, supplierId } from '../testing/fixtures';

const invA = 'a1b2c3d4-0004-4000-8000-00000000000a';
const invB = 'a1b2c3d4-0004-4000-8000-00000000000b';

const duplicate: CreateInvoiceRequest = {
  supplierId,
  invoiceNumber: 'inv77812',
  invoiceDate: '2026-10-08',
  currency: 'USD',
  poNumber: '4500123',
  taxAmount: 0,
  lines: [
    { poLineNo: 1, itemCode: 'GLV-NIT-M', uom: 'BX', description: 'Nitrile gloves', quantity: 100, unitPrice: 10.2 },
  ],
};

describe('InvoicesStore', () => {
  let store: InstanceType<typeof InvoicesStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(InvoicesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('InvoicesStore create 409 sets duplicate error', async () => {
    const done = store.create(duplicate);
    expect(store.saving()).toBe(true);

    const req = http.expectOne({ method: 'POST', url: '/api/invoices' });
    expect(req.request.body).toEqual(duplicate);
    const { body, init } = problem(409, 'Conflict', 'Invoice INV-77812 from Bluepeak Medical Supply is already recorded');
    req.flush(body, init);

    expect(await done).toBeNull();
    expect(store.error()).toBe('Invoice INV-77812 from Bluepeak Medical Supply is already recorded');
    expect(store.saving()).toBe(false);
    expect(store.items()).toEqual([]);
  });

  it('InvoicesStore ignores a late response for a previous id', async () => {
    const loadA = store.loadOne(invA);
    const loadB = store.loadOne(invB);
    const reqA = http.expectOne(`/bff/invoices/${invA}`);
    http.expectOne(`/bff/invoices/${invB}`).flush(invoiceView({ id: invB, invoiceNumber: 'INV-B' }));
    await loadB;
    reqA.flush(invoiceView({ id: invA, invoiceNumber: 'INV-A' }));
    await loadA;

    expect(store.current()?.id).toBe(invB);
    expect(store.current()?.invoiceNumber).toBe('INV-B');
    expect(store.loading()).toBe(false);
  });

  it('InvoicesStore ignores a late error for a previous id', async () => {
    const loadA = store.loadOne(invA);
    const loadB = store.loadOne(invB);
    const reqA = http.expectOne(`/bff/invoices/${invA}`);
    http.expectOne(`/bff/invoices/${invB}`).flush(invoiceView({ id: invB }));
    await loadB;
    const { body, init } = problem(404, 'Not found', 'Invoice not found');
    reqA.flush(body, init);
    await loadA;

    expect(store.current()?.id).toBe(invB);
    expect(store.error()).toBeNull();
  });
});

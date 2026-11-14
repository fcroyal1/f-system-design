import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { InvoicesStore } from './invoices.store';
import { CreateInvoiceRequest } from '../core/models';
import { problem, supplierId } from '../testing/fixtures';

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
});

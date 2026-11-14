import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SuppliersStore } from './suppliers.store';
import { CreateSupplierRequest, Supplier } from '../core/models';

const existing: Supplier = {
  id: '11111111-0000-4000-8000-000000000001',
  code: 'SUP-001',
  legalName: 'Bluepeak Medical Supply',
  categoryCode: 'MED',
  categoryName: 'Medical supplies',
  paymentTermsDays: 30,
  taxIdMasked: '•••• 1234',
  bankMasked: '•••• 9876',
  isBlocked: false,
};

const request: CreateSupplierRequest = {
  code: 'SUP-900',
  legalName: 'Northwind Lab Goods',
  categoryCode: 'MED',
  paymentTermsDays: 45,
  taxId: '98-7654421',
  bankAccount: 'GB29 NWBK 6016 1331 9268 19',
};

const created: Supplier = {
  id: '22222222-0000-4000-8000-000000000900',
  code: 'SUP-900',
  legalName: 'Northwind Lab Goods',
  categoryCode: 'MED',
  categoryName: 'Medical supplies',
  paymentTermsDays: 45,
  taxIdMasked: '•••• 4421',
  bankMasked: '•••• 6819',
  isBlocked: false,
};

describe('SuppliersStore', () => {
  let store: InstanceType<typeof SuppliersStore>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(SuppliersStore);
    http = TestBed.inject(HttpTestingController);

    const loaded = store.load();
    http.expectOne({ method: 'GET', url: '/api/suppliers' }).flush([existing]);
    await loaded;
  });

  afterEach(() => http.verify());

  it('SuppliersStore create adds supplier and clears error', async () => {
    // A previous failure leaves an error behind; the next success must clear it.
    const failed = store.create(request);
    http
      .expectOne('/api/suppliers')
      .flush(
        { title: 'Bad Request', status: 400, detail: 'legalName is required.' },
        { status: 400, statusText: 'Bad Request' },
      );
    expect(await failed).toBeNull();
    expect(store.error()).toBe('legalName is required.');

    const done = store.create(request);
    const req = http.expectOne({ method: 'POST', url: '/api/suppliers' });
    expect(req.request.body).toEqual(request);
    req.flush(created, { status: 201, statusText: 'Created' });

    expect(await done).toEqual(created);
    expect(store.error()).toBeNull();
    expect(store.items().map((s) => s.code)).toEqual(['SUP-001', 'SUP-900']);
    expect(store.items()[1].taxIdMasked).toBe('•••• 4421');
  });

  it('SuppliersStore create 409 sets error from problem detail', async () => {
    const done = store.create({ ...request, code: 'SUP-001' });
    http.expectOne({ method: 'POST', url: '/api/suppliers' }).flush(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
        title: 'Conflict',
        status: 409,
        detail: 'A supplier with code SUP-001 already exists.',
      },
      { status: 409, statusText: 'Conflict', headers: { 'Content-Type': 'application/problem+json' } },
    );

    expect(await done).toBeNull();
    expect(store.error()).toBe('A supplier with code SUP-001 already exists.');
    expect(store.items().length).toBe(1);
    expect(store.saving()).toBe(false);
  });
});

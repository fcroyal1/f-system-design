import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SupplierDetailStore } from './supplier-detail.store';
import { Supplier, SupplierHistoryEntry, UpdateSupplierRequest } from '../core/models';

const id = '11111111-0000-4000-8000-000000000001';

const supplier: Supplier = {
  id,
  code: 'V-10442',
  legalName: 'Bluepeak Medical Supply',
  categoryCode: 'MED-SUPPLIES',
  categoryName: 'Medical supplies',
  paymentTermsDays: 30,
  taxIdMasked: '•••• 4421',
  bankMasked: '•••• 6630',
  isBlocked: false,
};

const bankChanged: SupplierHistoryEntry = {
  occurredAt: '2026-11-14T10:00:00Z',
  actor: 'Demo User',
  action: 'SupplierBankChanged',
  fromStatus: null,
  toStatus: null,
  detail: 'Bank account •••• 6630 → •••• 9876. Confirm with the supplier before paying.',
};

const update: UpdateSupplierRequest = {
  legalName: 'Bluepeak Medical Supply',
  categoryCode: 'MED-SUPPLIES',
  paymentTermsDays: 30,
  bankAccount: '555500009876',
  isBlocked: false,
};

describe('SupplierDetailStore', () => {
  let store: InstanceType<typeof SupplierDetailStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(SupplierDetailStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the supplier and its history', async () => {
    const done = store.load(id);
    http.expectOne(`/api/suppliers/${id}`).flush(supplier);
    http.expectOne(`/api/suppliers/${id}/history`).flush([bankChanged]);
    await done;

    expect(store.supplier()?.legalName).toBe('Bluepeak Medical Supply');
    expect(store.history()).toEqual([bankChanged]);
    expect(store.pendingBankChange()?.action).toBe('SupplierBankChanged');
    expect(store.loading()).toBe(false);
  });

  it('save PUTs the changes, keeps only the masked result and reloads the history', async () => {
    const loaded = store.load(id);
    http.expectOne(`/api/suppliers/${id}`).flush(supplier);
    http.expectOne(`/api/suppliers/${id}/history`).flush([]);
    await loaded;

    const saved = store.save(id, update);
    const put = http.expectOne(`/api/suppliers/${id}`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual(update);
    put.flush({ ...supplier, bankMasked: '•••• 9876' });
    await new Promise((resolve) => setTimeout(resolve)); // the history is fetched once the PUT has answered
    http.expectOne(`/api/suppliers/${id}/history`).flush([bankChanged]);

    expect(await saved).toBe(true);
    expect(store.supplier()?.bankMasked).toBe('•••• 9876');
    expect(JSON.stringify(store.supplier())).not.toContain('555500009876');
    expect(store.history()).toEqual([bankChanged]);
    expect(store.notice()).toBe('Changes saved.');
  });

  it('save shows the ProblemDetails message and resolves false on a 400', async () => {
    const saved = store.save(id, { ...update, categoryCode: 'NOPE' });
    http.expectOne(`/api/suppliers/${id}`).flush(
      { title: 'Bad Request', status: 400, detail: "categoryCode 'NOPE' is not a known spend category." },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(await saved).toBe(false);
    expect(store.error()).toBe("categoryCode 'NOPE' is not a known spend category.");
    expect(store.saving()).toBe(false);
  });

  it('ignores a late response for a supplier that is no longer shown', async () => {
    const first = store.load(id);
    const other = '22222222-0000-4000-8000-000000000002';
    const second = store.load(other);

    http.expectOne(`/api/suppliers/${other}`).flush({ ...supplier, id: other, legalName: 'Other' });
    http.expectOne(`/api/suppliers/${other}/history`).flush([]);
    http.expectOne(`/api/suppliers/${id}`).flush(supplier);
    http.expectOne(`/api/suppliers/${id}/history`).flush([bankChanged]);
    await Promise.all([first, second]);

    expect(store.supplier()?.legalName).toBe('Other');
    expect(store.history()).toEqual([]);
  });
});

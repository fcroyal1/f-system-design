import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AnalyticsStore } from './analytics.store';
import { Analytics } from '../core/models';

const analytics: Analytics = {
  spendByCategory: [
    { code: 'IT', name: 'IT equipment', amount: 1200, poCount: 2 },
    { code: 'OFFICE', name: 'Office supplies', amount: 300, poCount: 3 },
  ],
  topSuppliers: [],
  invoicesByStatus: [
    { status: 'Blocked', count: 1, amount: 100 },
    { status: 'Matched', count: 3, amount: 900 },
  ],
  blockReasons: [{ reason: 'Quantity', count: 1, amount: 100 }],
  monthly: [],
  approvalsByRole: [],
  payablesAging: [],
  suppliers: { total: 5, blocked: 1, byCategory: [] },
};

describe('AnalyticsStore', () => {
  let store: InstanceType<typeof AnalyticsStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(AnalyticsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads analytics and derives the headline totals', async () => {
    const done = store.load();
    http.expectOne('/api/analytics').flush(analytics);
    await done;

    expect(store.totalSpend()).toBe(1500);
    expect(store.invoiceCount()).toBe(4);
    expect(store.invoiceAmount()).toBe(1000);
    expect(store.blockedShare()).toBe(25);
  });

  it('sets error from the ProblemDetails on failure', async () => {
    const done = store.load();
    http.expectOne('/api/analytics').flush(
      { title: 'The procurement API is unavailable', status: 502 },
      { status: 502, statusText: 'Bad Gateway' },
    );
    await done;

    expect(store.error()).toBe('The procurement API is unavailable');
    expect(store.data()).toBeNull();
  });
});

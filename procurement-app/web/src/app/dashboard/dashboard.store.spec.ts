import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardStore } from './dashboard.store';
import { dashboardFixture } from './dashboard.fixture';

describe('DashboardStore', () => {
  let store: InstanceType<typeof DashboardStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(DashboardStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('DashboardStore loads kpis and exceptions', async () => {
    const done = store.load();
    expect(store.loading()).toBe(true);

    http.expectOne({ method: 'GET', url: '/bff/dashboard' }).flush(dashboardFixture);
    await done;

    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();
    expect(store.kpis()?.firstPassMatchRatePct).toBe(50);
    expect(store.exceptions().length).toBe(1);
  });

  it('DashboardStore_LoadFails_SetsError', async () => {
    const done = store.load();

    http.expectOne('/bff/dashboard').flush(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.6.3',
        title: 'The procurement API is unavailable',
        status: 502,
        detail:
          'The page could not be loaded because the procurement API did not respond. Try again shortly.',
      },
      { status: 502, statusText: 'Bad Gateway', headers: { 'Content-Type': 'application/problem+json' } },
    );
    await done;

    expect(store.loading()).toBe(false);
    expect(store.error()).toBe('The procurement API is unavailable');
  });
});

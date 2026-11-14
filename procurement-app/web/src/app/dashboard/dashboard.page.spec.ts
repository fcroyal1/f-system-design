import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DashboardPage } from './dashboard.page';
import { dashboardFixture } from './dashboard.fixture';

describe('DashboardPage', () => {
  it('Dashboard page shows the open exception INV-77812 with its reason', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);

    // The page starts loading on creation; the store holds the app unstable until the response is applied.
    const fixture = TestBed.createComponent(DashboardPage);

    http.expectOne('/bff/dashboard').flush(dashboardFixture);
    await fixture.whenStable();

    const page = fixture.nativeElement as HTMLElement;
    const row = Array.from(page.querySelectorAll('[data-testid="exception"]')).find((el) =>
      el.textContent?.includes('INV-77812'),
    );
    expect(row).toBeTruthy();
    expect(row?.textContent).toContain('Bluepeak Medical Supply');
    expect(row?.textContent).toContain('Quantity: billed 2 more than received (line 1)');
    expect(page.textContent).toContain('50');

    http.verify();
  });
});

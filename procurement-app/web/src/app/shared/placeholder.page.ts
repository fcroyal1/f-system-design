import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

/** Stand-in for screens that are not built yet (purchase orders and invoices), so the nav never dead-ends. */
@Component({
  selector: 'app-placeholder-page',
  template: `
    <h1>{{ heading }}</h1>
    <p class="muted">This screen is not available yet.</p>
  `,
})
export class PlaceholderPage {
  protected readonly heading: string = inject(ActivatedRoute).snapshot.title ?? 'Coming soon';
}

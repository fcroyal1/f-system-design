import { Component, input, output } from '@angular/core';

/** Shows a store's `error` sentence. Renders nothing when the message is null. */
@Component({
  selector: 'app-error-banner',
  template: `
    @if (message(); as text) {
      <div class="error-banner" role="alert">
        <span>{{ text }}</span>
        @if (dismissible()) {
          <button type="button" class="link-button" (click)="dismissed.emit()" aria-label="Dismiss">Dismiss</button>
        }
      </div>
    }
  `,
  styles: `
    .error-banner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      margin: 0 0 1rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--danger-line, #f2c4bf);
      border-radius: var(--radius, 8px);
      background: var(--danger-soft, #fdeeed);
      color: var(--danger, #b42318);
      font-weight: 500;
    }
    .link-button {
      background: none;
      border: none;
      color: inherit;
      text-decoration: underline;
      cursor: pointer;
      padding: 0;
      font: inherit;
    }
  `,
})
export class ErrorBanner {
  readonly message = input<string | null>(null);
  readonly dismissible = input(false);
  readonly dismissed = output<void>();
}

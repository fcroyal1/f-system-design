import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const KEY = 'procurement.theme';

/**
 * Light / dark theme. Starts from the viewer's saved choice, else their system setting, and sets
 * data-theme on <html> (the CSS tokens switch on it). Storage can be unavailable (private mode), so
 * every access is guarded and the theme still works for the session.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  readonly theme = signal<Theme>(this.initial());

  constructor() {
    this.apply(this.theme());
  }

  toggle(): void {
    const next: Theme = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    this.apply(next);
    try {
      localStorage.setItem(KEY, next);
    } catch {
      // Not persisted; fine for this session.
    }
  }

  private initial(): Theme {
    try {
      const saved = localStorage.getItem(KEY);
      if (saved === 'light' || saved === 'dark') {
        return saved;
      }
    } catch {
      // Fall through to the system preference.
    }
    return this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private apply(theme: Theme): void {
    this.document.documentElement.setAttribute('data-theme', theme);
  }
}

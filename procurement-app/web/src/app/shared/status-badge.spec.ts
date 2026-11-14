import { TestBed } from '@angular/core/testing';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it('StatusBadge renders Blocked in the danger style', async () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', 'Blocked');
    await fixture.whenStable();

    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');
    expect(badge?.textContent?.trim()).toBe('Blocked');
    expect(badge?.classList.contains('badge-danger')).toBe(true);

    fixture.componentRef.setInput('status', 'Matched');
    await fixture.whenStable();
    expect(badge?.classList.contains('badge-danger')).toBe(false);
    expect(badge?.classList.contains('badge-success')).toBe(true);
  });
});

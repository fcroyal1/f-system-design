import { money, monthLabel } from './format';

describe('format', () => {
  it('money shows small amounts in full and large ones compact', () => {
    expect(money(4210)).toBe('$4,210');
    expect(money(123_902)).toBe('$123.9K');
    expect(money(0)).toBe('$0');
  });

  it('monthLabel names the month without time-zone drift', () => {
    expect(monthLabel('2026-09')).toBe('Sep 2026');
    expect(monthLabel('2026-01', true)).toBe('Jan');
  });
});

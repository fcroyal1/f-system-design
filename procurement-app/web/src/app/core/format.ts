const compact = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  notation: 'compact',
  maximumFractionDigits: 1,
});

const whole = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });

/** "$123.9K" for headline and chart labels; amounts under $10,000 are shown in full ("$4,210"). */
export function money(amount: number): string {
  return Math.abs(amount) < 10_000 ? whole.format(amount) : compact.format(amount);
}

/** "2026-09" → "Sep 2026" (or "Sep" when short). */
export function monthLabel(month: string, short = false): string {
  const [year, m] = month.split('-').map(Number);
  const name = new Date(Date.UTC(year, m - 1, 1)).toLocaleString('en-US', { month: 'short', timeZone: 'UTC' });
  return short ? name : `${name} ${year}`;
}

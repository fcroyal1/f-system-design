import { IsoDate } from './models';

/** Today's local date as "yyyy-MM-dd", the default for date inputs. */
export function today(): IsoDate {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

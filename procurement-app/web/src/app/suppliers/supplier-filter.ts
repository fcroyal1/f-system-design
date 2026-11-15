import { Supplier } from '../core/models';

export type SupplierStatusFilter = 'all' | 'active' | 'blocked';

export interface SupplierFilter {
  /** Matched against the legal name and the code, case-insensitive. */
  query: string;
  /** A spend category code, or '' for every category. */
  category: string;
  status: SupplierStatusFilter;
}

/** The suppliers that pass every part of the filter, in their original order. */
export function filterSuppliers(items: readonly Supplier[], filter: SupplierFilter): Supplier[] {
  const query = filter.query.trim().toLowerCase();
  return items.filter(
    (s) =>
      (!query || s.legalName.toLowerCase().includes(query) || s.code.toLowerCase().includes(query)) &&
      (!filter.category || s.categoryCode === filter.category) &&
      (filter.status === 'all' || (filter.status === 'blocked') === s.isBlocked),
  );
}

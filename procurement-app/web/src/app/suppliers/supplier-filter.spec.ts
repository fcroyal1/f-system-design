import { Supplier } from '../core/models';
import { filterSuppliers } from './supplier-filter';

function supplier(code: string, legalName: string, categoryCode: string, isBlocked = false): Supplier {
  return {
    id: code, code, legalName, categoryCode, categoryName: categoryCode, paymentTermsDays: 30,
    taxIdMasked: null, bankMasked: null, isBlocked,
  };
}

const all = [
  supplier('V-10442', 'Bluepeak Medical Supply', 'MED-SUPPLIES'),
  supplier('V-10518', 'Northline Office Co', 'OFFICE'),
  supplier('V-11010', 'Tidewater Stationers', 'OFFICE', true),
];

describe('filterSuppliers', () => {
  it('returns everything for an empty filter', () => {
    expect(filterSuppliers(all, { query: '', category: '', status: 'all' })).toEqual(all);
  });

  it('matches the name or code, ignoring case and surrounding spaces', () => {
    expect(filterSuppliers(all, { query: '  blue ', category: '', status: 'all' }).map((s) => s.code)).toEqual(['V-10442']);
    expect(filterSuppliers(all, { query: 'v-105', category: '', status: 'all' }).map((s) => s.code)).toEqual(['V-10518']);
  });

  it('filters by category and status together', () => {
    expect(filterSuppliers(all, { query: '', category: 'OFFICE', status: 'blocked' }).map((s) => s.code)).toEqual(['V-11010']);
    expect(filterSuppliers(all, { query: '', category: 'OFFICE', status: 'active' }).map((s) => s.code)).toEqual(['V-10518']);
  });
});

import { DashboardView } from '../core/models';

/** The /bff/dashboard payload from the BFF contract, shared by the dashboard specs. */
export const dashboardFixture: DashboardView = {
  kpis: {
    poComplianceBySpendPct: 100.0,
    poComplianceByCountPct: 100.0,
    firstPassMatchRatePct: 50.0,
    retroPoRatePct: 0.0,
    openExceptions: 1,
    invoiceCount: 2,
  },
  exceptions: [
    {
      id: '6f1c2a8e-0000-4000-8000-000000000077',
      invoiceNumber: 'INV-77812',
      supplierName: 'Bluepeak Medical Supply',
      total: 1020.0,
      blockReason: 'Quantity: billed 2 more than received (line 1)',
    },
  ],
};

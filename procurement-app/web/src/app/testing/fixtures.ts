import {
  Invoice,
  InvoiceView,
  MatchCheck,
  PurchaseOrder,
  PurchaseOrderView,
  Receipt,
  Supplier,
} from '../core/models';

/**
 * Payloads shared by the purchase order and invoice specs. They mirror the guide example: PO 4500123 for 100
 * boxes of nitrile gloves at 10.00, 98 received, and invoice INV-77812 billing 100 at 10.20, which blocks on
 * quantity. Kept out of spec files so importing them does not register tests twice.
 */

export const supplierId = 'a1b2c3d4-0001-4000-8000-000000010442';
export const poId = 'a1b2c3d4-0002-4000-8000-000004500123';
export const invoiceId = 'a1b2c3d4-0004-4000-8000-000000077812';

export const bluepeak: Supplier = {
  id: supplierId,
  code: 'V-10442',
  legalName: 'Bluepeak Medical Supply',
  categoryCode: 'MED-SUPPLIES',
  categoryName: 'Medical supplies',
  paymentTermsDays: 30,
  taxIdMasked: '•••• 1234',
  bankMasked: '•••• 9876',
  isBlocked: false,
};

/** The guide example's match rows, exactly as MatchEngine produces them. */
export const guideChecks: MatchCheck[] = [
  { seq: 1, name: 'Supplier', lineNo: null, poValue: '—', receiptValue: '—', invoiceValue: '—', outcome: 'Pass', message: 'supplier matches the PO' },
  { seq: 2, name: 'Currency', lineNo: null, poValue: 'USD', receiptValue: '—', invoiceValue: 'USD', outcome: 'Pass', message: 'invoice and PO both in USD' },
  { seq: 3, name: 'PO status', lineNo: null, poValue: 'Approved', receiptValue: '—', invoiceValue: '—', outcome: 'Pass', message: 'PO is approved' },
  { seq: 4, name: 'PO line', lineNo: 1, poValue: 'GLV-NIT-M/BX', receiptValue: '—', invoiceValue: 'GLV-NIT-M/BX', outcome: 'Pass', message: 'invoice line 1 matches PO line 1' },
  { seq: 5, name: 'Quantity', lineNo: 1, poValue: '100', receiptValue: '98', invoiceValue: '100', outcome: 'Fail', message: 'billed 2 more than received (line 1)' },
  { seq: 6, name: 'Unit price', lineNo: 1, poValue: '10.00', receiptValue: '—', invoiceValue: '10.20', outcome: 'Pass', message: '+2.0%, inside the ±3% / $50 price tolerance' },
  { seq: 7, name: 'Amount', lineNo: 1, poValue: '1000.00', receiptValue: '980.00', invoiceValue: '1020.00', outcome: 'Fail', message: '$40.00 more than the value received' },
];

export function purchaseOrder(overrides: Partial<PurchaseOrder> = {}): PurchaseOrder {
  return {
    id: poId,
    number: '4500123',
    supplierId,
    supplierCode: 'V-10442',
    supplierName: 'Bluepeak Medical Supply',
    requesterName: 'Dana Ruiz',
    costCenterCode: 'CC-200',
    currency: 'USD',
    poDate: '2026-10-01',
    status: 'Approved',
    approverRole: 'Cost center owner',
    approvedBy: 'Demo User',
    approvedAt: '2026-10-02T09:00:00',
    total: 1000,
    lines: [
      {
        lineNo: 1,
        itemCode: 'GLV-NIT-M',
        description: 'Nitrile gloves',
        uom: 'BX',
        quantity: 100,
        unitPrice: 10,
        glAccount: '6110',
        receiptRequired: true,
        amount: 1000,
      },
    ],
    receipts: [
      {
        id: 'a1b2c3d4-0003-4000-8000-000005000987',
        grNumber: '5000987',
        receivedAt: '2026-10-06T10:00:00',
        receivedBy: 'Sam Okafor',
        packingSlip: 'PS-4410',
        quantityReceived: 98,
      },
    ],
    invoices: [
      {
        id: invoiceId,
        invoiceNumber: 'INV-77812',
        invoiceDate: '2026-10-08',
        dueDate: '2026-11-07',
        currency: 'USD',
        status: 'Blocked',
        blockReason: 'Quantity: billed 2 more than received (line 1)',
        isRetroPo: false,
        total: 1020,
      },
    ],
    ...overrides,
  };
}

export function purchaseOrderView(overrides: Partial<PurchaseOrder> = {}): PurchaseOrderView {
  return { po: purchaseOrder(overrides), supplier: bluepeak };
}

export function invoice(overrides: Partial<Invoice> = {}): Invoice {
  return {
    id: invoiceId,
    invoiceNumber: 'INV-77812',
    normalizedNumber: '77812',
    supplierId,
    supplierCode: 'V-10442',
    supplierName: 'Bluepeak Medical Supply',
    poId,
    poNumber: '4500123',
    invoiceDate: '2026-10-08',
    dueDate: '2026-11-07',
    currency: 'USD',
    status: 'Blocked',
    blockReason: 'Quantity: billed 2 more than received (line 1)',
    firstMatchPassed: false,
    isRetroPo: false,
    createdAt: '2026-10-08T14:00:00',
    subtotal: 1020,
    taxAmount: 0,
    total: 1020,
    lines: [
      {
        lineNo: 1,
        poLineNo: 1,
        itemCode: 'GLV-NIT-M',
        uom: 'BX',
        description: 'Nitrile gloves',
        quantity: 100,
        unitPrice: 10.2,
        amount: 1020,
      },
    ],
    checks: guideChecks,
    ...overrides,
  };
}

export function invoiceView(overrides: Partial<Invoice> = {}): InvoiceView {
  return { invoice: invoice(overrides), po: purchaseOrder(), supplier: bluepeak };
}

/** The receipt that brings line 1 to 100 received, re-matching INV-77812 from Blocked to Matched. */
export const receiptFor2More: Receipt = {
  id: 'a1b2c3d4-0003-4000-8000-000005000988',
  grNumber: '5000988',
  poId,
  poNumber: '4500123',
  receivedAt: '2026-10-09T11:00:00',
  receivedBy: 'Sam Okafor',
  packingSlip: 'PS-4471',
  lines: [{ poLineNo: 1, quantityReceived: 2 }],
  rematchedInvoices: [
    { id: invoiceId, invoiceNumber: 'INV-77812', fromStatus: 'Blocked', status: 'Matched', blockReason: null },
  ],
};

export function problem(status: number, title: string, detail: string) {
  return {
    body: { type: 'about:blank', title, status, detail },
    init: { status, statusText: title, headers: { 'Content-Type': 'application/problem+json' } },
  };
}

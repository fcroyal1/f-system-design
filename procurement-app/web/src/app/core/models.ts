/**
 * TypeScript mirrors of the JSON the BFF returns. Field names are the camelCase forms of the C# DTOs in
 * Procurement.Api/Features/*Contracts.cs, Procurement.Api/Data/ReferenceStore.cs, Procurement.Domain KpiSnapshot,
 * and the BFF views in Procurement.Bff/Views.
 *
 * C# DateOnly arrives as "yyyy-MM-dd", DateTime as an ISO-8601 string, decimal as a JSON number, Guid as a string.
 */

export type IsoDate = string;
export type IsoDateTime = string;

export type InvoiceStatus = 'Matched' | 'Blocked';
export type PurchaseOrderStatus = 'Draft' | 'Approved' | 'Closed';

/** RFC 9457 ProblemDetails, as returned by the API and the BFF on every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [extension: string]: unknown;
}

// ---- Suppliers ---------------------------------------------------------------

export interface Supplier {
  id: string;
  code: string;
  legalName: string;
  categoryCode: string;
  categoryName: string;
  paymentTermsDays: number;
  /** e.g. "•••• 4421"; the full tax ID is never returned. */
  taxIdMasked: string | null;
  /** e.g. "•••• 6819"; the full bank account is never returned. */
  bankMasked: string | null;
  isBlocked: boolean;
}

export interface CreateSupplierRequest {
  code: string;
  legalName: string;
  categoryCode: string;
  paymentTermsDays: number | null;
  taxId?: string | null;
  bankAccount?: string | null;
}

/** PUT /api/suppliers/{id}. The code never changes. Tax ID / bank account: omit or null to keep the stored value. */
export interface UpdateSupplierRequest {
  legalName: string;
  categoryCode: string;
  paymentTermsDays: number;
  taxId?: string | null;
  bankAccount?: string | null;
  isBlocked: boolean;
}

/** One audit-trail entry about a supplier (GET /api/suppliers/{id}/history), newest first. */
export interface SupplierHistoryEntry {
  occurredAt: string;
  actor: string;
  /** SupplierUpdated, SupplierBlocked, SupplierUnblocked or SupplierBankChanged. */
  action: string;
  fromStatus: string | null;
  toStatus: string | null;
  detail: string | null;
}

// ---- Analytics (GET /api/analytics) ----------------------------------------------

export interface CategorySpend { code: string; name: string; amount: number; poCount: number; }
export interface SupplierSpend { supplierId: string; code: string; name: string; amount: number; poCount: number; }
export interface StatusTotal { status: string; count: number; amount: number; }
export interface BlockReasonTotal { reason: string; count: number; amount: number; }
export interface MonthTotal { month: string; poCount: number; poAmount: number; invoiceCount: number; invoiceAmount: number; }
export interface ApprovalTotal { role: string; poCount: number; amount: number; }
export interface AgingBucket { bucket: string; count: number; amount: number; }
export interface SupplierCategoryCount { code: string; name: string; isPoExempt: boolean; count: number; blocked: number; }

export interface Analytics {
  spendByCategory: CategorySpend[];
  topSuppliers: SupplierSpend[];
  invoicesByStatus: StatusTotal[];
  blockReasons: BlockReasonTotal[];
  monthly: MonthTotal[];
  approvalsByRole: ApprovalTotal[];
  payablesAging: AgingBucket[];
  suppliers: { total: number; blocked: number; byCategory: SupplierCategoryCount[] };
}

// ---- Reference data ----------------------------------------------------------

export interface SpendCategory {
  code: string;
  name: string;
  isPoExempt: boolean;
}

export interface GlAccount {
  number: string;
  name: string;
  type: string;
}

export interface CostCenter {
  code: string;
  name: string;
  ownerName: string;
}

export interface DoaTier {
  maxAmount: number;
  approverRole: string;
}

export interface ReferenceData {
  categories: SpendCategory[];
  glAccounts: GlAccount[];
  costCenters: CostCenter[];
  doaTiers: DoaTier[];
}

// ---- Purchase orders and receipts ---------------------------------------------

export interface PurchaseOrderSummary {
  id: string;
  number: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  requesterName: string;
  costCenterCode: string;
  currency: string;
  poDate: IsoDate;
  status: PurchaseOrderStatus;
  approverRole: string | null;
  total: number;
}

export interface PoLine {
  lineNo: number;
  itemCode: string;
  description: string;
  uom: string;
  quantity: number;
  unitPrice: number;
  glAccount: string;
  receiptRequired: boolean;
  amount: number;
}

export interface ReceiptSummary {
  id: string;
  grNumber: string;
  receivedAt: IsoDateTime;
  receivedBy: string;
  packingSlip: string | null;
  quantityReceived: number;
}

export interface InvoiceSummary {
  id: string;
  invoiceNumber: string;
  invoiceDate: IsoDate;
  dueDate: IsoDate;
  currency: string;
  status: InvoiceStatus;
  blockReason: string | null;
  isRetroPo: boolean;
  total: number;
}

export interface PurchaseOrder {
  id: string;
  number: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  requesterName: string;
  costCenterCode: string;
  currency: string;
  poDate: IsoDate;
  status: PurchaseOrderStatus;
  approverRole: string | null;
  approvedBy: string | null;
  approvedAt: IsoDateTime | null;
  total: number;
  lines: PoLine[];
  receipts: ReceiptSummary[];
  invoices: InvoiceSummary[];
}

export interface CreatePoLineRequest {
  itemCode: string;
  description: string;
  uom: string;
  quantity: number | null;
  unitPrice: number | null;
  glAccount: string;
  receiptRequired: boolean;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  requesterName: string;
  costCenterCode: string;
  currency: string;
  poDate: IsoDate;
  lines: CreatePoLineRequest[];
}

export interface CreateReceiptLineRequest {
  poLineNo: number;
  quantity: number | null;
}

export interface CreateReceiptRequest {
  receivedBy: string;
  packingSlip?: string | null;
  lines: CreateReceiptLineRequest[];
}

export interface ReceiptLine {
  poLineNo: number;
  quantityReceived: number;
}

/** A Blocked invoice on the PO that the receipt re-matched, with its new status. */
export interface RematchedInvoice {
  id: string;
  invoiceNumber: string;
  fromStatus: InvoiceStatus | null;
  status: InvoiceStatus;
  blockReason: string | null;
}

export interface Receipt {
  id: string;
  grNumber: string;
  poId: string;
  poNumber: string;
  receivedAt: IsoDateTime;
  receivedBy: string;
  packingSlip: string | null;
  lines: ReceiptLine[];
  rematchedInvoices: RematchedInvoice[];
}

// ---- Invoices ----------------------------------------------------------------

export interface InvoiceListItem {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  poId: string | null;
  poNumber: string | null;
  invoiceDate: IsoDate;
  dueDate: IsoDate;
  currency: string;
  status: InvoiceStatus;
  blockReason: string | null;
  isRetroPo: boolean;
  total: number;
}

export interface InvoiceLine {
  lineNo: number;
  poLineNo: number | null;
  itemCode: string;
  uom: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface MatchCheck {
  seq: number;
  name: string;
  lineNo: number | null;
  poValue: string;
  receiptValue: string;
  invoiceValue: string;
  outcome: 'Pass' | 'Fail';
  message: string;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  normalizedNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  poId: string | null;
  poNumber: string | null;
  invoiceDate: IsoDate;
  dueDate: IsoDate;
  currency: string;
  status: InvoiceStatus;
  blockReason: string | null;
  firstMatchPassed: boolean | null;
  isRetroPo: boolean;
  createdAt: IsoDateTime;
  subtotal: number;
  taxAmount: number;
  total: number;
  lines: InvoiceLine[];
  checks: MatchCheck[];
}

export interface CreateInvoiceLineRequest {
  poLineNo: number | null;
  itemCode: string;
  uom: string;
  description: string;
  quantity: number | null;
  unitPrice: number | null;
}

export interface CreateInvoiceRequest {
  supplierId: string;
  invoiceNumber: string;
  invoiceDate: IsoDate;
  currency: string;
  poNumber?: string | null;
  taxAmount: number | null;
  lines: CreateInvoiceLineRequest[];
}

// ---- KPIs and BFF screen views -------------------------------------------------

export interface KpiSnapshot {
  poComplianceBySpendPct: number;
  poComplianceByCountPct: number;
  firstPassMatchRatePct: number;
  retroPoRatePct: number;
  openExceptions: number;
  invoiceCount: number;
}

/** One Blocked invoice on the dashboard. `total` is in the invoice's currency (not carried here). */
export interface ExceptionItem {
  id: string;
  invoiceNumber: string;
  supplierName: string;
  total: number;
  blockReason: string | null;
}

/** GET /bff/dashboard */
export interface DashboardView {
  kpis: KpiSnapshot;
  exceptions: ExceptionItem[];
}

/** GET /bff/purchase-orders/{id} */
export interface PurchaseOrderView {
  po: PurchaseOrder;
  supplier: Supplier;
}

/** GET /bff/invoices/{id}. `po` is null when the invoice was saved without a matching PO. */
export interface InvoiceView {
  invoice: Invoice;
  po: PurchaseOrder | null;
  supplier: Supplier;
}

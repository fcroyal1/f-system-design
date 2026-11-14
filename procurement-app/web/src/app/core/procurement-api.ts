import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateInvoiceRequest,
  CreatePurchaseOrderRequest,
  CreateReceiptRequest,
  CreateSupplierRequest,
  DashboardView,
  Invoice,
  InvoiceListItem,
  InvoiceStatus,
  InvoiceView,
  ProblemDetails,
  PurchaseOrder,
  PurchaseOrderSummary,
  PurchaseOrderView,
  Receipt,
  ReferenceData,
  Supplier,
} from './models';

/**
 * The only HTTP client in the app. It talks to the BFF: `/api/*` is passed through to the API unchanged,
 * `/bff/*` returns screen payloads that combine several API calls.
 */
@Injectable({ providedIn: 'root' })
export class ProcurementApi {
  private readonly http = inject(HttpClient);

  dashboard(): Observable<DashboardView> {
    return this.http.get<DashboardView>('/bff/dashboard');
  }

  suppliers(): Observable<Supplier[]> {
    return this.http.get<Supplier[]>('/api/suppliers');
  }

  createSupplier(body: CreateSupplierRequest): Observable<Supplier> {
    return this.http.post<Supplier>('/api/suppliers', body);
  }

  reference(): Observable<ReferenceData> {
    return this.http.get<ReferenceData>('/api/reference');
  }

  purchaseOrders(): Observable<PurchaseOrderSummary[]> {
    return this.http.get<PurchaseOrderSummary[]>('/api/purchase-orders');
  }

  purchaseOrder(id: string): Observable<PurchaseOrderView> {
    return this.http.get<PurchaseOrderView>(`/bff/purchase-orders/${encodeURIComponent(id)}`);
  }

  createPurchaseOrder(body: CreatePurchaseOrderRequest): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>('/api/purchase-orders', body);
  }

  approve(id: string): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${encodeURIComponent(id)}/approve`, null);
  }

  receive(id: string, body: CreateReceiptRequest): Observable<Receipt> {
    return this.http.post<Receipt>(`/api/purchase-orders/${encodeURIComponent(id)}/receipts`, body);
  }

  invoices(status?: InvoiceStatus): Observable<InvoiceListItem[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<InvoiceListItem[]>('/api/invoices', { params });
  }

  invoice(id: string): Observable<InvoiceView> {
    return this.http.get<InvoiceView>(`/bff/invoices/${encodeURIComponent(id)}`);
  }

  createInvoice(body: CreateInvoiceRequest): Observable<Invoice> {
    return this.http.post<Invoice>('/api/invoices', body);
  }

  rematch(id: string): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/invoices/${encodeURIComponent(id)}/rematch`, null);
  }
}

/** Title of the BFF's 502 ProblemDetails when the API does not answer. */
export const API_UNAVAILABLE = 'The procurement API is unavailable';

/**
 * Turns a failed request into the sentence a store shows in its `error` signal.
 *
 * - 502 (the BFF could not reach the API): the ProblemDetails `title`, "The procurement API is unavailable".
 * - Any other ProblemDetails: its `detail`, or its `title` when there is no detail.
 * - Status 0 (the BFF itself could not be reached) or a non-problem body: a generic sentence.
 */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return error instanceof Error && error.message ? error.message : 'Something went wrong.';
  }

  const problem = asProblem(error.error);
  if (error.status === 502) {
    return problem?.title || API_UNAVAILABLE;
  }
  if (problem?.detail) {
    return problem.detail;
  }
  if (problem?.title) {
    return problem.title;
  }
  if (error.status === 0) {
    return 'The server could not be reached. Check your connection and try again.';
  }
  return `The request failed (HTTP ${error.status}).`;
}

function asProblem(body: unknown): ProblemDetails | null {
  if (body && typeof body === 'object') {
    return body as ProblemDetails;
  }
  if (typeof body === 'string' && body.trim().startsWith('{')) {
    try {
      return JSON.parse(body) as ProblemDetails;
    } catch {
      return null;
    }
  }
  return null;
}

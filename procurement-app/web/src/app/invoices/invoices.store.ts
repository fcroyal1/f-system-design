import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import {
  CreateInvoiceRequest,
  Invoice,
  InvoiceListItem,
  InvoiceStatus,
  PurchaseOrder,
  Supplier,
} from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface InvoicesState {
  items: InvoiceListItem[];
  /** The status filter of the last list load; null lists every invoice. */
  filter: InvoiceStatus | null;
  current: Invoice | null;
  po: PurchaseOrder | null;
  supplier: Supplier | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
}

const initialState: InvoicesState = {
  items: [],
  filter: null,
  current: null,
  po: null,
  supplier: null,
  loading: false,
  saving: false,
  error: null,
};

/** Invoices: the list, the invoice open on the detail page with its match checks, create and re-match. */
export const InvoicesStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ items, current }) => ({
    count: computed(() => items().length),
    isBlocked: computed(() => current()?.status === 'Blocked'),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => {
    async function tracked<T>(work: () => Promise<T>): Promise<T> {
      // Keeps the app unstable until the state is patched, so whenStable() (tests, SSR) sees the result.
      const taskDone = pendingTasks.add();
      try {
        return await work();
      } finally {
        taskDone();
      }
    }

    return {
      /** GET /api/invoices[?status=…]. Never rejects. */
      async load(filter: InvoiceStatus | null = null): Promise<void> {
        patchState(store, { loading: true, filter });
        await tracked(async () => {
          try {
            const items = await firstValueFrom(api.invoices(filter ?? undefined));
            patchState(store, { items, loading: false, error: null });
          } catch (e) {
            patchState(store, { loading: false, error: problemMessage(e) });
          }
        });
      },

      /** GET /bff/invoices/{id}: the invoice, its PO (or null) and its supplier. Never rejects. */
      async loadOne(id: string): Promise<void> {
        if (store.current()?.id !== id) {
          patchState(store, { current: null, po: null, supplier: null });
        }
        patchState(store, { loading: true });
        await tracked(async () => {
          try {
            const view = await firstValueFrom(api.invoice(id));
            patchState(store, {
              current: view.invoice,
              po: view.po,
              supplier: view.supplier,
              loading: false,
              error: null,
            });
          } catch (e) {
            patchState(store, { loading: false, error: problemMessage(e) });
          }
        });
      },

      /**
       * POST /api/invoices. Resolves to the saved invoice (Matched or Blocked), or to null with `error` set from
       * the ProblemDetails: 400 validation, 409 duplicate invoice for the supplier.
       */
      async create(body: CreateInvoiceRequest): Promise<Invoice | null> {
        patchState(store, { saving: true });
        return tracked(async () => {
          try {
            const invoice = await firstValueFrom(api.createInvoice(body));
            patchState(store, { saving: false, error: null });
            return invoice;
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return null;
          }
        });
      },

      /** POST /api/invoices/{id}/rematch. The open invoice takes the new status, block reason and checks. */
      async rematch(id: string): Promise<Invoice | null> {
        patchState(store, { saving: true });
        return tracked(async () => {
          try {
            const invoice = await firstValueFrom(api.rematch(id));
            patchState(store, {
              current: store.current()?.id === invoice.id ? invoice : store.current(),
              items: store.items().map((i) =>
                i.id === invoice.id ? { ...i, status: invoice.status, blockReason: invoice.blockReason } : i,
              ),
              saving: false,
              error: null,
            });
            return invoice;
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return null;
          }
        });
      },

      clearError(): void {
        patchState(store, { error: null });
      },
    };
  }),
);

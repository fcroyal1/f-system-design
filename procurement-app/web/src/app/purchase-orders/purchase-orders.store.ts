import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import {
  CreatePurchaseOrderRequest,
  CreateReceiptRequest,
  InvoiceStatus,
  PurchaseOrder,
  PurchaseOrderSummary,
  Receipt,
  Supplier,
} from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface PurchaseOrdersState {
  items: PurchaseOrderSummary[];
  current: PurchaseOrder | null;
  supplier: Supplier | null;
  /** Sentences about the last receipt's effect on invoices, e.g. "Invoice INV-77812 is now Matched". */
  notices: string[];
  loading: boolean;
  saving: boolean;
  error: string | null;
}

const initialState: PurchaseOrdersState = {
  items: [],
  current: null,
  supplier: null,
  notices: [],
  loading: false,
  saving: false,
  error: null,
};

/**
 * One sentence per invoice the receipt moved to Matched. The API reports `fromStatus`; when it is missing, the
 * status the PO listed before the receipt is used instead.
 */
export function matchedNotices(receipt: Receipt, before: PurchaseOrder | null): string[] {
  const previous = new Map<string, InvoiceStatus>((before?.invoices ?? []).map((i) => [i.id, i.status]));
  return receipt.rematchedInvoices
    .filter((r) => r.status === 'Matched' && (r.fromStatus ?? previous.get(r.id) ?? null) !== 'Matched')
    .map((r) => `Invoice ${r.invoiceNumber} is now Matched`);
}

/** Purchase orders: the list, the PO open on the detail page, approval and goods receipts. */
export const PurchaseOrdersStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ items, current }) => ({
    count: computed(() => items().length),
    canApprove: computed(() => current()?.status === 'Draft'),
    canReceive: computed(() => current()?.status === 'Approved'),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => {
    /** Runs a request with the app held unstable until the state is patched, so whenStable() sees the result. */
    async function tracked<T>(work: () => Promise<T>): Promise<T> {
      const taskDone = pendingTasks.add();
      try {
        return await work();
      } finally {
        taskDone();
      }
    }

    /** The id of the most recent loadOne; a response for any other id arrived late and is ignored. */
    let latestId: string | null = null;

    /** GET /bff/purchase-orders/{id}. Never rejects: a failure sets `error`. */
    async function loadOne(id: string): Promise<void> {
      latestId = id;
      if (store.current()?.id !== id) {
        // A different PO: drop the previous one so its lines and notices are never shown under this id.
        patchState(store, { current: null, supplier: null, notices: [] });
      }
      patchState(store, { loading: true });
      await tracked(async () => {
        try {
          const view = await firstValueFrom(api.purchaseOrder(id));
          if (id !== latestId) return;
          patchState(store, { current: view.po, supplier: view.supplier, loading: false, error: null });
        } catch (e) {
          if (id !== latestId) return;
          patchState(store, { loading: false, error: problemMessage(e) });
        }
      });
    }

    return {
      /** GET /api/purchase-orders. Never rejects. */
      async load(): Promise<void> {
        patchState(store, { loading: true });
        await tracked(async () => {
          try {
            const items = await firstValueFrom(api.purchaseOrders());
            patchState(store, { items, loading: false, error: null });
          } catch (e) {
            patchState(store, { loading: false, error: problemMessage(e) });
          }
        });
      },

      loadOne,

      /** POST /api/purchase-orders. Resolves to the new Draft PO, or null with `error` set (400). */
      async create(body: CreatePurchaseOrderRequest): Promise<PurchaseOrder | null> {
        patchState(store, { saving: true });
        return tracked(async () => {
          try {
            const po = await firstValueFrom(api.createPurchaseOrder(body));
            patchState(store, { saving: false, error: null });
            return po;
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return null;
          }
        });
      },

      /**
       * POST /api/purchase-orders/{id}/approve. On success the open PO takes the returned status, approver role
       * and approval stamp; on 422 (not Draft) `error` holds the API's sentence and null is returned.
       */
      async approve(id: string): Promise<PurchaseOrder | null> {
        patchState(store, { saving: true });
        return tracked(async () => {
          try {
            const po = await firstValueFrom(api.approve(id));
            patchState(store, {
              current: store.current()?.id === po.id ? { ...store.current()!, ...po } : store.current(),
              items: store.items().map((i) =>
                i.id === po.id ? { ...i, status: po.status, approverRole: po.approverRole } : i,
              ),
              saving: false,
              error: null,
            });
            return po;
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return null;
          }
        });
      },

      /**
       * POST /api/purchase-orders/{id}/receipts. On success it sets `notices` for every invoice the receipt moved
       * to Matched and reloads the PO; on 400/422 (over-receipt) `error` holds the API's sentence and null is
       * returned. Never rejects.
       */
      async receive(id: string, body: CreateReceiptRequest): Promise<Receipt | null> {
        patchState(store, { saving: true, notices: [] });
        return tracked(async () => {
          const before = store.current();
          let receipt: Receipt;
          try {
            receipt = await firstValueFrom(api.receive(id, body));
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return null;
          }
          patchState(store, { saving: false, error: null, notices: matchedNotices(receipt, before) });
          await loadOne(id);
          return receipt;
        });
      },

      clearError(): void {
        patchState(store, { error: null });
      },
    };
  }),
);

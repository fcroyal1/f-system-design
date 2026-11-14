import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { CreateSupplierRequest, Supplier } from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface SuppliersState {
  items: Supplier[];
  loading: boolean;
  saving: boolean;
  error: string | null;
}

const initialState: SuppliersState = {
  items: [],
  loading: false,
  saving: false,
  error: null,
};

/**
 * Supplier master data. Items hold only what the API returns, so tax ID and bank account are the masked
 * `taxIdMasked` / `bankMasked` values; the full numbers sent on create are never kept here.
 */
export const SuppliersStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ items }) => ({
    count: computed(() => items().length),
    blockedCount: computed(() => items().filter((s) => s.isBlocked).length),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => ({
    /** Loads GET /api/suppliers. Never rejects: a failure sets `error`. */
    async load(): Promise<void> {
      patchState(store, { loading: true });
      // Keeps the app unstable until the state is patched, so whenStable() (tests, SSR) sees the result.
      const taskDone = pendingTasks.add();
      try {
        const items = await firstValueFrom(api.suppliers());
        patchState(store, { items, loading: false, error: null });
      } catch (e) {
        patchState(store, { loading: false, error: problemMessage(e) });
      } finally {
        taskDone();
      }
    },

    /**
     * POST /api/suppliers. Resolves to the created supplier (masked values only) and appends it to `items`,
     * or resolves to null and sets `error` from the ProblemDetails (400 validation, 409 duplicate code).
     */
    async create(body: CreateSupplierRequest): Promise<Supplier | null> {
      patchState(store, { saving: true });
      const taskDone = pendingTasks.add();
      try {
        const supplier = await firstValueFrom(api.createSupplier(body));
        patchState(store, { items: [...store.items(), supplier], saving: false, error: null });
        return supplier;
      } catch (e) {
        patchState(store, { saving: false, error: problemMessage(e) });
        return null;
      } finally {
        taskDone();
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  })),
);

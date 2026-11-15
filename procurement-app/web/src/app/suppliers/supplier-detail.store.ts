import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { Supplier, SupplierHistoryEntry, UpdateSupplierRequest } from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface SupplierDetailState {
  supplier: Supplier | null;
  history: SupplierHistoryEntry[];
  loading: boolean;
  saving: boolean;
  error: string | null;
  notice: string | null;
}

const initialState: SupplierDetailState = {
  supplier: null,
  history: [],
  loading: false,
  saving: false,
  error: null,
  notice: null,
};

/**
 * One supplier and its audit history, for the edit screen. Only masked tax ID / bank values are ever held:
 * the full numbers typed into the form are sent once in the PUT and never stored here.
 */
export const SupplierDetailStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ history }) => ({
    /** The newest bank-account change, shown as a warning until someone has checked it with the supplier. */
    pendingBankChange: computed(() => history().find((h) => h.action === 'SupplierBankChanged') ?? null),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => {
    async function tracked<T>(work: () => Promise<T>): Promise<T> {
      const taskDone = pendingTasks.add();
      try {
        return await work();
      } finally {
        taskDone();
      }
    }

    /** The id of the most recent load; a response for any other id arrived late and is ignored. */
    let latestId: string | null = null;

    return {
      /** GET the supplier and its history together. Never rejects: a failure sets `error`. */
      async load(id: string): Promise<void> {
        latestId = id;
        if (store.supplier()?.id !== id) {
          patchState(store, { supplier: null, history: [], notice: null });
        }
        patchState(store, { loading: true, error: null });
        await tracked(async () => {
          try {
            const [supplier, history] = await Promise.all([
              firstValueFrom(api.supplier(id)),
              firstValueFrom(api.supplierHistory(id)),
            ]);
            if (id !== latestId) return;
            patchState(store, { supplier, history, loading: false });
          } catch (e) {
            if (id !== latestId) return;
            patchState(store, { loading: false, error: problemMessage(e) });
          }
        });
      },

      /** PUT the changes, then reload the history. Resolves true on success, false (with `error` set) otherwise. */
      async save(id: string, body: UpdateSupplierRequest): Promise<boolean> {
        patchState(store, { saving: true, error: null, notice: null });
        return tracked(async () => {
          try {
            const supplier = await firstValueFrom(api.updateSupplier(id, body));
            const history = await firstValueFrom(api.supplierHistory(id));
            if (id === latestId || latestId === null) {
              patchState(store, { supplier, history });
            }
            patchState(store, { saving: false, notice: 'Changes saved.' });
            return true;
          } catch (e) {
            patchState(store, { saving: false, error: problemMessage(e) });
            return false;
          }
        });
      },

      clearMessages(): void {
        patchState(store, { error: null, notice: null });
      },
    };
  }),
);

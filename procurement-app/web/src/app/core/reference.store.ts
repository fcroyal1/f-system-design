import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ReferenceData } from './models';
import { ProcurementApi, problemMessage } from './procurement-api';

interface ReferenceState {
  reference: ReferenceData | null;
  loading: boolean;
  error: string | null;
}

const initialState: ReferenceState = {
  reference: null,
  loading: false,
  error: null,
};

/** Spend categories, cost centers, GL accounts and DoA tiers from GET /api/reference, loaded once per session. */
export const ReferenceStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ reference }) => ({
    categories: computed(() => reference()?.categories ?? []),
    costCenters: computed(() => reference()?.costCenters ?? []),
    glAccounts: computed(() => reference()?.glAccounts ?? []),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => ({
    /** Loads the reference data. A no-op once loaded (or while a load is in flight). Never rejects. */
    async load(): Promise<void> {
      if (store.reference() !== null || store.loading()) {
        return;
      }
      patchState(store, { loading: true });
      const taskDone = pendingTasks.add();
      try {
        const reference = await firstValueFrom(api.reference());
        patchState(store, { reference, loading: false, error: null });
      } catch (e) {
        patchState(store, { loading: false, error: problemMessage(e) });
      } finally {
        taskDone();
      }
    },
  })),
);

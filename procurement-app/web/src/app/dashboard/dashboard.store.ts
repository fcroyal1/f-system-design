import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ExceptionItem, KpiSnapshot } from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface DashboardState {
  kpis: KpiSnapshot | null;
  exceptions: ExceptionItem[];
  loading: boolean;
  error: string | null;
}

const initialState: DashboardState = {
  kpis: null,
  exceptions: [],
  loading: false,
  error: null,
};

/** KPIs and the newest open exceptions, from GET /bff/dashboard. */
export const DashboardStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ exceptions }) => ({
    hasExceptions: computed(() => exceptions().length > 0),
  })),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => ({
    /** Loads the dashboard. Never rejects: a failure sets `error` and keeps the previous data. */
    async load(): Promise<void> {
      patchState(store, { loading: true });
      // Keeps the app unstable until the state is patched, so whenStable() (tests, SSR) sees the result.
      const taskDone = pendingTasks.add();
      try {
        const view = await firstValueFrom(api.dashboard());
        patchState(store, { kpis: view.kpis, exceptions: view.exceptions, loading: false, error: null });
      } catch (e) {
        patchState(store, { loading: false, error: problemMessage(e) });
      } finally {
        taskDone();
      }
    },
  })),
);

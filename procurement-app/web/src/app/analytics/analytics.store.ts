import { PendingTasks, computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { Analytics } from '../core/models';
import { ProcurementApi, problemMessage } from '../core/procurement-api';

interface AnalyticsState {
  data: Analytics | null;
  loading: boolean;
  error: string | null;
}

const initialState: AnalyticsState = { data: null, loading: false, error: null };

/** GET /api/analytics plus the few headline numbers the page shows above the charts. */
export const AnalyticsStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ data }) => {
    const statuses = computed(() => data()?.invoicesByStatus ?? []);
    const invoiceCount = computed(() => statuses().reduce((sum, s) => sum + s.count, 0));
    return {
      totalSpend: computed(() => (data()?.spendByCategory ?? []).reduce((sum, c) => sum + c.amount, 0)),
      invoiceCount,
      invoiceAmount: computed(() => statuses().reduce((sum, s) => sum + s.amount, 0)),
      /** Percentage of invoices currently Blocked, 0–100. */
      blockedShare: computed(() => {
        const blocked = statuses().find((s) => s.status === 'Blocked')?.count ?? 0;
        return invoiceCount() === 0 ? 0 : (blocked / invoiceCount()) * 100;
      }),
    };
  }),
  withMethods((store, api = inject(ProcurementApi), pendingTasks = inject(PendingTasks)) => ({
    /** Never rejects: a failure sets `error`. */
    async load(): Promise<void> {
      patchState(store, { loading: true });
      const taskDone = pendingTasks.add();
      try {
        const data = await firstValueFrom(api.analytics());
        patchState(store, { data, loading: false, error: null });
      } catch (e) {
        patchState(store, { loading: false, error: problemMessage(e) });
      } finally {
        taskDone();
      }
    },
  })),
);

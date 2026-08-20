import type { Routes } from '@angular/router';

/** Routes available in the drilling telemetry dashboard. */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import(
      './features/dashboard/dashboard-page/dashboard-page'
    ).then(module => module.DashboardPage),
    title: 'Drilling Telemetry'
  }
];

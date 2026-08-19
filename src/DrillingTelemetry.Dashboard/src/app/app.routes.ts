import { Routes } from '@angular/router';

import { DashboardPage } from './features/dashboard/dashboard-page/dashboard-page';

/** Routes available in the drilling telemetry dashboard. */
export const routes: Routes = [
  {
    path: '',
    component: DashboardPage,
    title: 'Drilling Telemetry'
  }
];

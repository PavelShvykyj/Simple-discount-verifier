import { Routes } from '@angular/router';

import { adminGuard } from '../shared/auth/admin.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('../pages/public-redemption/ui/public-redemption.page').then(
        (m) => m.PublicRedemptionPage,
      ),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./shells/admin-shell/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'customers',
      },
      {
        path: 'customers',
        loadChildren: () =>
          import('../pages/admin-customers/admin-customers.routes').then(
            (m) => m.ADMIN_CUSTOMERS_ROUTES,
          ),
      },
      {
        path: 'service',
        loadChildren: () =>
          import('../pages/admin-service/admin-service.routes').then(
            (m) => m.ADMIN_SERVICE_ROUTES,
          ),
      },
    ],
  },
  {
    path: 'scanner-survey',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('../pages/scanner-survey/ui/legacy-scanner-survey-redirect.page').then(
        (m) => m.LegacyScannerSurveyRedirectPage,
      ),
  },
];

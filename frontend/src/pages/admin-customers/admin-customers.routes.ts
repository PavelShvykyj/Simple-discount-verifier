import { Routes } from '@angular/router';

export const ADMIN_CUSTOMERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./ui/admin-customers.page').then((m) => m.AdminCustomersPage),
  },
];

import { Routes } from '@angular/router';

export const ADMIN_SERVICE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./ui/admin-service.page').then((m) => m.AdminServicePage),
  },
  {
    path: 'scanner-survey',
    loadComponent: () =>
      import('../scanner-survey/ui/scanner-survey.page').then((m) => m.ScannerSurveyPage),
  },
];

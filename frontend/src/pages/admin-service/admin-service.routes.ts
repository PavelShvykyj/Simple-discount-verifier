import { Routes } from '@angular/router';

export const ADMIN_SERVICE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./ui/admin-service.page').then((m) => m.AdminServicePage),
  },
  {
    path: 'inspect',
    loadComponent: () =>
      import('./ui/admin-service-inspect.page').then((m) => m.AdminServiceInspectPage),
  },
  {
    path: 'health',
    loadComponent: () =>
      import('./ui/admin-service-health.page').then((m) => m.AdminServiceHealthPage),
  },
  {
    path: 'scanner-survey',
    loadComponent: () =>
      import('../scanner-survey/ui/scanner-survey.page').then((m) => m.ScannerSurveyPage),
  },
];

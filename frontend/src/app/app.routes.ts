import { Routes } from '@angular/router';

import { adminGuard } from '../shared/auth/admin.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('../pages/home/ui/home.page').then((m) => m.HomePage),
  },
  {
    path: 'scanner-survey',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('../pages/scanner-survey/ui/scanner-survey.page').then((m) => m.ScannerSurveyPage),
  },
];

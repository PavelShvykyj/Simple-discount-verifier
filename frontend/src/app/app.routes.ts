import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('../pages/home/ui/home.page').then((m) => m.HomePage),
  },
  {
    path: 'scanner-survey',
    loadComponent: () =>
      import('../pages/scanner-survey/ui/scanner-survey.page').then((m) => m.ScannerSurveyPage),
  },
];

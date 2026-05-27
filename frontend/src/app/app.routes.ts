import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('../pages/home/ui/home.page').then((m) => m.HomePage),
  },
];

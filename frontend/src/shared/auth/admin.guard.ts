import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';

export const adminGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);

  return authService.getCurrentUser().pipe(
    map((user) => {
      const isAdmin = user?.userRoles?.includes('admin') === true;

      if (isAdmin) {
        return true;
      }

      const returnUrl = `${window.location.origin}${state.url}`;
      authService.login(returnUrl);

      return false;
    }),
    catchError(() => {
      const returnUrl = `${window.location.origin}${state.url}`;
      authService.login(returnUrl);

      return of(false);
    }),
  );
};

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';

export interface StaticWebAppsClientPrincipal {
  identityProvider: string;
  userId: string;
  userDetails: string;
  userRoles: string[];
}

export interface StaticWebAppsMeResponse {
  clientPrincipal: StaticWebAppsClientPrincipal | null;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);

  private currentUser$?: Observable<StaticWebAppsClientPrincipal | null>;

  getCurrentUser(forceRefresh = false): Observable<StaticWebAppsClientPrincipal | null> {
    if (!this.currentUser$ || forceRefresh) {
      this.currentUser$ = this.http.get<StaticWebAppsMeResponse>('/.auth/me').pipe(
        map((response) => response.clientPrincipal),
        catchError((error: unknown) => {
          console.error('Failed to load current user', error);
          return of(null);
        }),
        shareReplay(1),
      );
    }

    return this.currentUser$;
  }

  isAdmin(): Observable<boolean> {
    return this.getCurrentUser().pipe(
      map((user) => user?.userRoles?.includes('admin') === true),
    );
  }

  login(returnUrl: string): void {
    const loginUrl = `/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(returnUrl)}`;

    window.location.replace(loginUrl);
  }

  logout(returnUrl = '/'): void {
    const logoutUrl = `/.auth/logout?post_logout_redirect_uri=${encodeURIComponent(returnUrl)}`;

    window.location.replace(logoutUrl);
  }

  clearCache(): void {
    this.currentUser$ = undefined;
  }
}

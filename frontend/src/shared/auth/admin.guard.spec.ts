import { TestBed } from '@angular/core/testing';
import { RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, isObservable, of, throwError } from 'rxjs';

import { adminGuard } from './admin.guard';
import { AuthService, StaticWebAppsClientPrincipal } from './auth.service';

describe('adminGuard', () => {
  const adminUser: StaticWebAppsClientPrincipal = {
    identityProvider: 'aad',
    userId: 'admin-user',
    userDetails: 'admin@example.com',
    userRoles: ['anonymous', 'authenticated', 'admin'],
  };

  const regularUser: StaticWebAppsClientPrincipal = {
    identityProvider: 'aad',
    userId: 'regular-user',
    userDetails: 'user@example.com',
    userRoles: ['anonymous', 'authenticated'],
  };

  function configureAuthService(userResult: ReturnType<AuthService['getCurrentUser']>) {
    const authService = {
      getCurrentUser: vi.fn(() => userResult),
      login: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: authService }],
    });

    return authService;
  }

  async function runGuard(url = '/admin/customers'): Promise<unknown> {
    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as never, { url } as RouterStateSnapshot),
    );

    return isObservable(result) ? await firstValueFrom(result) : result;
  }

  it('allows users with the admin role', async () => {
    const authService = configureAuthService(of(adminUser));

    await expect(runGuard()).resolves.toBe(true);
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('redirects non-admin users to login with the current URL', async () => {
    const authService = configureAuthService(of(regularUser));

    await expect(runGuard('/admin/profiles')).resolves.toBe(false);
    expect(authService.login).toHaveBeenCalledWith(`${window.location.origin}/admin/profiles`);
  });

  it('redirects to login when current user loading fails', async () => {
    const authService = configureAuthService(throwError(() => new Error('SWA auth unavailable')));

    await expect(runGuard('/admin')).resolves.toBe(false);
    expect(authService.login).toHaveBeenCalledWith(`${window.location.origin}/admin`);
  });
});

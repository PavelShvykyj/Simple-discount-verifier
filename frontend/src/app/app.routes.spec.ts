import { routes } from './app.routes';
import { adminGuard } from '../shared/auth/admin.guard';

describe('routes', () => {
  it('loads the expected route components', () => {
    expect(routes).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          path: '',
          loadComponent: expect.any(Function),
        }),
        expect.objectContaining({
          path: 'admin',
          canActivate: [adminGuard],
          loadComponent: expect.any(Function),
          children: expect.arrayContaining([
            expect.objectContaining({
              path: 'customers',
              loadChildren: expect.any(Function),
            }),
            expect.objectContaining({
              path: 'service',
              loadChildren: expect.any(Function),
            }),
          ]),
        }),
        expect.objectContaining({
          path: 'scanner-survey',
          canActivate: [adminGuard],
          loadComponent: expect.any(Function),
        }),
      ]),
    );
  });
});

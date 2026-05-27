import { routes } from './app.routes';

describe('routes', () => {
  it('loads the home page for the default route', () => {
    expect(routes).toEqual([
      expect.objectContaining({
        path: '',
        loadComponent: expect.any(Function),
      }),
    ]);
  });
});

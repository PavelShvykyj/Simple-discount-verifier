import { routes } from './app.routes';

describe('routes', () => {
  it('loads the expected route components', () => {
    expect(routes).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          path: '',
          loadComponent: expect.any(Function),
        }),
        expect.objectContaining({
          path: 'scanner-survey',
          loadComponent: expect.any(Function),
        }),
      ]),
    );
  });
});

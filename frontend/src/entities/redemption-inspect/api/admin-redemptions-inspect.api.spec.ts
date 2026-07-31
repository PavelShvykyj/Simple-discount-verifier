import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { AdminRedemptionsInspectApi } from './admin-redemptions-inspect.api';

describe('AdminRedemptionsInspectApi', () => {
  let http: {
    post: ReturnType<typeof vi.fn>;
  };
  let api: AdminRedemptionsInspectApi;

  beforeEach(() => {
    http = {
      post: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AdminRedemptionsInspectApi,
        {
          provide: HttpClient,
          useValue: http,
        },
      ],
    });

    api = TestBed.inject(AdminRedemptionsInspectApi);
  });

  it('posts inspect requests to the approved backoffice endpoint', () => {
    const request = { correlationId: 'QPS7O7KCNM' };

    api.inspect(request);

    expect(http.post).toHaveBeenCalledWith(
      '/api/backoffice/redemptions/inspect',
      request,
    );
  });
});

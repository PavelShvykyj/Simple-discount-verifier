import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { SystemHealthApi } from './system-health.api';

describe('SystemHealthApi', () => {
  let http: {
    get: ReturnType<typeof vi.fn>;
  };
  let api: SystemHealthApi;

  beforeEach(() => {
    http = {
      get: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        SystemHealthApi,
        {
          provide: HttpClient,
          useValue: http,
        },
      ],
    });

    api = TestBed.inject(SystemHealthApi);
  });

  it('reads health from the system health endpoint', () => {
    api.getHealth();

    expect(http.get).toHaveBeenCalledWith('/api/system/health');
  });
});

import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

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
    http.get.mockReturnValue(of({ status: 'ok', service: 'api' }));

    api.getHealth();

    expect(http.get).toHaveBeenCalledWith('/api/system/health');
  });

  it('normalizes PascalCase health responses from .NET', () => {
    const values: unknown[] = [];
    http.get.mockReturnValue(of({ Status: 'ok', Service: 'simple-discount-verifier-api' }));

    api.getHealth().subscribe((response) => values.push(response));

    expect(values).toEqual([
      {
        status: 'ok',
        service: 'simple-discount-verifier-api',
      },
    ]);
  });
});

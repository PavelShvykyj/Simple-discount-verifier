import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { SystemHealthResponse } from '../model/system-health.types';

interface SystemHealthApiResponse {
  readonly status?: string;
  readonly Status?: string;
  readonly service?: string;
  readonly Service?: string;
}

@Injectable({ providedIn: 'root' })
export class SystemHealthApi {
  private readonly http = inject(HttpClient);

  getHealth(): Observable<SystemHealthResponse> {
    return this.http
      .get<SystemHealthApiResponse>('/api/system/health')
      .pipe(map(toSystemHealthResponse));
  }
}

function toSystemHealthResponse(response: SystemHealthApiResponse): SystemHealthResponse {
  return {
    status: response.status ?? response.Status ?? '',
    service: response.service ?? response.Service ?? '',
  };
}

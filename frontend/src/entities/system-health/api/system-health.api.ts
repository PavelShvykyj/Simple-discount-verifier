import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { SystemHealthResponse } from '../model/system-health.types';

@Injectable({ providedIn: 'root' })
export class SystemHealthApi {
  private readonly http = inject(HttpClient);

  getHealth(): Observable<SystemHealthResponse> {
    return this.http.get<SystemHealthResponse>('/api/system/health');
  }
}

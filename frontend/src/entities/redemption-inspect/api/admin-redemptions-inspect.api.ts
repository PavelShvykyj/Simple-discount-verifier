import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  RedemptionInspectRequest,
  RedemptionInspectResponse,
} from '../model/redemption-inspect.types';

@Injectable({ providedIn: 'root' })
export class AdminRedemptionsInspectApi {
  private readonly http = inject(HttpClient);

  inspect(request: RedemptionInspectRequest): Observable<RedemptionInspectResponse> {
    return this.http.post<RedemptionInspectResponse>(
      '/api/backoffice/redemptions/inspect',
      request,
    );
  }
}

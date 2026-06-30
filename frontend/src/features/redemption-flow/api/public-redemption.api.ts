import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  StartRedemptionRequest,
  StartRedemptionResponse,
  VerifySmsRequest,
  VerifySmsResponse,
} from '../model/redemption-flow.types';

@Injectable()
export class PublicRedemptionApi {
  private readonly http = inject(HttpClient);

  startRedemption(request: StartRedemptionRequest): Observable<StartRedemptionResponse> {
    return this.http.post<StartRedemptionResponse>('/api/public/redemptions', request);
  }

  verifySms(redemptionKey: string, request: VerifySmsRequest): Observable<VerifySmsResponse> {
    return this.http.post<VerifySmsResponse>(
      `/api/public/redemptions/${encodeURIComponent(redemptionKey)}/sms-verifications`,
      request,
    );
  }
}

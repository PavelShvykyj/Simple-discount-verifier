import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import {
  StartRedemptionRequest,
  StartRedemptionResponse,
  VerifySmsRequest,
  VerifySmsResponse,
} from '../model/redemption-flow.types';

interface StartRedemptionApiResponse {
  readonly redemptionKey?: string;
  readonly RedemptionKey?: string;
  readonly correlationId?: string;
  readonly CorrelationId?: string;
  readonly smsSent?: boolean;
  readonly SmsSent?: boolean;
  readonly retryAfterSeconds?: number;
  readonly RetryAfterSeconds?: number;
  readonly smsExpiresAt?: string;
  readonly SmsExpiresAt?: string;
}

interface VerifySmsApiResponse {
  readonly correlationId?: string;
  readonly CorrelationId?: string;
  readonly barcodeValue?: string;
  readonly BarcodeValue?: string;
  readonly barcodeFormat?: 'code128';
  readonly BarcodeFormat?: 'code128';
  readonly expiresAt?: string;
  readonly ExpiresAt?: string;
  readonly ttlSeconds?: number;
  readonly TtlSeconds?: number;
}

@Injectable()
export class PublicRedemptionApi {
  private readonly http = inject(HttpClient);

  startRedemption(request: StartRedemptionRequest): Observable<StartRedemptionResponse> {
    return this.http
      .post<StartRedemptionApiResponse>('/api/public/redemptions', request)
      .pipe(map(toStartRedemptionResponse));
  }

  verifySms(redemptionKey: string, request: VerifySmsRequest): Observable<VerifySmsResponse> {
    return this.http
      .post<VerifySmsApiResponse>(
        `/api/public/redemptions/${encodeURIComponent(redemptionKey)}/sms-verifications`,
        request,
      )
      .pipe(map(toVerifySmsResponse));
  }
}

function toStartRedemptionResponse(response: StartRedemptionApiResponse): StartRedemptionResponse {
  return {
    redemptionKey: response.redemptionKey ?? response.RedemptionKey ?? '',
    correlationId: response.correlationId ?? response.CorrelationId ?? '',
    smsSent: response.smsSent ?? response.SmsSent ?? false,
    retryAfterSeconds: response.retryAfterSeconds ?? response.RetryAfterSeconds ?? 0,
    smsExpiresAt: response.smsExpiresAt ?? response.SmsExpiresAt ?? '',
  };
}

function toVerifySmsResponse(response: VerifySmsApiResponse): VerifySmsResponse {
  return {
    correlationId: response.correlationId ?? response.CorrelationId ?? '',
    barcodeValue: response.barcodeValue ?? response.BarcodeValue ?? '',
    barcodeFormat: response.barcodeFormat ?? response.BarcodeFormat ?? 'code128',
    expiresAt: response.expiresAt ?? response.ExpiresAt ?? '',
    ttlSeconds: response.ttlSeconds ?? response.TtlSeconds ?? 0,
  };
}

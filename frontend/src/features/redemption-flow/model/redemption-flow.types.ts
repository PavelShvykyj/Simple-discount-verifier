import type { ApiErrorResponse } from '../../../shared/lib/api-error/api-error';

export type RedemptionStep = 'phone' | 'sms' | 'barcode';

export type RedemptionRequestStatus = 'idle' | 'submitting' | 'success' | 'error';

export type RedemptionTransitionResult = 'advanced' | 'blocked';

export const SMS_CODE_LENGTH = 6;

export interface StartRedemptionRequest {
  readonly phone: string;
  readonly turnstileToken?: string | null;
}

export interface StartRedemptionResponse {
  readonly redemptionKey: string;
  readonly correlationId: string;
  readonly smsSent: boolean;
  readonly retryAfterSeconds: number;
  readonly smsExpiresAt: string;
}

export interface VerifySmsRequest {
  readonly code: string;
}

export interface VerifySmsResponse {
  readonly correlationId: string;
  readonly barcodeValue: string;
  readonly barcodeFormat: 'code128';
  readonly expiresAt: string;
  readonly ttlSeconds: number;
}

export type { ApiErrorResponse };

export interface RedemptionFlowError {
  readonly code: string;
  readonly message: string;
  readonly correlationId: string | null;
  readonly isRestartRequired: boolean;
}

export interface BarcodeResult {
  readonly correlationId: string;
  readonly barcodeValue: string;
  readonly barcodeFormat: 'code128';
  readonly expiresAt: string;
  readonly ttlSeconds: number;
}

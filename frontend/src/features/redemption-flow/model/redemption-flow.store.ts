import { Injectable, InjectionToken, Signal, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';

import { PublicRedemptionApi } from '../api/public-redemption.api';
import {
  ApiErrorResponse,
  BarcodeResult,
  RedemptionFlowError,
  RedemptionRequestStatus,
  RedemptionStep,
  RedemptionTransitionResult,
  SMS_CODE_LENGTH,
  StartRedemptionResponse,
  VerifySmsResponse,
} from './redemption-flow.types';
import {
  EMPTY_PHONE_MESSAGE,
  INVALID_UKRAINIAN_PHONE_MESSAGE,
  normalizeUkrainianPhone,
} from '../../../shared/lib/phone/ukrainian-phone';

export interface PublicRedemptionFlowStore {
  readonly currentStep: Signal<RedemptionStep>;
  readonly phone: Signal<string>;
  readonly smsCode: Signal<string>;
  readonly correlationId: Signal<string | null>;
  readonly redemptionKey: Signal<string | null>;
  readonly retryAfterSeconds: Signal<number | null>;
  readonly smsExpiresAt: Signal<string | null>;
  readonly barcodeResult: Signal<BarcodeResult | null>;
  readonly phoneStatus: Signal<RedemptionRequestStatus>;
  readonly smsStatus: Signal<RedemptionRequestStatus>;
  readonly error: Signal<RedemptionFlowError | null>;
  readonly phoneValidationError: Signal<string | null>;
  readonly canSubmitPhone: Signal<boolean>;
  readonly canSubmitSms: Signal<boolean>;
  readonly canAccessSmsStep: Signal<boolean>;
  readonly canAccessBarcodeStep: Signal<boolean>;
  readonly isMockCorrelationIdActive: Signal<boolean>;
  readonly isMockBarcodeActive: Signal<boolean>;
  setPhone(phone: string): void;
  setSmsCode(code: string): void;
  startRedemption(): Observable<RedemptionTransitionResult>;
  resendSms(): Observable<RedemptionTransitionResult>;
  verifySms(): Observable<RedemptionTransitionResult>;
  toggleMockCorrelationId(): void;
  toggleMockBarcode(): void;
  restart(): void;
}

export const PUBLIC_REDEMPTION_FLOW_STORE = new InjectionToken<PublicRedemptionFlowStore>(
  'PUBLIC_REDEMPTION_FLOW_STORE',
);

const FALLBACK_ERROR_MESSAGE = 'Не вдалося виконати запит. Спробуйте ще раз.';
const MOCK_CORRELATION_ID = 'QPS7O7KCNM';
const MOCK_PHONE_RUNTIME_KEY = 'EOBMCDRDRT';
const MOCK_BARCODE_VALUE = `${MOCK_PHONE_RUNTIME_KEY}${MOCK_CORRELATION_ID}`;
const MOCK_BARCODE_TTL_SECONDS = 300;

@Injectable()
export class PublicRedemptionSignalStore implements PublicRedemptionFlowStore {
  private readonly api = inject(PublicRedemptionApi);

  private readonly currentStepState = signal<RedemptionStep>('phone');
  private readonly phoneState = signal('');
  private readonly smsCodeState = signal('');
  private readonly correlationIdState = signal<string | null>(null);
  private readonly redemptionKeyState = signal<string | null>(null);
  private readonly retryAfterSecondsState = signal<number | null>(null);
  private readonly smsExpiresAtState = signal<string | null>(null);
  private readonly barcodeResultState = signal<BarcodeResult | null>(null);
  private readonly phoneStatusState = signal<RedemptionRequestStatus>('idle');
  private readonly smsStatusState = signal<RedemptionRequestStatus>('idle');
  private readonly errorState = signal<RedemptionFlowError | null>(null);
  private readonly phoneTouchedState = signal(false);

  readonly currentStep = this.currentStepState.asReadonly();
  readonly phone = this.phoneState.asReadonly();
  readonly smsCode = this.smsCodeState.asReadonly();
  readonly correlationId = this.correlationIdState.asReadonly();
  readonly redemptionKey = this.redemptionKeyState.asReadonly();
  readonly retryAfterSeconds = this.retryAfterSecondsState.asReadonly();
  readonly smsExpiresAt = this.smsExpiresAtState.asReadonly();
  readonly barcodeResult = this.barcodeResultState.asReadonly();
  readonly phoneStatus = this.phoneStatusState.asReadonly();
  readonly smsStatus = this.smsStatusState.asReadonly();
  readonly error = this.errorState.asReadonly();

  private readonly normalizedPhone = computed(() => normalizeUkrainianPhone(this.phone()));

  readonly phoneValidationError = computed(() => {
    const phone = this.phone().trim();

    if (!this.phoneTouchedState() && phone.length === 0) {
      return null;
    }

    if (phone.length === 0) {
      return EMPTY_PHONE_MESSAGE;
    }

    return this.normalizedPhone() === null ? INVALID_UKRAINIAN_PHONE_MESSAGE : null;
  });

  readonly canSubmitPhone = computed(() => {
    return this.phoneStatus() !== 'submitting' && this.normalizedPhone() !== null;
  });

  readonly canAccessSmsStep = computed(() => {
    return this.redemptionKey() !== null && this.correlationId() !== null;
  });

  readonly canSubmitSms = computed(() => {
    return (
      this.smsStatus() !== 'submitting' &&
      this.canAccessSmsStep() &&
      this.smsCode().length === SMS_CODE_LENGTH
    );
  });

  readonly canAccessBarcodeStep = computed(() => this.barcodeResult() !== null);
  readonly isMockCorrelationIdActive = computed(
    () => this.correlationId() === MOCK_CORRELATION_ID,
  );
  readonly isMockBarcodeActive = computed(
    () => this.barcodeResult()?.barcodeValue === MOCK_BARCODE_VALUE,
  );

  setPhone(phone: string): void {
    this.phoneTouchedState.set(true);
    this.phoneState.set(phone);
    this.clearError();
  }

  setSmsCode(code: string): void {
    this.smsCodeState.set(code.replace(/\D/g, '').slice(0, SMS_CODE_LENGTH));
    this.clearError();
  }

  startRedemption(): Observable<RedemptionTransitionResult> {
    const normalizedPhone = this.normalizedPhone();

    if (!this.canSubmitPhone() || normalizedPhone === null) {
      this.phoneTouchedState.set(true);
      return of('blocked');
    }

    this.phoneStatusState.set('submitting');
    this.clearAttemptStateBeforePhoneSubmit();

    return this.api.startRedemption({ phone: normalizedPhone }).pipe(
      tap((response) => this.handleStartRedemptionSuccess(response)),
      map(() => 'advanced' as const),
      catchError((error: unknown) => {
        this.handlePhoneError(error);
        return of('blocked' as const);
      }),
    );
  }

  resendSms(): Observable<RedemptionTransitionResult> {
    const normalizedPhone = this.normalizedPhone();

    if (
      !this.canAccessSmsStep() ||
      this.phoneStatus() === 'submitting' ||
      normalizedPhone === null
    ) {
      return of('blocked');
    }

    this.phoneStatusState.set('submitting');
    this.clearError();

    return this.api.startRedemption({ phone: normalizedPhone }).pipe(
      tap((response) => {
        this.clearAttemptStateAfterSmsResendSuccess();
        this.handleStartRedemptionSuccess(response);
      }),
      map(() => 'advanced' as const),
      catchError((error: unknown) => {
        this.handleSmsResendError(error);
        return of('blocked' as const);
      }),
    );
  }

  verifySms(): Observable<RedemptionTransitionResult> {
    const redemptionKey = this.redemptionKey();

    if (!this.canSubmitSms() || redemptionKey === null) {
      return of('blocked');
    }

    this.smsStatusState.set('submitting');
    this.clearError();

    return this.api.verifySms(redemptionKey, { code: this.smsCode() }).pipe(
      tap((response) => this.handleVerifySmsSuccess(response)),
      map(() => 'advanced' as const),
      catchError((error: unknown) => {
        this.handleSmsError(error);
        return of('blocked' as const);
      }),
    );
  }

  toggleMockCorrelationId(): void {
    if (this.isMockCorrelationIdActive()) {
      this.correlationIdState.set(null);
      return;
    }

    this.correlationIdState.set(MOCK_CORRELATION_ID);
    this.clearError();
  }

  toggleMockBarcode(): void {
    if (this.isMockBarcodeActive()) {
      this.barcodeResultState.set(null);
      return;
    }

    const expiresAt = new Date(Date.now() + MOCK_BARCODE_TTL_SECONDS * 1000).toISOString();

    this.correlationIdState.set(MOCK_CORRELATION_ID);
    this.barcodeResultState.set({
      correlationId: MOCK_CORRELATION_ID,
      barcodeValue: MOCK_BARCODE_VALUE,
      barcodeFormat: 'code128',
      expiresAt,
      ttlSeconds: MOCK_BARCODE_TTL_SECONDS,
    });
    this.clearError();
  }

  restart(): void {
    this.currentStepState.set('phone');
    this.phoneState.set('');
    this.phoneTouchedState.set(false);
    this.smsCodeState.set('');
    this.correlationIdState.set(null);
    this.redemptionKeyState.set(null);
    this.retryAfterSecondsState.set(null);
    this.smsExpiresAtState.set(null);
    this.barcodeResultState.set(null);
    this.phoneStatusState.set('idle');
    this.smsStatusState.set('idle');
    this.clearError();
  }

  private handleStartRedemptionSuccess(response: StartRedemptionResponse): void {
    this.correlationIdState.set(response.correlationId);
    this.redemptionKeyState.set(response.redemptionKey);
    this.retryAfterSecondsState.set(response.retryAfterSeconds);
    this.smsExpiresAtState.set(response.smsExpiresAt);
    this.phoneStatusState.set('success');
    this.currentStepState.set('sms');
  }

  private handleVerifySmsSuccess(response: VerifySmsResponse): void {
    this.correlationIdState.set(response.correlationId);
    this.barcodeResultState.set({
      correlationId: response.correlationId,
      barcodeValue: response.barcodeValue,
      barcodeFormat: response.barcodeFormat,
      expiresAt: response.expiresAt,
      ttlSeconds: response.ttlSeconds,
    });
    this.smsStatusState.set('success');
    this.currentStepState.set('barcode');
  }

  private handlePhoneError(error: unknown): void {
    const flowError = this.toFlowError(error, false);

    this.applyCorrelationIdFromError(flowError);
    this.phoneStatusState.set('error');
    this.currentStepState.set('phone');
    this.errorState.set(flowError);
  }

  private handleSmsResendError(error: unknown): void {
    const flowError = this.toFlowError(error, false);

    this.applyCorrelationIdFromError(flowError);
    this.phoneStatusState.set('error');
    this.currentStepState.set('sms');
    this.errorState.set(flowError);
  }

  private handleSmsError(error: unknown): void {
    const flowError = this.toFlowError(error, this.isRestartRequiredSmsError(error));

    this.applyCorrelationIdFromError(flowError);
    this.smsStatusState.set('error');
    this.currentStepState.set('sms');
    this.errorState.set(flowError);
  }

  private toFlowError(error: unknown, isRestartRequired: boolean): RedemptionFlowError {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as ApiErrorResponse | null;
      const apiError = body?.error;

      return {
        code: apiError?.code ?? `http_${error.status}`,
        message: apiError?.message ?? FALLBACK_ERROR_MESSAGE,
        correlationId: apiError?.correlationId ?? null,
        isRestartRequired,
      };
    }

    return {
      code: 'unknown_error',
      message: FALLBACK_ERROR_MESSAGE,
      correlationId: null,
      isRestartRequired,
    };
  }

  private isRestartRequiredSmsError(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse)) {
      return true;
    }

    const body = error.error as ApiErrorResponse | null;
    const code = body?.error?.code;

    return code !== 'invalid_sms_code';
  }

  private applyCorrelationIdFromError(error: RedemptionFlowError): void {
    if (error.correlationId !== null) {
      this.correlationIdState.set(error.correlationId);
    }
  }

  private clearAttemptStateBeforePhoneSubmit(): void {
    this.smsCodeState.set('');
    this.redemptionKeyState.set(null);
    this.retryAfterSecondsState.set(null);
    this.smsExpiresAtState.set(null);
    this.barcodeResultState.set(null);
    this.smsStatusState.set('idle');
    this.clearError();
  }

  private clearAttemptStateAfterSmsResendSuccess(): void {
    this.smsCodeState.set('');
    this.barcodeResultState.set(null);
    this.smsStatusState.set('idle');
    this.clearError();
  }

  private clearError(): void {
    this.errorState.set(null);
  }
}

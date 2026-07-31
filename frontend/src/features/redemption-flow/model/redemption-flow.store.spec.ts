import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';

import { PublicRedemptionApi } from '../api/public-redemption.api';
import {
  StartRedemptionRequest,
  StartRedemptionResponse,
  VerifySmsRequest,
  VerifySmsResponse,
} from './redemption-flow.types';
import { PublicRedemptionSignalStore } from './redemption-flow.store';

class PublicRedemptionApiStub {
  startRedemption = vi.fn<
    (request: StartRedemptionRequest) => Observable<StartRedemptionResponse>
  >();
  verifySms = vi.fn<
    (redemptionKey: string, request: VerifySmsRequest) => Observable<VerifySmsResponse>
  >();
}

describe('PublicRedemptionSignalStore', () => {
  let api: PublicRedemptionApiStub;
  let store: PublicRedemptionSignalStore;

  beforeEach(() => {
    api = new PublicRedemptionApiStub();

    TestBed.configureTestingModule({
      providers: [
        PublicRedemptionSignalStore,
        {
          provide: PublicRedemptionApi,
          useValue: api,
        },
      ],
    });

    store = TestBed.inject(PublicRedemptionSignalStore);
  });

  it('starts redemption and enables the SMS step after a successful phone submit', async () => {
    api.startRedemption.mockReturnValue(
      of({
        redemptionKey: 'redemption-key',
        correlationId: 'QPS7O7KCNM',
        smsSent: true,
        retryAfterSeconds: 30,
        smsExpiresAt: '2026-06-26T12:05:00Z',
      }),
    );

    store.setPhone('+380501112233');

    await expect(firstValueFrom(store.startRedemption())).resolves.toBe('advanced');

    expect(api.startRedemption).toHaveBeenCalledWith({ phone: '+380501112233' });
    expect(store.currentStep()).toBe('sms');
    expect(store.canAccessSmsStep()).toBe(true);
    expect(store.correlationId()).toBe('QPS7O7KCNM');
  });

  it('normalizes local Ukrainian phone input before starting redemption', async () => {
    api.startRedemption.mockReturnValue(
      of({
        redemptionKey: 'redemption-key',
        correlationId: 'QPS7O7KCNM',
        smsSent: true,
        retryAfterSeconds: 30,
        smsExpiresAt: '2026-06-26T12:05:00Z',
      }),
    );

    store.setPhone('050 111 22 33');

    await expect(firstValueFrom(store.startRedemption())).resolves.toBe('advanced');

    expect(api.startRedemption).toHaveBeenCalledWith({ phone: '+380501112233' });
    expect(store.phoneValidationError()).toBeNull();
  });

  it('normalizes Ukrainian phone input without country prefix before starting redemption', async () => {
    api.startRedemption.mockReturnValue(
      of({
        redemptionKey: 'redemption-key',
        correlationId: 'QPS7O7KCNM',
        smsSent: true,
        retryAfterSeconds: 30,
        smsExpiresAt: '2026-06-26T12:05:00Z',
      }),
    );

    store.setPhone('501112233');

    await expect(firstValueFrom(store.startRedemption())).resolves.toBe('advanced');

    expect(api.startRedemption).toHaveBeenCalledWith({ phone: '+380501112233' });
    expect(store.phoneValidationError()).toBeNull();
  });

  it('blocks phone submit when the Ukrainian phone format is incomplete', async () => {
    store.setPhone('+38050');

    await expect(firstValueFrom(store.startRedemption())).resolves.toBe('blocked');

    expect(api.startRedemption).not.toHaveBeenCalled();
    expect(store.canSubmitPhone()).toBe(false);
    expect(store.phoneValidationError()).toBe('Введіть український номер у форматі +380501234567.');
  });

  it('keeps the phone step and stores the correlation id when phone submit fails', async () => {
    api.startRedemption.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 429,
            error: {
              error: {
                code: 'sms_retry_later',
                message: 'Зачекайте перед повторною спробою.',
                correlationId: 'ERR7O7KCNM',
              },
            },
          }),
      ),
    );

    store.setPhone('+380501112233');

    await expect(firstValueFrom(store.startRedemption())).resolves.toBe('blocked');

    expect(store.currentStep()).toBe('phone');
    expect(store.phoneStatus()).toBe('error');
    expect(store.correlationId()).toBe('ERR7O7KCNM');
    expect(store.error()?.code).toBe('sms_retry_later');
  });

  it('keeps the SMS step open when the SMS code is invalid', async () => {
    await arrangeStartedRedemption();
    api.verifySms.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: {
              error: {
                code: 'invalid_sms_code',
                message: 'Невірний SMS-код.',
                correlationId: 'QPS7O7KCNM',
              },
            },
          }),
      ),
    );

    store.setSmsCode('123456');

    await expect(firstValueFrom(store.verifySms())).resolves.toBe('blocked');

    expect(store.currentStep()).toBe('sms');
    expect(store.smsStatus()).toBe('error');
    expect(store.error()?.isRestartRequired).toBe(false);
  });

  it('requests a new SMS code for the current phone and clears the previous code', async () => {
    await arrangeStartedRedemption();
    store.setSmsCode('123456');

    api.startRedemption.mockReturnValue(
      of({
        redemptionKey: 'new-redemption-key',
        correlationId: 'NEW7O7KCNM',
        smsSent: true,
        retryAfterSeconds: 180,
        smsExpiresAt: '2026-06-26T12:06:00Z',
      }),
    );

    await expect(firstValueFrom(store.resendSms())).resolves.toBe('advanced');

    expect(api.startRedemption).toHaveBeenLastCalledWith({ phone: '+380501112233' });
    expect(store.currentStep()).toBe('sms');
    expect(store.smsCode()).toBe('');
    expect(store.redemptionKey()).toBe('new-redemption-key');
    expect(store.correlationId()).toBe('NEW7O7KCNM');
    expect(store.retryAfterSeconds()).toBe(180);
  });

  it('keeps the SMS step open when requesting a new SMS code fails', async () => {
    await arrangeStartedRedemption();

    api.startRedemption.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 502,
            error: {
              error: {
                code: 'sms_send_failed',
                message: 'Не вдалося надіслати SMS.',
                correlationId: 'QPS7O7KCNM',
              },
            },
          }),
      ),
    );

    await expect(firstValueFrom(store.resendSms())).resolves.toBe('blocked');

    expect(store.currentStep()).toBe('sms');
    expect(store.phoneStatus()).toBe('error');
    expect(store.error()?.code).toBe('sms_send_failed');
    expect(store.error()?.isRestartRequired).toBe(false);
  });

  it('moves to the barcode step after successful SMS verification', async () => {
    await arrangeStartedRedemption();
    api.verifySms.mockReturnValue(
      of({
        correlationId: 'QPS7O7KCNM',
        barcodeValue: '6R2V2FZ9AK',
        barcodeFormat: 'code128',
        expiresAt: '2026-06-26T12:10:00Z',
        ttlSeconds: 300,
      }),
    );

    store.setSmsCode('123456');

    await expect(firstValueFrom(store.verifySms())).resolves.toBe('advanced');

    expect(api.verifySms).toHaveBeenCalledWith('redemption-key', { code: '123456' });
    expect(store.currentStep()).toBe('barcode');
    expect(store.canAccessBarcodeStep()).toBe(true);
    expect(store.barcodeResult()?.barcodeValue).toBe('6R2V2FZ9AK');
  });

  it('toggles a mock correlation id for support QR testing', () => {
    expect(store.correlationId()).toBeNull();
    expect(store.isMockCorrelationIdActive()).toBe(false);

    store.toggleMockCorrelationId();

    expect(store.correlationId()).toBe('QPS7O7KCNM');
    expect(store.isMockCorrelationIdActive()).toBe(true);

    store.toggleMockCorrelationId();

    expect(store.correlationId()).toBeNull();
    expect(store.isMockCorrelationIdActive()).toBe(false);
  });

  it('toggles a mock barcode result for the result screen', () => {
    expect(store.barcodeResult()).toBeNull();
    expect(store.isMockBarcodeActive()).toBe(false);

    store.toggleMockBarcode();

    expect(store.correlationId()).toBe('QPS7O7KCNM');
    expect(store.barcodeResult()).toEqual(
      expect.objectContaining({
        correlationId: 'QPS7O7KCNM',
        barcodeValue: 'EOBMCDRDRTQPS7O7KCNM',
        barcodeFormat: 'code128',
        ttlSeconds: 300,
      }),
    );
    expect(store.isMockBarcodeActive()).toBe(true);

    store.toggleMockBarcode();

    expect(store.barcodeResult()).toBeNull();
    expect(store.isMockBarcodeActive()).toBe(false);
  });

  async function arrangeStartedRedemption(): Promise<void> {
    api.startRedemption.mockReturnValue(
      of({
        redemptionKey: 'redemption-key',
        correlationId: 'QPS7O7KCNM',
        smsSent: true,
        retryAfterSeconds: 30,
        smsExpiresAt: '2026-06-26T12:05:00Z',
      }),
    );

    store.setPhone('+380501112233');
    await firstValueFrom(store.startRedemption());
  }
});

import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PublicRedemptionApi } from './public-redemption.api';

describe('PublicRedemptionApi', () => {
  let http: {
    post: ReturnType<typeof vi.fn>;
  };
  let api: PublicRedemptionApi;

  beforeEach(() => {
    http = {
      post: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        PublicRedemptionApi,
        {
          provide: HttpClient,
          useValue: http,
        },
      ],
    });

    api = TestBed.inject(PublicRedemptionApi);
  });

  it('normalizes PascalCase start redemption responses from .NET', () => {
    const values: unknown[] = [];
    http.post.mockReturnValue(
      of({
        RedemptionKey: 'EOBMCDRDRT',
        CorrelationId: 'YCZ6KQJEIG',
        SmsSent: true,
        RetryAfterSeconds: 180,
        SmsExpiresAt: '2026-07-03T17:49:01.4395574+00:00',
      }),
    );

    api.startRedemption({ phone: '+380504320316' }).subscribe((response) => values.push(response));

    expect(http.post).toHaveBeenCalledWith('/api/public/redemptions', {
      phone: '+380504320316',
    });
    expect(values).toEqual([
      {
        redemptionKey: 'EOBMCDRDRT',
        correlationId: 'YCZ6KQJEIG',
        smsSent: true,
        retryAfterSeconds: 180,
        smsExpiresAt: '2026-07-03T17:49:01.4395574+00:00',
      },
    ]);
  });

  it('normalizes PascalCase SMS verification responses from .NET', () => {
    const values: unknown[] = [];
    http.post.mockReturnValue(
      of({
        CorrelationId: 'YCZ6KQJEIG',
        BarcodeValue: 'EOBMCDRDRTYCZ6KQJEIG',
        BarcodeFormat: 'code128',
        ExpiresAt: '2026-07-03T17:54:01.4395574+00:00',
        TtlSeconds: 300,
      }),
    );

    api.verifySms('EOBMCDRDRT', { code: '012345' }).subscribe((response) => values.push(response));

    expect(http.post).toHaveBeenCalledWith(
      '/api/public/redemptions/EOBMCDRDRT/sms-verifications',
      { code: '012345' },
    );
    expect(values).toEqual([
      {
        correlationId: 'YCZ6KQJEIG',
        barcodeValue: 'EOBMCDRDRTYCZ6KQJEIG',
        barcodeFormat: 'code128',
        expiresAt: '2026-07-03T17:54:01.4395574+00:00',
        ttlSeconds: 300,
      },
    ]);
  });
});

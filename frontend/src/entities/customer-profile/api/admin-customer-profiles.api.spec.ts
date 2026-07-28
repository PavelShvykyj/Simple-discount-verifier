import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { AdminCustomerProfilesApi } from './admin-customer-profiles.api';

describe('AdminCustomerProfilesApi', () => {
  let http: {
    get: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    patch: ReturnType<typeof vi.fn>;
  };
  let api: AdminCustomerProfilesApi;

  beforeEach(() => {
    http = {
      get: vi.fn(),
      post: vi.fn(),
      patch: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AdminCustomerProfilesApi,
        {
          provide: HttpClient,
          useValue: http,
        },
      ],
    });

    api = TestBed.inject(AdminCustomerProfilesApi);
  });

  it('lists profiles with phone, page size, and continuation token params', () => {
    api.list({
      phone: '+380501234567',
      pageSize: 20,
      continuationToken: 'next-page',
    });

    expect(http.get).toHaveBeenCalledWith(
      '/api/backoffice/customer-profiles',
      expect.objectContaining({
        params: expect.any(HttpParams),
      }),
    );

    const params = http.get.mock.calls[0][1].params as HttpParams;
    expect(params.get('phone')).toBe('+380501234567');
    expect(params.get('pageSize')).toBe('20');
    expect(params.get('continuationToken')).toBe('next-page');
  });

  it('uses encoded phone path values for point reads and updates', () => {
    api.getByPhone('+380501234567');

    expect(http.get).toHaveBeenCalledWith(
      '/api/backoffice/customer-profiles/by-phone/%2B380501234567',
    );

    api.updateByPhone('+380501234567', {
      phone: '+380501234567',
      physicalCardNumber: '4820001234565',
      answers: [{ code: 'fullName', value: 'Олена Коваленко' }],
    });

    expect(http.patch).toHaveBeenCalledWith(
      '/api/backoffice/customer-profiles/by-phone/%2B380501234567',
      {
        phone: '+380501234567',
        physicalCardNumber: '4820001234565',
        answers: [{ code: 'fullName', value: 'Олена Коваленко' }],
      },
    );
  });

  it('creates profiles using the approved backoffice endpoint and payload shape', () => {
    const request = {
      phone: '+380501234567',
      physicalCardNumber: '4820001234565',
      answers: [
        { code: 'fullName', value: 'Олена Коваленко' },
        { code: 'birthDate', value: '1990-04-15' },
        { code: 'favoriteDish', value: null },
      ],
    };

    api.create(request);

    expect(http.post).toHaveBeenCalledWith('/api/backoffice/customer-profiles', request);
  });

  it('sends only the phone and two-digit activation code', () => {
    const request = {
      phone: '+380501234567',
      code: '07',
    };

    api.sendActivationCodeSms(request);

    expect(http.post).toHaveBeenCalledWith(
      '/api/backoffice/customer-profiles/activation-code-sms',
      request,
    );
  });
});

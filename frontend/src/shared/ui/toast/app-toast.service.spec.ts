import { TestBed } from '@angular/core/testing';

import type { ApiErrorMessage } from '../../lib/api-error/api-error';

interface ToastOptions {
  message?: string;
  color?: string;
  duration?: number;
  position?: string;
  buttons?: unknown[];
}

describe('AppToastService', () => {
  let toastOptions: ToastOptions;
  let present: ReturnType<typeof vi.fn>;
  let ToastController: new () => unknown;
  let AppToastService: new (...args: never[]) => {
    showSuccess: (message: string) => Promise<void>;
    showError: (message: ApiErrorMessage, fallbackMessage?: string) => Promise<void>;
    showWarning: (message: string) => Promise<void>;
    showInfo: (message: string) => Promise<void>;
  };

  beforeEach(async () => {
    vi.resetModules();
    present = vi.fn(() => Promise.resolve());

    vi.doMock('@ionic/angular/standalone', () => ({
      ToastController: class ToastController {},
    }));

    ({ ToastController } = await import('@ionic/angular/standalone'));
    ({ AppToastService } = await import('./app-toast.service'));

    TestBed.configureTestingModule({
      providers: [
        AppToastService,
        {
          provide: ToastController,
          useValue: {
            create: vi.fn((options: ToastOptions) => {
              toastOptions = options;

              return Promise.resolve({ present });
            }),
          },
        },
      ],
    });
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('shows success toasts for eight seconds with a dismiss button', async () => {
    const service = TestBed.inject(AppToastService);

    await service.showSuccess('Анкету створено.');

    expect(toastOptions).toEqual(
      expect.objectContaining({
        message: 'Анкету створено.',
        color: 'success',
        duration: 8000,
        position: 'bottom',
        buttons: [expect.objectContaining({ text: 'Закрити', role: 'cancel' })],
      }),
    );
    expect(present).toHaveBeenCalledOnce();
  });

  it('maps error, warning, and info messages to Ionic colors', async () => {
    const service = TestBed.inject(AppToastService);

    await service.showError('Помилка');
    expect(toastOptions.color).toBe('danger');

    await service.showWarning('Увага');
    expect(toastOptions.color).toBe('warning');

    await service.showInfo('Інформація');
    expect(toastOptions.color).toBe('primary');
  });

  it('uses API error messages with correlation ids for error toasts', async () => {
    const service = TestBed.inject(AppToastService);

    await service.showError(
      {
        error: {
          code: 'invalid_phone',
          message: 'Phone is missing or invalid.',
          correlationId: 'abc-123',
        },
      },
      'Не вдалося зберегти анкету.',
    );

    expect(toastOptions.message).toBe('Phone is missing or invalid.\nКод звернення: abc-123');
    expect(toastOptions.color).toBe('danger');
  });

  it('supports PascalCase API error responses from .NET', async () => {
    const service = TestBed.inject(AppToastService);

    await service.showError(
      {
        Error: {
          Code: 'invalid_phone',
          Message: 'Phone is missing or invalid.',
          CorrelationId: 'request-1',
        },
      },
      'Не вдалося зберегти анкету.',
    );

    expect(toastOptions.message).toBe('Phone is missing or invalid.\nКод звернення: request-1');
  });
});

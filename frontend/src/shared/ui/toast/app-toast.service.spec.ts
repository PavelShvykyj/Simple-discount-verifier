import { TestBed } from '@angular/core/testing';

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
    showError: (message: string) => Promise<void>;
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
});

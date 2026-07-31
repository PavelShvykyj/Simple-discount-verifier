import { TestBed } from '@angular/core/testing';

interface ActionSheetOptions {
  header?: string;
  subHeader?: string;
  cssClass?: string | string[];
  buttons?: unknown[];
}

describe('ConfirmActionSheetService', () => {
  let actionSheetOptions: ActionSheetOptions;
  let dismissRole = 'confirm';
  let present: ReturnType<typeof vi.fn>;
  let ActionSheetController: new () => unknown;
  let ConfirmActionSheetService: new (...args: never[]) => unknown;

  beforeEach(async () => {
    vi.resetModules();
    present = vi.fn(() => Promise.resolve());
    dismissRole = 'confirm';

    vi.doMock('@ionic/angular/standalone', () => ({
      ActionSheetController: class ActionSheetController {},
    }));

    ({ ActionSheetController } = await import('@ionic/angular/standalone'));
    ({ ConfirmActionSheetService } = await import('./confirm-action-sheet.service'));

    TestBed.configureTestingModule({
      providers: [
        ConfirmActionSheetService,
        {
          provide: ActionSheetController,
          useValue: {
            create: vi.fn((options: ActionSheetOptions) => {
              actionSheetOptions = options;

              return Promise.resolve({
                present,
                onDidDismiss: () => Promise.resolve({ role: dismissRole }),
              });
            }),
          },
        },
      ],
    });
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('creates a styled warning action sheet and resolves true on confirm', async () => {
    const service = TestBed.inject(ConfirmActionSheetService) as {
      confirm: (options: unknown) => Promise<boolean>;
    };

    await expect(
      service.confirm({
        header: 'Отримати повторний код?',
        message: 'Поточний код ще активний.',
        confirmText: 'Отримати повторний код',
        cancelText: 'Залишити поточний',
        importance: 'warning',
      }),
    ).resolves.toBe(true);

    expect(actionSheetOptions).toEqual(
      expect.objectContaining({
        header: 'Отримати повторний код?',
        subHeader: 'Поточний код ще активний.',
        cssClass: ['sdv-confirm-action-sheet', 'sdv-confirm-action-sheet--warning'],
        buttons: [
          expect.objectContaining({ text: 'Отримати повторний код', role: 'confirm' }),
          expect.objectContaining({ text: 'Залишити поточний', role: 'cancel' }),
        ],
      }),
    );
    expect(present).toHaveBeenCalledOnce();
  });

  it('uses the critical sheet class and resolves false on cancel', async () => {
    dismissRole = 'cancel';
    const service = TestBed.inject(ConfirmActionSheetService) as {
      confirm: (options: unknown) => Promise<boolean>;
    };

    await expect(
      service.confirm({
        header: 'Небезпечна дія',
        confirmText: 'Видалити',
        importance: 'critical',
      }),
    ).resolves.toBe(false);

    expect(actionSheetOptions.cssClass).toEqual([
      'sdv-confirm-action-sheet',
      'sdv-confirm-action-sheet--critical',
    ]);
    expect(actionSheetOptions.buttons).toEqual([
      expect.objectContaining({ text: 'Видалити', role: 'confirm' }),
      expect.objectContaining({ text: 'Скасувати', role: 'cancel' }),
    ]);
  });
});

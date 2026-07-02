/* eslint-disable @angular-eslint/component-selector, @angular-eslint/directive-selector */
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { RedemptionInspectResponse } from '../../../entities/redemption-inspect/model/redemption-inspect.types';

describe('AdminServiceInspectPage', () => {
  let api: {
    inspect: ReturnType<typeof vi.fn>;
  };
  let modalController: {
    create: ReturnType<typeof vi.fn>;
  };
  let toast: {
    showWarning: ReturnType<typeof vi.fn>;
  };
  let AdminRedemptionsInspectApi: new () => unknown;
  let AdminServiceInspectPage: new () => unknown;
  let AppToastService: new () => unknown;
  let ModalController: new () => unknown;

  beforeEach(async () => {
    vi.resetModules();
    mockIonicStandalone();
    api = {
      inspect: vi.fn(),
    };
    modalController = {
      create: vi.fn(),
    };
    toast = {
      showWarning: vi.fn(() => Promise.resolve()),
    };

    ({ ModalController } = await import('@ionic/angular/standalone'));
    ({ AdminRedemptionsInspectApi } = await import(
      '../../../entities/redemption-inspect/api/admin-redemptions-inspect.api'
    ));
    ({ AppToastService } = await import('../../../shared/ui/toast/app-toast.service'));
    ({ AdminServiceInspectPage } = await import('./admin-service-inspect.page'));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminRedemptionsInspectApi,
          useValue: api,
        },
        {
          provide: ModalController,
          useValue: modalController,
        },
        {
          provide: AppToastService,
          useValue: toast,
        },
      ],
    });
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('submits valid support code input and stores the inspection result', () => {
    const response = createInspectResponse();
    api.inspect.mockReturnValue(of(response));
    const page = createPage(AdminServiceInspectPage);

    read<{ setValue: (value: string) => void }>(page, 'inputControl').setValue(
      'SDV-SUPPORT:V1:qps7o7kcnm',
    );
    submit(page);

    expect(api.inspect).toHaveBeenCalledWith({ correlationId: 'QPS7O7KCNM' });
    expect(readSignal(page, 'status')()).toBe('success');
    expect(readSignal(page, 'result')()).toBe(response);
  });

  it('blocks invalid input without calling the backend', () => {
    const page = createPage(AdminServiceInspectPage);

    read<{ setValue: (value: string) => void }>(page, 'inputControl').setValue('bad code');
    submit(page);

    expect(api.inspect).not.toHaveBeenCalled();
    expect(readSignal(page, 'status')()).toBe('error');
    expect(readSignal(page, 'inputErrorText')()).toBe(
      'Код не вдалося розпізнати. Перевірте символи або відскануйте код ще раз.',
    );
  });

  it('uses API error messages when inspection fails', () => {
    api.inspect.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            error: { error: { message: 'Звернення не знайдено.' } },
            status: 404,
          }),
      ),
    );
    const page = createPage(AdminServiceInspectPage);

    read<{ setValue: (value: string) => void }>(page, 'inputControl').setValue('QPS7O7KCNM');
    submit(page);

    expect(readSignal(page, 'status')()).toBe('error');
    expect(readSignal(page, 'result')()).toBeNull();
    expect(readSignal(page, 'errorMessage')()).toBe('Звернення не знайдено.');
  });
});

function createPage(Page: new () => unknown): unknown {
  return TestBed.runInInjectionContext(() => new Page());
}

function submit(page: unknown): void {
  (page as { submit: (event: Event) => void }).submit(new Event('submit'));
}

function readSignal<T = unknown>(target: unknown, key: string): () => T {
  return (target as Record<string, unknown>)[key] as () => T;
}

function read<T>(target: unknown, key: string): T {
  return (target as Record<string, unknown>)[key] as T;
}

function createInspectResponse(): RedemptionInspectResponse {
  return {
    correlationId: 'QPS7O7KCNM',
    redemption: {
      status: 'barcode_issued',
      startedAt: '2026-07-02T09:00:00Z',
      lastEventAt: '2026-07-02T09:05:00Z',
    },
    barcode: null,
    scan: null,
    profile: null,
    auditEvents: [],
  };
}

function mockIonicStandalone(): void {
  vi.doMock('@ionic/angular/standalone', async () => {
    const { Component, Directive, input } =
      await vi.importActual<typeof import('@angular/core')>('@angular/core');

    @Component({ selector: 'ion-action-sheet', standalone: true, template: '' })
    class IonActionSheet {}
    @Component({ selector: 'ion-back-button', standalone: true, template: '' })
    class IonBackButton {}
    @Component({ selector: 'ion-badge', standalone: true, template: '<ng-content />' })
    class IonBadge {}
    @Component({ selector: 'ion-button', standalone: true, template: '<ng-content />' })
    class IonButton {}
    @Component({ selector: 'ion-buttons', standalone: true, template: '<ng-content />' })
    class IonButtons {}
    @Component({ selector: 'ion-card', standalone: true, template: '<ng-content />' })
    class IonCard {}
    @Component({ selector: 'ion-card-content', standalone: true, template: '<ng-content />' })
    class IonCardContent {}
    @Component({ selector: 'ion-card-header', standalone: true, template: '<ng-content />' })
    class IonCardHeader {}
    @Component({ selector: 'ion-card-title', standalone: true, template: '<ng-content />' })
    class IonCardTitle {}
    @Component({ selector: 'ion-col', standalone: true, template: '<ng-content />' })
    class IonCol {}
    @Component({ selector: 'ion-content', standalone: true, template: '<ng-content />' })
    class IonContent {}
    @Component({ selector: 'ion-grid', standalone: true, template: '<ng-content />' })
    class IonGrid {}
    @Component({ selector: 'ion-header', standalone: true, template: '<ng-content />' })
    class IonHeader {}
    @Component({ selector: 'ion-icon', standalone: true, template: '' })
    class IonIcon {}
    @Component({ selector: 'ion-input', standalone: true, template: '<ng-content />' })
    class IonInput {}
    @Component({ selector: 'ion-item', standalone: true, template: '<ng-content />' })
    class IonItem {}
    @Component({ selector: 'ion-label', standalone: true, template: '<ng-content />' })
    class IonLabel {}
    @Component({ selector: 'ion-list', standalone: true, template: '<ng-content />' })
    class IonList {}
    @Component({ selector: 'ion-note', standalone: true, template: '<ng-content />' })
    class IonNote {}
    @Component({ selector: 'ion-row', standalone: true, template: '<ng-content />' })
    class IonRow {}
    @Component({ selector: 'ion-spinner', standalone: true, template: '' })
    class IonSpinner {}
    @Component({ selector: 'ion-text', standalone: true, template: '<ng-content />' })
    class IonText {}
    @Component({ selector: 'ion-title', standalone: true, template: '<ng-content />' })
    class IonTitle {}
    @Component({ selector: 'ion-toolbar', standalone: true, template: '<ng-content />' })
    class IonToolbar {}
    @Directive({ selector: '[slot]', standalone: true })
    class SlotDirective {
      readonly slot = input<string>();
    }
    class ModalController {}
    class ToastController {}

    return {
      IonActionSheet,
      IonBackButton,
      IonBadge,
      IonButton,
      IonButtons,
      IonCard,
      IonCardContent,
      IonCardHeader,
      IonCardTitle,
      IonCol,
      IonContent,
      IonGrid,
      IonHeader,
      IonIcon,
      IonInput,
      IonItem,
      IonLabel,
      IonList,
      IonNote,
      IonRow,
      IonSpinner,
      IonText,
      IonTitle,
      IonToolbar,
      ModalController,
      SlotDirective,
      ToastController,
    };
  });
}

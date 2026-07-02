/* eslint-disable @angular-eslint/component-selector, @angular-eslint/directive-selector */
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

describe('AdminServiceHealthPage', () => {
  let api: {
    getHealth: ReturnType<typeof vi.fn>;
  };
  let SystemHealthApi: new () => unknown;
  let AdminServiceHealthPage: new () => unknown;

  beforeEach(async () => {
    vi.resetModules();
    mockIonicStandalone();
    api = {
      getHealth: vi.fn(),
    };

    ({ SystemHealthApi } = await import(
      '../../../entities/system-health/api/system-health.api'
    ));
    ({ AdminServiceHealthPage } = await import('./admin-service-health.page'));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: SystemHealthApi,
          useValue: api,
        },
      ],
    });
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('loads service health on creation and exposes success text', () => {
    api.getHealth.mockReturnValue(of({ status: 'ok', service: 'api' }));

    const page = createPage(AdminServiceHealthPage);

    expect(api.getHealth).toHaveBeenCalled();
    expect(readSignal(page, 'status')()).toBe('success');
    expect(readSignal(page, 'serviceStatusText')()).toBe('Сервіс доступний');
    expect(readSignal(page, 'serviceStatusDescription')()).toBe(
      'Система готова відповідати на запити адміністраторів і каси.',
    );
  });

  it('shows a manager-friendly message when a 200 response has an unexpected format', () => {
    api.getHealth.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 200 })),
    );

    const page = createPage(AdminServiceHealthPage);

    expect(readSignal(page, 'status')()).toBe('error');
    expect(readSignal(page, 'health')()).toBeNull();
    expect(readSignal(page, 'errorMessage')()).toBe(
      'Сервіс відповів у неочікуваному форматі. Повторіть перевірку або зверніться до технічної підтримки.',
    );
  });
});

function createPage(Page: new () => unknown): unknown {
  return TestBed.runInInjectionContext(() => new Page());
}

function readSignal<T = unknown>(target: unknown, key: string): () => T {
  return (target as Record<string, unknown>)[key] as () => T;
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
    @Component({ selector: 'ion-item', standalone: true, template: '<ng-content />' })
    class IonItem {}
    @Component({ selector: 'ion-label', standalone: true, template: '<ng-content />' })
    class IonLabel {}
    @Component({ selector: 'ion-list', standalone: true, template: '<ng-content />' })
    class IonList {}
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
      IonItem,
      IonLabel,
      IonList,
      IonRow,
      IonSpinner,
      IonText,
      IonTitle,
      IonToolbar,
      ModalController,
      SlotDirective,
    };
  });
}

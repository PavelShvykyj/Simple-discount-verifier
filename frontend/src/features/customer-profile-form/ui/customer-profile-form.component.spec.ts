/* eslint-disable @angular-eslint/component-selector, @angular-eslint/directive-selector */
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';

import type {
  ActivationCodeSmsRequest,
  CustomerProfile,
  CustomerProfileUpsertRequest,
} from '../../../entities/customer-profile/model/customer-profile.types';

class AdminCustomerProfilesApiStub {
  sendActivationCodeSms = vi.fn<(request: ActivationCodeSmsRequest) => Observable<void>>();
  create = vi.fn<(request: CustomerProfileUpsertRequest) => Observable<CustomerProfile>>();
  updateByPhone =
    vi.fn<(phone: string, request: CustomerProfileUpsertRequest) => Observable<CustomerProfile>>();
}

describe('CustomerProfileFormComponent', () => {
  let api: AdminCustomerProfilesApiStub;
  let toast: {
    showSuccess: ReturnType<typeof vi.fn>;
    showError: ReturnType<typeof vi.fn>;
  };
  let modalController: {
    create: ReturnType<typeof vi.fn>;
    dismiss: ReturnType<typeof vi.fn>;
  };
  let AdminCustomerProfilesApi: new () => unknown;
  let ConfirmActionSheetService: new () => unknown;
  let CustomerProfileFormComponent: new () => unknown;
  let AppToastService: new () => unknown;
  let component: unknown;
  let ModalController: new () => unknown;

  beforeEach(async () => {
    vi.resetModules();
    api = new AdminCustomerProfilesApiStub();
    toast = {
      showSuccess: vi.fn(() => Promise.resolve()),
      showError: vi.fn(() => Promise.resolve()),
    };
    modalController = {
      create: vi.fn(),
      dismiss: vi.fn(() => Promise.resolve(true)),
    };

    vi.doMock('@ionic/angular/standalone', async () => {
      const { Component, Directive, input } =
        await vi.importActual<typeof import('@angular/core')>('@angular/core');

      @Component({
        selector: 'ion-button',
        standalone: true,
        template: '<ng-content />',
      })
      class IonButton {}

      @Component({
        selector: 'ion-buttons',
        standalone: true,
        template: '<ng-content />',
      })
      class IonButtons {}

      @Component({
        selector: 'ion-card',
        standalone: true,
        template: '<ng-content />',
      })
      class IonCard {}

      @Component({
        selector: 'ion-card-content',
        standalone: true,
        template: '<ng-content />',
      })
      class IonCardContent {}

      @Component({
        selector: 'ion-col',
        standalone: true,
        template: '<ng-content />',
      })
      class IonCol {}

      @Component({
        selector: 'ion-content',
        standalone: true,
        template: '<ng-content />',
      })
      class IonContent {}

      @Component({
        selector: 'ion-grid',
        standalone: true,
        template: '<ng-content />',
      })
      class IonGrid {}

      @Component({
        selector: 'ion-header',
        standalone: true,
        template: '<ng-content />',
      })
      class IonHeader {}

      @Component({
        selector: 'ion-action-sheet',
        standalone: true,
        template: '',
      })
      class IonActionSheet {}

      @Component({
        selector: 'ion-icon',
        standalone: true,
        template: '',
      })
      class IonIcon {}

      @Component({
        selector: 'ion-input',
        standalone: true,
        template: '<ng-content />',
      })
      class IonInput {}

      @Component({
        selector: 'ion-input-otp',
        standalone: true,
        template: '<ng-content />',
      })
      class IonInputOtp {}

      @Component({
        selector: 'ion-list',
        standalone: true,
        template: '<ng-content />',
      })
      class IonList {}

      @Component({
        selector: 'ion-note',
        standalone: true,
        template: '<ng-content />',
      })
      class IonNote {}

      @Component({
        selector: 'ion-row',
        standalone: true,
        template: '<ng-content />',
      })
      class IonRow {}

      @Component({
        selector: 'ion-spinner',
        standalone: true,
        template: '',
      })
      class IonSpinner {}

      @Component({
        selector: 'ion-textarea',
        standalone: true,
        template: '<ng-content />',
      })
      class IonTextarea {}

      @Component({
        selector: 'ion-text',
        standalone: true,
        template: '<ng-content />',
      })
      class IonText {}

      @Component({
        selector: 'ion-title',
        standalone: true,
        template: '<ng-content />',
      })
      class IonTitle {}

      @Component({
        selector: 'ion-toolbar',
        standalone: true,
        template: '<ng-content />',
      })
      class IonToolbar {}

      @Directive({
        selector: '[slot]',
        standalone: true,
      })
      class SlotDirective {
        readonly slot = input<string>();
      }

      class ModalController {}

      return {
        IonActionSheet,
        IonButton,
        IonButtons,
        IonCard,
        IonCardContent,
        IonCol,
        IonContent,
        IonGrid,
        IonHeader,
        IonIcon,
        IonInput,
        IonInputOtp,
        IonList,
        IonNote,
        IonRow,
        IonSpinner,
        IonTextarea,
        IonText,
        IonTitle,
        IonToolbar,
        ModalController,
        SlotDirective,
      };
    });

    ({ ModalController } = await import('@ionic/angular/standalone'));
    ({ AdminCustomerProfilesApi } =
      await import('../../../entities/customer-profile/api/admin-customer-profiles.api'));
    ({ ConfirmActionSheetService } =
      await import('../../../shared/ui/confirm-action-sheet/confirm-action-sheet.service'));
    ({ AppToastService } = await import('../../../shared/ui/toast/app-toast.service'));
    ({ CustomerProfileFormComponent } = await import('./customer-profile-form.component'));

    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminCustomerProfilesApi,
          useValue: api,
        },
        {
          provide: AppToastService,
          useValue: toast,
        },
        {
          provide: ConfirmActionSheetService,
          useValue: {
            confirm: vi.fn(() => Promise.resolve(true)),
          },
        },
        {
          provide: ModalController,
          useValue: modalController,
        },
      ],
    });

    component = TestBed.runInInjectionContext(() => new CustomerProfileFormComponent());
  });

  afterEach(() => {
    vi.doUnmock('@ionic/angular/standalone');
  });

  it('creates a profile with normalized phone and answers, then resets the form', () => {
    api.sendActivationCodeSms.mockReturnValue(of(void 0));
    api.create.mockReturnValue(of(createProfile()));
    setCreateFormValues(component);
    sendAndMatchActivationCode(component, '07');

    submit(component);

    expect(api.create).toHaveBeenCalledWith({
      phone: '+380501234567',
      physicalCardNumber: '4820001234565',
      answers: [
        { code: 'fullName', value: 'Олена Коваленко' },
        { code: 'birthDate', value: '1990-04-15' },
        { code: 'favoriteDish', value: 'Борщ' },
      ],
    });
    expect(toast.showSuccess).toHaveBeenCalledWith('Анкету створено.');
    expect(modalController.dismiss).not.toHaveBeenCalled();
    expect(getPhoneControl(component).value).toBe('');
    expect(getPhysicalCardNumberControl(component).value).toBe('');
  });

  it('updates an existing profile in modal mode and dismisses with a saved result', () => {
    const profile = createProfile();
    api.updateByPhone.mockReturnValue(of(profile));
    setComponentInput(component, 'mode', 'edit');
    setComponentInput(component, 'presentation', 'modal');
    setComponentInput(component, 'profile', profile);
    getAnswerControl(component, 'favoriteDish').setValue('Вареники');

    submit(component);

    expect(api.updateByPhone).toHaveBeenCalledWith('+380501234567', {
      phone: '+380501234567',
      physicalCardNumber: '4820001234565',
      answers: [
        { code: 'fullName', value: 'Олена Коваленко' },
        { code: 'birthDate', value: '1990-04-15' },
        { code: 'favoriteDish', value: 'Вареники' },
      ],
    });
    expect(toast.showSuccess).toHaveBeenCalledWith('Анкету збережено.');
    expect(modalController.dismiss).toHaveBeenCalledWith({ saved: true, profile });
  });

  it('shows an error toast when saving fails', () => {
    api.sendActivationCodeSms.mockReturnValue(of(void 0));
    api.create.mockReturnValue(throwError(() => new Error('backend offline')));
    setCreateFormValues(component);
    sendAndMatchActivationCode(component, '07');

    submit(component);

    expect(toast.showError).toHaveBeenCalledWith(
      'Не вдалося зберегти анкету.',
      'Не вдалося зберегти анкету.',
    );
    expect(modalController.dismiss).not.toHaveBeenCalled();
  });

  it('passes API error responses to the error toast when saving fails', () => {
    const errorBody = {
      Error: {
        Code: 'invalid_phone',
        Message: 'Phone is missing or invalid.',
        CorrelationId: 'request-1',
      },
    };
    api.create.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: errorBody,
          }),
      ),
    );
    api.sendActivationCodeSms.mockReturnValue(of(void 0));
    setCreateFormValues(component);
    sendAndMatchActivationCode(component, '07');

    submit(component);

    expect(toast.showError).toHaveBeenCalledWith(errorBody, 'Не вдалося зберегти анкету.');
    expect(modalController.dismiss).not.toHaveBeenCalled();
  });

  it('blocks submit when the phone body is invalid', () => {
    getPhoneControl(component).setValue('123');
    getAnswerControl(component, 'fullName').setValue('Олена Коваленко');

    submit(component);

    expect(api.create).not.toHaveBeenCalled();
    expect(toast.showError).not.toHaveBeenCalled();
  });

  it('blocks submit when the phone body is not an accepted Ukrainian national part', () => {
    getPhoneControl(component).setValue('222222222');
    getAnswerControl(component, 'fullName').setValue('Olena');

    submit(component);

    expect(api.create).not.toHaveBeenCalled();
    expect(toast.showError).not.toHaveBeenCalled();
  });

  it.each(['', '482000123456', '482000123456A', '4820001234564'])(
    'blocks submit when the physical card number is invalid: %s',
    (physicalCardNumber) => {
      api.sendActivationCodeSms.mockReturnValue(of(void 0));
      setCreateFormValues(component);
      sendAndMatchActivationCode(component, '07');
      getPhysicalCardNumberControl(component).setValue(physicalCardNumber);

      submit(component);

      expect(api.create).not.toHaveBeenCalled();
    },
  );

  it('fills the physical card control from the scanner modal', async () => {
    const scannerModal = {
      present: vi.fn(() => Promise.resolve()),
      onDidDismiss: vi.fn(() =>
        Promise.resolve({
          data: { value: '4820001234565' },
        }),
      ),
    };
    modalController.create.mockResolvedValue(scannerModal);

    await openPhysicalCardScanner(component);

    expect(modalController.create).toHaveBeenCalledOnce();
    expect(scannerModal.present).toHaveBeenCalledOnce();
    expect(getPhysicalCardNumberControl(component).value).toBe('4820001234565');
    expect(getPhysicalCardNumberControl(component).dirty).toBe(true);
  });

  it('blocks create until the locally entered activation code matches', () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.07);
    api.sendActivationCodeSms.mockReturnValue(of(void 0));
    api.create.mockReturnValue(of(createProfile()));
    setCreateFormValues(component);

    submit(component);
    expect(api.create).not.toHaveBeenCalled();

    sendActivationCode(component);
    expect(api.sendActivationCodeSms).toHaveBeenCalledWith({
      phone: '+380501234567',
      code: '07',
    });

    getActivationCodeControl(component).setValue('08');
    submit(component);
    expect(api.create).not.toHaveBeenCalled();

    getActivationCodeControl(component).setValue('07');
    expect(isActivationCodeMatched(component)).toBe(true);
    expect(canSendActivationCode(component)).toBe(false);

    submit(component);
    expect(api.create).toHaveBeenCalledOnce();
  });

  it('reuses the same code for resend and clears confirmation after a phone edit', () => {
    vi.spyOn(Math, 'random').mockReturnValue(0.42);
    api.sendActivationCodeSms.mockReturnValue(of(void 0));
    api.create.mockReturnValue(of(createProfile()));
    setCreateFormValues(component);

    sendActivationCode(component);
    sendActivationCode(component);

    expect(api.sendActivationCodeSms).toHaveBeenNthCalledWith(1, {
      phone: '+380501234567',
      code: '42',
    });
    expect(api.sendActivationCodeSms).toHaveBeenNthCalledWith(2, {
      phone: '+380501234567',
      code: '42',
    });

    getActivationCodeControl(component).setValue('42');
    expect(isActivationCodeMatched(component)).toBe(true);

    getPhoneControl(component).setValue('671234567');
    expect(isActivationCodeMatched(component)).toBe(false);
    expect(canSendActivationCode(component)).toBe(true);

    submit(component);

    expect(api.create).not.toHaveBeenCalled();
    expect(getActivationCodeControl(component).value).toBe('');
  });

  it('retains the generated code for retry after the first send fails', () => {
    vi.spyOn(Math, 'random').mockReturnValue(0);
    api.sendActivationCodeSms
      .mockReturnValueOnce(throwError(() => new Error('provider unavailable')))
      .mockReturnValueOnce(of(void 0));
    setCreateFormValues(component);

    sendActivationCode(component);
    sendActivationCode(component);

    expect(api.sendActivationCodeSms).toHaveBeenNthCalledWith(1, {
      phone: '+380501234567',
      code: '00',
    });
    expect(api.sendActivationCodeSms).toHaveBeenNthCalledWith(2, {
      phone: '+380501234567',
      code: '00',
    });
  });
});

function setCreateFormValues(component: unknown): void {
  getPhoneControl(component).setValue('501234567');
  getPhysicalCardNumberControl(component).setValue('4820001234565');
  getAnswerControl(component, 'fullName').setValue('Олена Коваленко');
  getAnswerControl(component, 'birthDate').setValue('1990-04-15');
  getAnswerControl(component, 'favoriteDish').setValue('Борщ');
}

function submit(component: unknown): void {
  (component as { submit: (event: Event) => void }).submit(new Event('submit'));
}

function sendActivationCode(component: unknown): void {
  (component as { sendActivationCode: () => void }).sendActivationCode();
}

function isActivationCodeMatched(component: unknown): boolean {
  return (component as { isActivationCodeMatched: () => boolean }).isActivationCodeMatched();
}

function canSendActivationCode(component: unknown): boolean {
  return (component as { canSendActivationCode: () => boolean }).canSendActivationCode();
}

function sendAndMatchActivationCode(component: unknown, code: string): void {
  vi.spyOn(Math, 'random').mockReturnValue(Number(code) / 100);
  sendActivationCode(component);
  getActivationCodeControl(component).setValue(code);
}

function openPhysicalCardScanner(component: unknown): Promise<void> {
  return (
    component as {
      openPhysicalCardScanner: () => Promise<void>;
    }
  ).openPhysicalCardScanner();
}

function setComponentInput(component: unknown, name: string, value: unknown): void {
  (component as Record<string, unknown>)[name] = value;
}

function getPhoneControl(component: unknown) {
  return (component as { phoneControl: { setValue: (value: string) => void; value: string } })
    .phoneControl;
}

function getActivationCodeControl(component: unknown) {
  return (
    component as {
      activationCodeControl: { setValue: (value: string) => void; value: string };
    }
  ).activationCodeControl;
}

function getPhysicalCardNumberControl(component: unknown) {
  return (
    component as {
      physicalCardNumberControl: { setValue: (value: string) => void; value: string };
    }
  ).physicalCardNumberControl as {
    setValue: (value: string) => void;
    value: string;
    dirty: boolean;
  };
}

function getAnswerControl(component: unknown, code: string) {
  return (
    component as {
      answerControls: Record<string, { setValue: (value: string) => void; value: string }>;
    }
  ).answerControls[code];
}

function createProfile(): CustomerProfile {
  return {
    phone: '+380501234567',
    physicalCardNumber: '4820001234565',
    createdAt: '2026-07-02T09:00:00Z',
    updatedAt: '2026-07-02T09:00:00Z',
    answers: [
      {
        code: 'fullName',
        name: 'ФИО',
        value: 'Олена Коваленко',
      },
      {
        code: 'birthDate',
        name: 'День рождения',
        value: '1990-04-15',
      },
      {
        code: 'favoriteDish',
        name: 'Любимое блюдо',
        value: 'Борщ',
      },
    ],
  };
}

import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import {
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
  IonList,
  IonRow,
  IonSpinner,
  IonTextarea,
  IonText,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { barcodeOutline, closeOutline } from 'ionicons/icons';
import { take } from 'rxjs';

import { AdminCustomerProfilesApi } from '../../../entities/customer-profile/api/admin-customer-profiles.api';
import { CUSTOMER_PROFILE_FORM_CONFIG } from '../../../entities/customer-profile/model/customer-profile-form.config';
import { isValidPhysicalCardNumber } from '../../../entities/customer-profile/model/physical-card-number';
import {
  CustomerProfile,
  CustomerProfileFieldConfig,
  CustomerProfileUpsertRequest,
} from '../../../entities/customer-profile/model/customer-profile.types';
import type { ApiErrorMessage } from '../../../shared/lib/api-error/api-error';
import {
  EMPTY_PHONE_MESSAGE,
  INVALID_UKRAINIAN_PHONE_MESSAGE,
  normalizeUkrainianPhone,
} from '../../../shared/lib/phone/ukrainian-phone';
import {
  nineDigitPhoneBodyValidator,
  ukrainianPhoneBodyStartDigitValidator,
} from '../../../shared/lib/phone/ukrainian-phone.validators';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';
import { ConfirmActionSheetService } from '../../../shared/ui/confirm-action-sheet/confirm-action-sheet.service';
import { DisabledButtonColorDirective } from '../../../shared/ui/disabled-button-color/disabled-button-color.directive';
import { AppToastService } from '../../../shared/ui/toast/app-toast.service';

type CustomerProfileFormMode = 'create' | 'edit';
type CustomerProfileFormPresentation = 'page' | 'modal';
type CustomerProfileFormCanDismissRegister = (handler: () => Promise<boolean>) => void;
const UKRAINIAN_PHONE_BODY_LENGTH = 9;
const PHONE_BODY_INVALID_MESSAGE = 'Введіть 9 цифр номера після +380.';
const PHONE_NOT_UKRAINIAN_MESSAGE = 'Введіть український номер телефону.';
const PHYSICAL_CARD_NUMBER_REQUIRED_MESSAGE = 'Введіть номер фізичної картки.';
const PHYSICAL_CARD_NUMBER_INVALID_MESSAGE =
  'Введіть 13 цифр EAN-13 із правильною контрольною цифрою.';
const SAVE_ERROR_MESSAGE = 'Не вдалося зберегти анкету.';

interface CustomerProfileFormFieldView {
  readonly config: CustomerProfileFieldConfig;
  readonly control: FormControl<string>;
  readonly isTouched: () => boolean;
  readonly shouldShowError: () => boolean;
  readonly errorText: () => string;
}

@Component({
  selector: 'app-customer-profile-form',
  imports: [
    DisabledButtonColorDirective,
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
    IonList,
    IonRow,
    IonSpinner,
    IonTextarea,
    IonText,
    IonTitle,
    IonToolbar,
    ReactiveFormsModule,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './customer-profile-form.component.html',
  styleUrl: './customer-profile-form.component.scss',
})
export class CustomerProfileFormComponent {
  private readonly api = inject(AdminCustomerProfilesApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modalController = inject(ModalController);
  private readonly confirmActionSheet = inject(ConfirmActionSheetService);
  private readonly toast = inject(AppToastService);
  private readonly modeState = signal<CustomerProfileFormMode>('create');
  private readonly presentationState = signal<CustomerProfileFormPresentation>('page');
  private readonly profileState = signal<CustomerProfile | null>(null);
  private hasSavedChanges = false;
  private hasCanDismissHost = false;

  constructor() {
    addIcons({ barcodeOutline, closeOutline });
  }

  protected readonly title = computed(() =>
    this.modeState() === 'create' ? 'Нова анкета' : 'Редагування анкети',
  );
  protected readonly submitText = computed(() =>
    this.modeState() === 'create' ? 'Створити' : 'Зберегти',
  );
  protected readonly isModalPresentation = computed(() => this.presentationState() === 'modal');
  protected readonly isSaving = signal(false);

  protected readonly phoneControl = new FormControl('', {
    nonNullable: true,
    validators: [
      requiredTrimmedValidator,
      nineDigitPhoneBodyValidator,
      ukrainianPhoneBodyStartDigitValidator,
    ],
  });
  private readonly phoneControlEvent = toSignal(
    this.phoneControl.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );
  protected readonly physicalCardNumberControl = new FormControl('', {
    nonNullable: true,
    validators: [requiredTrimmedValidator, physicalCardNumberValidator],
  });
  private readonly physicalCardNumberControlEvent = toSignal(
    this.physicalCardNumberControl.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );
  private readonly answerControls = createAnswerControls();

  protected readonly answerForm = new FormGroup(this.answerControls);
  protected readonly profileForm = new FormGroup({
    phone: this.phoneControl,
    physicalCardNumber: this.physicalCardNumberControl,
    answers: this.answerForm,
  });
  private readonly profileFormEvent = toSignal(
    this.profileForm.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );

  protected readonly fieldViews: readonly CustomerProfileFormFieldView[] =
    CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields.map((config) => {
      const control = this.answerControls[config.code];
      const controlEvent = toSignal(control.events.pipe(takeUntilDestroyed(this.destroyRef)));

      return {
        config,
        control,
        isTouched: computed(() => controlEvent()?.source.touched ?? control.touched),
        shouldShowError: computed(
          () => (controlEvent()?.source.touched ?? control.touched) && control.invalid,
        ),
        errorText: computed(() => {
          controlEvent();

          return getFieldErrorText(config, control);
        }),
      };
    });

  protected readonly isPhoneTouched = computed(
    () => this.phoneControlEvent()?.source.touched ?? this.phoneControl.touched,
  );
  protected readonly shouldShowPhoneError = computed(
    () => this.isPhoneTouched() && this.phoneControl.invalid,
  );
  protected readonly phoneErrorText = computed(() => {
    this.phoneControlEvent();

    return getPhoneErrorText(this.phoneControl);
  });
  protected readonly isPhysicalCardNumberTouched = computed(
    () =>
      this.physicalCardNumberControlEvent()?.source.touched ??
      this.physicalCardNumberControl.touched,
  );
  protected readonly shouldShowPhysicalCardNumberError = computed(
    () => this.isPhysicalCardNumberTouched() && this.physicalCardNumberControl.invalid,
  );
  protected readonly physicalCardNumberErrorText = computed(() => {
    this.physicalCardNumberControlEvent();

    return getPhysicalCardNumberErrorText(this.physicalCardNumberControl);
  });
  protected readonly canSubmit = computed(() => {
    this.profileFormEvent();

    return this.profileForm.valid && !this.isSaving();
  });

  set mode(value: CustomerProfileFormMode) {
    this.modeState.set(value);
  }

  set presentation(value: CustomerProfileFormPresentation) {
    this.presentationState.set(value);
  }

  set profile(value: CustomerProfile | null) {
    this.profileState.set(value);

    if (value !== null) {
      this.patchProfile(value);
    }
  }

  set registerCanDismiss(value: CustomerProfileFormCanDismissRegister | undefined) {
    this.hasCanDismissHost = value !== undefined;
    value?.(() => this.canLeave());
  }

  protected async close(): Promise<void> {
    if (!this.isModalPresentation()) {
      return;
    }

    if (!this.hasCanDismissHost && !(await this.canLeave())) {
      return;
    }

    void this.modalController.dismiss({ saved: this.hasSavedChanges });
  }

  async canLeave(): Promise<boolean> {
    if (this.isSaving()) {
      return false;
    }

    if (!this.profileForm.dirty) {
      return true;
    }

    return this.confirmActionSheet.confirm({
      header: 'Закрити без збереження?',
      message: 'Внесені дані буде втрачено.',
      confirmText: 'Закрити',
      cancelText: 'Продовжити заповнення',
      importance: 'warning',
    });
  }

  protected async openPhysicalCardScanner(): Promise<void> {
    const { PhysicalCardScannerComponent } =
      await import('./physical-card-scanner/physical-card-scanner.component');
    const modal = await this.modalController.create({
      component: PhysicalCardScannerComponent,
    });

    await modal.present();
    const scanResult = await modal.onDidDismiss<{ value?: string }>();
    const scannedValue = scanResult.data?.value;

    if (scannedValue === undefined) {
      return;
    }

    this.physicalCardNumberControl.setValue(scannedValue);
    this.physicalCardNumberControl.markAsDirty();
    this.physicalCardNumberControl.markAsTouched();
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.profileForm.markAllAsTouched();
    this.profileForm.updateValueAndValidity();

    if (this.profileForm.invalid) {
      return;
    }

    const request = this.toRequest();
    if (request === null) {
      void this.toast.showError(INVALID_UKRAINIAN_PHONE_MESSAGE);
      return;
    }

    this.isSaving.set(true);
    const saveRequest =
      this.modeState() === 'create'
        ? this.api.create(request)
        : this.api.updateByPhone(this.profileState()?.phone ?? request.phone, request);

    saveRequest.pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (profile) => {
        this.isSaving.set(false);
        this.hasSavedChanges = true;

        if (this.modeState() === 'create') {
          void this.toast.showSuccess('Анкету створено.');
          this.resetForm();
          return;
        }

        void this.toast.showSuccess('Анкету збережено.');
        void this.modalController.dismiss({ saved: true, profile });
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        void this.toast.showError(toSaveErrorMessage(error), SAVE_ERROR_MESSAGE);
      },
    });
  }

  private patchProfile(profile: CustomerProfile): void {
    this.phoneControl.setValue(toUkrainianPhoneBody(profile.phone), { emitEvent: false });
    this.physicalCardNumberControl.setValue(profile.physicalCardNumber, { emitEvent: false });

    for (const field of CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields) {
      const answerValue = profile.answers.find((answer) => answer.code === field.code)?.value ?? '';
      this.answerControls[field.code].setValue(answerValue, { emitEvent: false });
    }

    this.profileForm.markAsPristine();
  }

  private resetForm(): void {
    this.profileForm.reset({
      phone: '',
      physicalCardNumber: '',
      answers: CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields.reduce<Record<string, string>>(
        (answers, field) => {
          answers[field.code] = '';

          return answers;
        },
        {},
      ),
    });
    this.profileForm.markAsPristine();
    this.profileForm.markAsUntouched();
  }

  private toRequest(): CustomerProfileUpsertRequest | null {
    const phone = normalizeUkrainianPhone(this.phoneControl.value);

    if (phone === null) {
      return null;
    }

    return {
      phone,
      physicalCardNumber: this.physicalCardNumberControl.value,
      answers: CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields.map((field) => ({
        code: field.code,
        value: toAnswerValue(this.answerControls[field.code].value),
      })),
    };
  }
}

function createAnswerControls(): Record<string, FormControl<string>> {
  return CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields.reduce<
    Record<string, FormControl<string>>
  >((controls, field) => {
    controls[field.code] = new FormControl('', {
      nonNullable: true,
      validators: createFieldValidators(field),
    });

    return controls;
  }, {});
}

function createFieldValidators(field: CustomerProfileFieldConfig): ValidatorFn[] {
  const validators: ValidatorFn[] = [];

  if (field.required) {
    validators.push(requiredTrimmedValidator);
  }

  if (field.minLength !== undefined) {
    validators.push(Validators.minLength(field.minLength));
  }

  if (field.maxLength !== undefined) {
    validators.push(Validators.maxLength(field.maxLength));
  }

  if (field.type === 'date') {
    validators.push(dateValueValidator);
  }

  return validators;
}

function requiredTrimmedValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');

  return value.trim().length === 0 ? { required: true } : null;
}

function physicalCardNumberValidator(control: AbstractControl): ValidationErrors | null {
  return isValidPhysicalCardNumber(String(control.value ?? ''))
    ? null
    : { physicalCardNumber: true };
}

function dateValueValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();

  if (value.length === 0) {
    return null;
  }

  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? null : { date: true };
}

function getPhoneErrorText(control: AbstractControl): string {
  if (control.hasError('required')) {
    return EMPTY_PHONE_MESSAGE;
  }

  if (control.hasError('phoneBodyFormat')) {
    return PHONE_BODY_INVALID_MESSAGE;
  }

  if (control.hasError('ukrainianPhone')) {
    return PHONE_NOT_UKRAINIAN_MESSAGE;
  }

  return '';
}

function getPhysicalCardNumberErrorText(control: AbstractControl): string {
  return control.hasError('required')
    ? PHYSICAL_CARD_NUMBER_REQUIRED_MESSAGE
    : PHYSICAL_CARD_NUMBER_INVALID_MESSAGE;
}

function getFieldErrorText(field: CustomerProfileFieldConfig, control: AbstractControl): string {
  if (control.hasError('required')) {
    return `${field.label} обов'язкове поле.`;
  }

  if (control.hasError('minlength')) {
    return `Мінімальна довжина: ${field.minLength}.`;
  }

  if (control.hasError('maxlength')) {
    return `Максимальна довжина: ${field.maxLength}.`;
  }

  if (control.hasError('date')) {
    return 'Вкажіть дату у форматі РРРР-ММ-ДД.';
  }

  return '';
}

function toAnswerValue(value: string): string | null {
  const trimmedValue = value.trim();

  return trimmedValue.length > 0 ? trimmedValue : null;
}

function toUkrainianPhoneBody(phone: string): string {
  const normalizedPhone = normalizeUkrainianPhone(phone);

  return normalizedPhone?.slice(-UKRAINIAN_PHONE_BODY_LENGTH) ?? '';
}

function toSaveErrorMessage(error: unknown): ApiErrorMessage {
  if (error instanceof HttpErrorResponse && isApiErrorMessage(error.error)) {
    return error.error;
  }

  return SAVE_ERROR_MESSAGE;
}

function isApiErrorMessage(value: unknown): value is ApiErrorMessage {
  if (typeof value === 'string') {
    return true;
  }

  if (typeof value !== 'object' || value === null) {
    return false;
  }

  return (
    'error' in value ||
    'Error' in value ||
    'message' in value ||
    'Message' in value ||
    'code' in value ||
    'Code' in value ||
    'correlationId' in value ||
    'CorrelationId' in value
  );
}

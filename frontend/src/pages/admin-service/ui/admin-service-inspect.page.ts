import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import {
  IonBackButton,
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
  IonRow,
  IonSpinner,
  IonText,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { qrCodeOutline, searchOutline } from 'ionicons/icons';
import { take } from 'rxjs';

import { AdminRedemptionsInspectApi } from '../../../entities/redemption-inspect/api/admin-redemptions-inspect.api';
import {
  RedemptionInspectRequest,
  RedemptionInspectResponse,
} from '../../../entities/redemption-inspect/model/redemption-inspect.types';
import { parseRedemptionInspectInput } from '../../../features/redemption-inspect/model/redemption-inspect-input.parser';
import { InspectResultComponent } from '../../../features/redemption-inspect/ui/inspect-result/inspect-result.component';
import { ThemeModeSelectorComponent } from '../../../shared/theme/ui/theme-mode-selector.component';
import { DisabledButtonColorDirective } from '../../../shared/ui/disabled-button-color/disabled-button-color.directive';
import { AppToastService } from '../../../shared/ui/toast/app-toast.service';

type InspectLoadStatus = 'idle' | 'loading' | 'success' | 'error';

@Component({
  selector: 'app-admin-service-inspect-page',
  imports: [
    DisabledButtonColorDirective,
    InspectResultComponent,
    IonBackButton,
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
    IonRow,
    IonSpinner,
    IonText,
    IonTitle,
    IonToolbar,
    ReactiveFormsModule,
    ThemeModeSelectorComponent,
  ],
  templateUrl: './admin-service-inspect.page.html',
  styleUrl: './admin-service-inspect.page.scss',
})
export class AdminServiceInspectPage {
  private readonly api = inject(AdminRedemptionsInspectApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modalController = inject(ModalController);
  private readonly toast = inject(AppToastService);

  protected readonly inputControl = new FormControl('', {
    nonNullable: true,
    validators: [requiredTrimmedValidator, inspectInputValidator],
  });
  private readonly inputControlEvent = toSignal(
    this.inputControl.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );
  protected readonly status = signal<InspectLoadStatus>('idle');
  protected readonly result = signal<RedemptionInspectResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isLoading = computed(() => this.status() === 'loading');
  protected readonly isInputTouched = computed(
    () => this.inputControlEvent()?.source.touched ?? this.inputControl.touched,
  );
  protected readonly shouldShowInputError = computed(
    () => this.isInputTouched() && this.inputControl.invalid,
  );
  protected readonly inputErrorText = computed(() => {
    this.inputControlEvent();

    return getInputErrorText(this.inputControl);
  });
  protected readonly canSubmit = computed(() => {
    this.inputControlEvent();

    return this.inputControl.valid && !this.isLoading();
  });

  constructor() {
    addIcons({ qrCodeOutline, searchOutline });
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.inputControl.markAsTouched();
    this.inputControl.updateValueAndValidity();

    if (this.inputControl.invalid) {
      this.errorMessage.set(null);
      this.status.set('error');
      return;
    }

    const parsedInput = parseRedemptionInspectInput(this.inputControl.value);

    if (parsedInput !== null) {
      this.inspect(parsedInput.request);
    }
  }

  protected async openScanner(): Promise<void> {
    const { InspectScannerComponent } = await import(
      '../../../features/redemption-inspect/ui/inspect-scanner/inspect-scanner.component'
    );
    const modal = await this.modalController.create({
      component: InspectScannerComponent,
    });

    await modal.present();
    const scanResult = await modal.onDidDismiss<{ value?: string }>();
    const scannedValue = scanResult.data?.value;

    if (scannedValue === undefined) {
      return;
    }

    this.inputControl.setValue(scannedValue);
    const parsedInput = parseRedemptionInspectInput(scannedValue);

    if (parsedInput === null) {
      void this.toast.showWarning('Цей код підтримки не вдалося розпізнати.');
      return;
    }

    this.inspect(parsedInput.request);
  }

  private inspect(request: RedemptionInspectRequest): void {
    this.status.set('loading');
    this.errorMessage.set(null);

    this.api
      .inspect(request)
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.result.set(response);
          this.status.set('success');
        },
        error: (error: unknown) => {
          this.result.set(null);
          this.errorMessage.set(getInspectErrorMessage(error));
          this.status.set('error');
        },
      });
  }
}

function requiredTrimmedValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');

  return value.trim().length === 0 ? { required: true } : null;
}

function inspectInputValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');

  if (value.trim().length === 0) {
    return null;
  }

  return parseRedemptionInspectInput(value) === null ? { inspectInput: true } : null;
}

function getInputErrorText(control: AbstractControl): string {
  if (control.hasError('required')) {
    return 'Введіть або відскануйте код підтримки.';
  }

  if (control.hasError('inspectInput')) {
    return 'Код не вдалося розпізнати. Перевірте символи або відскануйте код ще раз.';
  }

  return '';
}

function getInspectErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const apiError = (error.error as { error?: { message?: string } } | null)?.error;

    if (apiError?.message) {
      return apiError.message;
    }
  }

  return 'Не вдалося знайти звернення за цим кодом підтримки.';
}

import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonInputOtp,
  IonNote,
  IonSpinner,
  IonText,
} from '@ionic/angular/standalone';
import { take } from 'rxjs';

import { PublicRedemptionNavService } from '../../navigation/public-redemption-nav.service';
import { PUBLIC_REDEMPTION_FLOW_STORE } from '../../model/redemption-flow.store';
import { SMS_CODE_LENGTH } from '../../model/redemption-flow.types';
import { MobileFlowScreenComponent } from '../../../../shared/ui/mobile-flow-screen/mobile-flow-screen.component';
import { DisabledButtonColorDirective } from '../../../../shared/ui/disabled-button-color/disabled-button-color.directive';
import { CountdownTimerComponent } from '../../../../shared/ui/countdown-timer/countdown-timer.component';
import { normalizeUkrainianPhone } from '../../../../shared/lib/phone/ukrainian-phone';

const SMS_REQUIRED_MESSAGE = 'Введіть SMS-код.';
const SMS_INVALID_MESSAGE = `Введіть ${SMS_CODE_LENGTH} цифр SMS-коду.`;
const COUNTDOWN_TICK_MS = 1000;

@Component({
  selector: 'app-sms-verification-screen',
  imports: [
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardSubtitle,
    IonCardTitle,
    IonInputOtp,
    IonNote,
    IonSpinner,
    IonText,
    CountdownTimerComponent,
    DisabledButtonColorDirective,
    MobileFlowScreenComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './sms-verification-screen.component.html',
})
export class SmsVerificationScreenComponent {
  protected readonly flow = inject(PUBLIC_REDEMPTION_FLOW_STORE);
  protected readonly smsCodeLength = SMS_CODE_LENGTH;

  protected readonly nav = inject(PublicRedemptionNavService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly lastSmsRequestAt = signal(Date.now());
  private readonly resendRemainingSecondsState = signal(0);

  protected readonly smsCodeControl = new FormControl(this.flow.smsCode(), {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
  });
  protected readonly smsCodeForm = new FormGroup({
    smsCode: this.smsCodeControl,
  });

  protected readonly smsCodeControlEvent = toSignal(
    this.smsCodeControl.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );



  protected readonly smsCodeErrorText = computed(() => {
    this.smsCodeControlEvent();
    if (this.smsCodeControl.valid) {
      return '';
    }

    if (!this.smsCodeControl.touched) {
      return '';
    }

    return this.smsCodeControl.hasError('required') ? SMS_REQUIRED_MESSAGE : SMS_INVALID_MESSAGE;
  });

  protected readonly isSmsBusy = computed(() => this.flow.smsStatus() === 'submitting');
  protected readonly isResendBusy = computed(() => this.flow.phoneStatus() === 'submitting');
  protected readonly canVerifySms = computed(() => {
    this.smsCodeControlEvent();
    return (
      this.smsCodeControl.valid &&
      this.flow.canAccessSmsStep() &&
      this.flow.smsStatus() !== 'submitting'
    );
  });

  protected readonly displayPhone = computed(() => {
    const normalizedPhone = normalizeUkrainianPhone(this.flow.phone());

    return (
      normalizedPhone ?? (this.flow.phone().trim().length > 0 ? this.flow.phone() : 'вказаний номер')
    );
  });

  protected readonly resendAvailableAt = computed(() => {
    const retryAfterSeconds = this.flow.retryAfterSeconds() ?? 0;

    return this.lastSmsRequestAt() + retryAfterSeconds * COUNTDOWN_TICK_MS;
  });
  protected readonly resendRemainingSeconds = this.resendRemainingSecondsState.asReadonly();

  protected readonly canResendSms = computed(() => {
    return (
      this.flow.canAccessSmsStep() &&
      this.resendRemainingSeconds() === 0 &&
      this.flow.canSubmitPhone()
    );
  });
  protected readonly canGoNext = computed(() => this.flow.canAccessBarcodeStep());

  protected readonly resendButtonText = computed(() => {
    if (this.flow.phoneStatus() === 'submitting') {
      return 'Надсилаємо код...';
    }

    return 'Надіслати код ще раз';
  });

  protected readonly resendHintText = computed(() => {
    const remainingSeconds = this.resendRemainingSeconds();

    if (remainingSeconds > 0) {
      return 'Повторний запит буде доступний через';
    }

    return 'Повторна відправка коду доступна';
  });

  constructor() {
    effect(() => {
      this.resendRemainingSecondsState.set(secondsUntil(this.resendAvailableAt()));
    });

    effect(() => {
      if (this.isSmsBusy()) {
        this.smsCodeControl.disable({ emitEvent: false });
        return;
      }

      this.smsCodeControl.enable({ emitEvent: false });
    });

    this.smsCodeControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((code) => {
      const normalizedCode = toSmsCode(code);

      if (normalizedCode !== code) {
        this.smsCodeControl.setValue(normalizedCode, { emitEvent: false });
      }

      this.flow.setSmsCode(normalizedCode);
    });
  }

  protected markSmsCodeTouched(): void {
    this.smsCodeControl.markAsTouched();
  }

  protected verifySms(): void {
    this.smsCodeControl.markAsTouched();
    this.smsCodeControl.updateValueAndValidity();
    const normalizedCode = toSmsCode(this.smsCodeControl.value);

    if (normalizedCode !== this.smsCodeControl.value) {
      this.smsCodeControl.setValue(normalizedCode, { emitEvent: false });
    }

    this.flow.setSmsCode(normalizedCode);

    if (this.smsCodeControl.invalid) {
      return;
    }

    this.flow
      .verifySms()
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (result === 'advanced') {
          void this.nav.pushBarcode();
        }
      });
  }

  protected resendSms(): void {
    if (!this.canResendSms()) {
      return;
    }

    this.flow
      .resendSms()
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (result === 'advanced') {
          this.smsCodeControl.reset('', { emitEvent: true });
          this.lastSmsRequestAt.set(Date.now());
        }
      });
  }

  protected updateResendRemainingSeconds(remainingSeconds: number): void {
    this.resendRemainingSecondsState.set(remainingSeconds);
  }

  protected goNext(): void {
    if (!this.canGoNext()) {
      return;
    }

    void this.nav.pushBarcode();
  }

  protected restart(): void {
    this.flow.restart();
    void this.nav.resetToPhone();
  }
}

function toSmsCode(value: unknown): string {
  return String(value ?? '').replace(/\D/g, '').slice(0, SMS_CODE_LENGTH);
}

function secondsUntil(targetAt: number): number {
  return Math.max(0, Math.ceil((targetAt - Date.now()) / COUNTDOWN_TICK_MS));
}

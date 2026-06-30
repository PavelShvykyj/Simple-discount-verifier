import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
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
  private readonly now = signal(Date.now());
  private readonly lastSmsRequestAt = signal(Date.now());

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
    const digits = this.flow.phone().replace(/\D/g, '');

    if (digits.length === 9) {
      return `+380${digits}`;
    }

    if (digits.length === 10 && digits.startsWith('0')) {
      return `+38${digits}`;
    }

    if (digits.length === 12 && digits.startsWith('380')) {
      return `+${digits}`;
    }

    return this.flow.phone().trim().length > 0 ? this.flow.phone() : 'вказаний номер';
  });

  protected readonly resendRemainingSeconds = computed(() => {
    const retryAfterSeconds = this.flow.retryAfterSeconds() ?? 0;
    const availableAt = this.lastSmsRequestAt() + retryAfterSeconds * COUNTDOWN_TICK_MS;
    const remainingMs = availableAt - this.now();

    return Math.max(0, Math.ceil(remainingMs / COUNTDOWN_TICK_MS));
  });

  protected readonly canResendSms = computed(() => {
    return (
      this.flow.canAccessSmsStep() &&
      this.resendRemainingSeconds() === 0 &&
      this.flow.phoneStatus() !== 'submitting'
    );
  });
  protected readonly canGoNext = computed(() => this.flow.canAccessBarcodeStep());

  protected readonly resendRemainingTimeText = computed(() => {
    const remainingSeconds = this.resendRemainingSeconds();
    const minutes = Math.floor(remainingSeconds / 60);
    const seconds = String(remainingSeconds % 60).padStart(2, '0');

    return `${minutes}:${seconds}`;
  });

  protected readonly resendButtonText = computed(() => {
    if (this.flow.phoneStatus() === 'submitting') {
      return 'Надсилаємо код...';
    }

    return 'Надіслати код ще раз';
  });

  protected readonly resendHintText = computed(() => {
    const remainingSeconds = this.resendRemainingSeconds();

    if (remainingSeconds > 0) {
      return `Повторний запит буде доступний через ${this.resendRemainingTimeText()}`;
    }

    return 'Повторна відправка коду доступна';
  });

  constructor() {
    this.smsCodeControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((code) => {
      const normalizedCode = toSmsCode(code);

      if (normalizedCode !== code) {
        this.smsCodeControl.setValue(normalizedCode, { emitEvent: false });
      }

      this.flow.setSmsCode(normalizedCode);
    });

    const tick = window.setInterval(() => this.now.set(Date.now()), COUNTDOWN_TICK_MS);
    this.destroyRef.onDestroy(() => window.clearInterval(tick));
  }

  protected markSmsCodeTouched(): void {
    this.smsCodeControl.markAsTouched();
  }

  protected verifySms(): void {
    this.smsCodeControl.markAsTouched();
    this.smsCodeControl.updateValueAndValidity();
    this.flow.setSmsCode(this.smsCodeControl.value);

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
          this.now.set(Date.now());
        }
      });
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

function toSmsCode(value: string): string {
  return value.replace(/\D/g, '').slice(0, SMS_CODE_LENGTH);
}

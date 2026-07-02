import { Component, computed, DestroyRef, effect, inject } from '@angular/core';
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
  IonInput,
  IonNote,
  IonSpinner,
  IonText,
} from '@ionic/angular/standalone';
import { take } from 'rxjs';

import { PublicRedemptionNavService } from '../../navigation/public-redemption-nav.service';
import { PUBLIC_REDEMPTION_FLOW_STORE } from '../../model/redemption-flow.store';
import { MobileFlowScreenComponent } from '../../../../shared/ui/mobile-flow-screen/mobile-flow-screen.component';
import { DisabledButtonColorDirective } from '../../../../shared/ui/disabled-button-color/disabled-button-color.directive';

const UKRAINIAN_PHONE_BODY_LENGTH = 9;
const PHONE_REQUIRED_MESSAGE = 'Введіть номер телефону.';
const PHONE_INVALID_MESSAGE = 'Введіть 9 цифр номера після +380.';

@Component({
  selector: 'app-phone-entry-screen',
  imports: [
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardSubtitle,
    IonCardTitle,
    IonInput,
    IonNote,
    IonSpinner,
    IonText,
    DisabledButtonColorDirective,
    MobileFlowScreenComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './phone-entry-screen.component.html',
})
export class PhoneEntryScreenComponent {
  protected readonly flow = inject(PUBLIC_REDEMPTION_FLOW_STORE);
  protected readonly nav = inject(PublicRedemptionNavService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly phoneControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\d{9}$/), Validators.maxLength(UKRAINIAN_PHONE_BODY_LENGTH)],
  });

  protected phoneForm = new FormGroup({
    phone: this.phoneControl,
  });

  protected readonly phoneControlStatus = toSignal(this.phoneControl.events.pipe(takeUntilDestroyed(this.destroyRef)));
  protected readonly shouldShowPhoneError = computed(() => {
    const isInvalid = this.phoneControlStatus()?.source.invalid;
    const isTouched = this.phoneControlStatus()?.source.touched;
    return isTouched && isInvalid;
  });

  protected readonly isTouched = computed(() => this.phoneControlStatus()?.source.touched);

  protected readonly phoneErrorText = computed(() => {
    const isInvalid = this.phoneControlStatus()?.source.invalid;
    if (!isInvalid) {
      return '';
    }
    return this.phoneControl.hasError('required') ? PHONE_REQUIRED_MESSAGE : PHONE_INVALID_MESSAGE;
  });
  protected readonly canSubmitPhone = computed(
    () => this.phoneControl.valid && this.flow.phoneStatus() !== 'submitting',
  );

  constructor() {
    this.phoneControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((phone) => {
      this.flow.setPhone(phone);
    });

    effect(() => {
      if (this.flow.phoneStatus() === 'submitting') {
        this.phoneControl.disable({ emitEvent: false });
      } else {
        this.phoneControl.enable({ emitEvent: false });
      }
    });
  }

  protected submitPhone(event?: Event): void {
    event?.preventDefault();
    this.phoneControl.markAsTouched();
    this.phoneControl.updateValueAndValidity();
    this.flow.setPhone(this.phoneControl.value);

    if (this.phoneControl.invalid) {
      return;
    }

    this.flow
      .startRedemption()
      .pipe(take(1), takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (result === 'advanced') {
          void this.nav.pushSms();
        }
      });
  }
}

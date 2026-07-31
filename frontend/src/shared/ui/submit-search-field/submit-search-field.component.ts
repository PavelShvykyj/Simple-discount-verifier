import { Component, DestroyRef, computed, effect, inject, input, output } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  FormControl,
  ReactiveFormsModule,
  TouchedChangeEvent,
  ValidatorFn,
  ValueChangeEvent,
} from '@angular/forms';
import { IonSearchbar, IonText } from '@ionic/angular/standalone';
import { DisabledButtonColorDirective } from '../disabled-button-color/disabled-button-color.directive';

type SubmitSearchFieldInputType =
  | 'email'
  | 'number'
  | 'password'
  | 'search'
  | 'tel'
  | 'text'
  | 'url';

export interface SubmitSearchFieldValidator {
  readonly errorKey: string;
  readonly message: string;
  readonly validator: ValidatorFn;
}

@Component({
  selector: 'app-submit-search-field',
  imports: [IonSearchbar, IonText, ReactiveFormsModule, DisabledButtonColorDirective],
  templateUrl: './submit-search-field.component.html',
  styleUrl: './submit-search-field.component.scss',
})
export class SubmitSearchFieldComponent {
  readonly label = input.required<string>();
  readonly placeholder = input('');
  readonly inputType = input<SubmitSearchFieldInputType>('search');
  readonly inputMode = input<string | null>(null);
  readonly autocomplete = input<string | null>(null);
  readonly enterKeyHint = input('search');
  readonly validators = input<readonly SubmitSearchFieldValidator[]>([]);
  readonly busy = input(false);
  readonly disabled = input(false);

  readonly submitted = output<string>();
  readonly blurredWithValue = output<string>();
  readonly cleared = output<void>();

  private readonly destroyRef = inject(DestroyRef);
  private lastSubmittedValue: string | null = null;
  private lastControlValue = '';
  private skipNextTouchedSubmit = false;

  protected readonly control = new FormControl('', {
    nonNullable: true,
  });
  protected readonly controlEvent = toSignal(
    this.control.events.pipe(takeUntilDestroyed(this.destroyRef)),
  );
  protected readonly errorText = computed(() => {
    this.controlEvent();

    if (!this.control.touched || this.control.valid) {
      return null;
    }

    const errors = this.control.errors;

    if (errors === null) {
      return null;
    }

    return this.validators().find((item) => errors[item.errorKey] !== undefined)?.message ?? null;
  });
  protected readonly canSubmit = computed(() => {
    this.controlEvent();

    return this.control.enabled && this.control.valid && !this.busy();
  });

  constructor() {
    effect(() => {
      this.control.setValidators(this.validators().map((item) => item.validator));
      this.control.updateValueAndValidity({ emitEvent: false });
    });

    effect(() => {
      if (this.disabled() && this.control.enabled) {
        this.control.disable({ emitEvent: false });
        return;
      }

      if (!this.disabled() && this.control.disabled) {
        this.control.enable({ emitEvent: false });
      }
    });

    effect(() => {
      const event = this.controlEvent();

      if (event instanceof ValueChangeEvent) {
        this.handleValueChange(event.value);
        return;
      }

      if (!(event instanceof TouchedChangeEvent) || !event.touched) {
        return;
      }

      if (this.skipNextTouchedSubmit) {
        this.skipNextTouchedSubmit = false;
        return;
      }

      this.submitTouchedValue();
    });
  }

  reset(): void {
    this.control.reset('', { emitEvent: true });
    this.lastSubmittedValue = null;
    this.lastControlValue = '';
    this.skipNextTouchedSubmit = false;
  }

  protected submit(event?: Event): void {
    event?.preventDefault();

    if (this.control.disabled) {
      return;
    }

    this.skipNextTouchedSubmit = !this.control.touched;

    if (this.control.value.trim().length === 0) {
      this.skipNextTouchedSubmit = false;
      this.emitCleared();
      this.control.markAsUntouched({ emitEvent: false });
      return;
    }

    this.control.markAsTouched();
    this.control.updateValueAndValidity();

    if (!this.canSubmit()) {
      return;
    }

    this.emitSubmittedValue(this.submitted);
  }

  private submitTouchedValue(): void {
    const value = this.control.value.trim();

    if (value.length === 0) {
      this.control.markAsUntouched({ emitEvent: false });
      return;
    }

    this.control.updateValueAndValidity();

    if (this.control.invalid) {
      return;
    }

    this.emitSubmittedValue(this.blurredWithValue);

    this.control.markAsUntouched({ emitEvent: false });
  }

  private handleValueChange(value: string): void {
    this.skipNextTouchedSubmit = false;

    const normalizedValue = value.trim();
    const previousValue = this.lastControlValue;

    this.lastControlValue = normalizedValue;

    if (normalizedValue.length === 0 && previousValue.length > 0) {
      this.emitCleared();
    }

    if (this.control.touched) {
      this.control.markAsUntouched({ emitEvent: false });
    }
  }

  private emitSubmittedValue(target: { emit(value: string): void }): void {
    const value = this.control.value.trim();

    if (value === this.lastSubmittedValue) {
      return;
    }

    this.lastSubmittedValue = value;
    target.emit(value);
  }

  private emitCleared(): void {
    this.lastSubmittedValue = null;
    this.cleared.emit();
  }
}

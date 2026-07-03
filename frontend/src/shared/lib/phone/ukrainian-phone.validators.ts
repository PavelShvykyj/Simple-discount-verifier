import { AbstractControl, ValidationErrors } from '@angular/forms';

import { isValidUkrainianPhoneBody, normalizeUkrainianPhone } from './ukrainian-phone';

export function nineDigitPhoneBodyValidator(
  control: AbstractControl,
): ValidationErrors | null {
  const value = String(control.value ?? '');

  if (value.trim().length === 0) {
    return null;
  }

  return /^\d{9}$/.test(value) ? null : { phoneBodyFormat: true };
}

export function ukrainianPhoneBodyStartDigitValidator(
  control: AbstractControl,
): ValidationErrors | null {
  const value = String(control.value ?? '');

  if (value.trim().length === 0 || !/^\d{9}$/.test(value)) {
    return null;
  }

  return isValidUkrainianPhoneBody(value) ? null : { ukrainianPhone: true };
}

export function normalizableUkrainianPhoneValidator(
  control: AbstractControl,
): ValidationErrors | null {
  const value = String(control.value ?? '');

  if (value.trim().length === 0) {
    return null;
  }

  return normalizeUkrainianPhone(value) === null ? { ukrainianPhone: true } : null;
}

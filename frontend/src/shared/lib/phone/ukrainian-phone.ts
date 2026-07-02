const UKRAINIAN_COUNTRY_CODE = '380';
const UKRAINIAN_LOCAL_PREFIX = '0';
const UKRAINIAN_E164_DIGITS_LENGTH = 12;
const UKRAINIAN_LOCAL_DIGITS_LENGTH = 10;
const UKRAINIAN_NATIONAL_SIGNIFICANT_DIGITS_LENGTH = 9;

export const EMPTY_PHONE_MESSAGE = 'Введіть номер телефону.';
export const INVALID_UKRAINIAN_PHONE_MESSAGE =
  'Введіть український номер у форматі +380501234567.';

export function normalizeUkrainianPhone(phone: string): string | null {
  const digits = phone.replace(/\D/g, '');

  if (digits.length === UKRAINIAN_E164_DIGITS_LENGTH && digits.startsWith(UKRAINIAN_COUNTRY_CODE)) {
    return `+${digits}`;
  }

  if (digits.length === UKRAINIAN_LOCAL_DIGITS_LENGTH && digits.startsWith(UKRAINIAN_LOCAL_PREFIX)) {
    return `+38${digits}`;
  }

  if (digits.length === UKRAINIAN_NATIONAL_SIGNIFICANT_DIGITS_LENGTH) {
    return `+${UKRAINIAN_COUNTRY_CODE}${digits}`;
  }

  return null;
}

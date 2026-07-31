const UKRAINIAN_COUNTRY_CODE = '380';
const UKRAINIAN_LOCAL_PREFIX = '0';
const UKRAINIAN_E164_DIGITS_LENGTH = 12;
const UKRAINIAN_LOCAL_DIGITS_LENGTH = 10;
const UKRAINIAN_NATIONAL_SIGNIFICANT_DIGITS_LENGTH = 9;

export const EMPTY_PHONE_MESSAGE = 'Введіть номер телефону.';
export const INVALID_UKRAINIAN_PHONE_MESSAGE =
  'Введіть український номер у форматі +380501234567.';

export function isValidUkrainianPhoneBody(phoneBody: string): boolean {
  return isValidUkrainianNationalPart(phoneBody);
}

export function normalizeUkrainianPhone(phone: string): string | null {
  const digits = getAllowedPhoneDigits(phone);

  if (digits === null) {
    return null;
  }

  if (
    digits.length === UKRAINIAN_E164_DIGITS_LENGTH &&
    digits.startsWith(UKRAINIAN_COUNTRY_CODE) &&
    isValidUkrainianNationalPart(digits.slice(UKRAINIAN_COUNTRY_CODE.length))
  ) {
    return `+${digits}`;
  }

  if (digits.length === UKRAINIAN_LOCAL_DIGITS_LENGTH && digits.startsWith(UKRAINIAN_LOCAL_PREFIX)) {
    const nationalPart = digits.slice(UKRAINIAN_LOCAL_PREFIX.length);

    return isValidUkrainianNationalPart(nationalPart) ? `+38${digits}` : null;
  }

  if (
    digits.length === UKRAINIAN_NATIONAL_SIGNIFICANT_DIGITS_LENGTH &&
    isValidUkrainianNationalPart(digits)
  ) {
    return `+${UKRAINIAN_COUNTRY_CODE}${digits}`;
  }

  return null;
}

function getAllowedPhoneDigits(phone: string): string | null {
  let digits = '';

  for (const character of phone.trim()) {
    if (/\d/.test(character)) {
      digits += character;
      continue;
    }

    if (character === '+' || character === ' ' || character === '-' || character === '(' || character === ')') {
      continue;
    }

    return null;
  }

  return digits;
}

function isValidUkrainianNationalPart(nationalPart: string): boolean {
  return /^[3-9]\d{8}$/.test(nationalPart);
}

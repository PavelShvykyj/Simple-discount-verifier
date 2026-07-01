import {
  CUSTOMER_PROFILE_LIST_DETAIL_FIELDS,
  CUSTOMER_PROFILE_LIST_TITLE_FIELD,
} from './customer-profile-form.config';
import {
  CustomerProfile,
  CustomerProfileFieldConfig,
  CustomerProfileListAnswerItem,
  CustomerProfileListItem,
} from './customer-profile.types';

export function toCustomerProfileListItem(profile: CustomerProfile): CustomerProfileListItem {
  return {
    phone: profile.phone,
    title:
      answerValue(profile, CUSTOMER_PROFILE_LIST_TITLE_FIELD) ??
      CUSTOMER_PROFILE_LIST_TITLE_FIELD.list?.emptyText ??
      'Без назви',
    updatedAt: profile.updatedAt,
    details: CUSTOMER_PROFILE_LIST_DETAIL_FIELDS.map((field) =>
      toListAnswerItem(profile, field),
    ).filter((item) => item.value.length > 0),
  };
}

function toListAnswerItem(
  profile: CustomerProfile,
  field: CustomerProfileFieldConfig,
): CustomerProfileListAnswerItem {
  return {
    code: field.code,
    label: field.label,
    value: answerValue(profile, field) ?? '',
  };
}

function answerValue(profile: CustomerProfile, field: CustomerProfileFieldConfig): string | null {
  return profile.answers.find((answer) => answer.code === field.code)?.value ?? null;
}

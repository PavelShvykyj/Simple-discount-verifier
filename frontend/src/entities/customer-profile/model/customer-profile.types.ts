export interface CustomerProfileAnswer {
  readonly code: string;
  readonly name: string;
  readonly value: string | null;
}

export interface CustomerProfileAnswerInput {
  readonly code: string;
  readonly value: string | null;
}

export interface CustomerProfile {
  readonly phone: string;
  readonly physicalCardNumber: string;
  readonly answers: readonly CustomerProfileAnswer[];
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface CustomerProfileUpsertRequest {
  readonly phone: string;
  readonly physicalCardNumber: string;
  readonly answers: readonly CustomerProfileAnswerInput[];
}

export interface CustomerProfileListResponse {
  readonly items: readonly CustomerProfile[];
  readonly continuationToken: string | null;
}

export interface AdminCustomerProfileListRequest {
  readonly phone?: string;
  readonly pageSize?: number;
  readonly continuationToken?: string | null;
}

export interface CustomerProfileListError {
  readonly code: string;
  readonly message: string;
}

export const CUSTOMER_PROFILE_PAGE_SIZE = 20;

export type CustomerProfileFieldType = 'text' | 'date';
export type CustomerProfileFormControlType = 'input' | 'textarea' | 'date';
export type CustomerProfileListFieldRole = 'title' | 'detail';

export interface CustomerProfilePhoneConfig {
  readonly code: 'phone';
  readonly label: string;
  readonly required: true;
  readonly keyField: true;
  readonly placeholder: string;
  readonly inputMode: 'tel';
  readonly autocomplete: 'tel';
}

export interface CustomerProfileFieldListConfig {
  readonly role: CustomerProfileListFieldRole;
  readonly emptyText?: string;
}

export interface CustomerProfileFieldFormConfig {
  readonly control: CustomerProfileFormControlType;
  readonly autocomplete?: string;
}

export interface CustomerProfileFieldConfig {
  readonly code: string;
  readonly label: string;
  readonly backendName: string;
  readonly type: CustomerProfileFieldType;
  readonly required: boolean;
  readonly minLength?: number;
  readonly maxLength?: number;
  readonly format?: string;
  readonly list?: CustomerProfileFieldListConfig;
  readonly form: CustomerProfileFieldFormConfig;
}

export interface CustomerProfileQuestionnaireConfig {
  readonly version: string;
  readonly fields: readonly CustomerProfileFieldConfig[];
}

export interface CustomerProfileFormConfig {
  readonly phone: CustomerProfilePhoneConfig;
  readonly questionnaire: CustomerProfileQuestionnaireConfig;
}

export interface CustomerProfileListAnswerItem {
  readonly code: string;
  readonly label: string;
  readonly value: string;
}

export interface CustomerProfileListItem {
  readonly phone: string;
  readonly title: string;
  readonly updatedAt: string;
  readonly details: readonly CustomerProfileListAnswerItem[];
}

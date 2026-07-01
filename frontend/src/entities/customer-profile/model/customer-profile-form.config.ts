import type { CustomerProfileFieldConfig, CustomerProfileFormConfig } from './customer-profile.types';

export const CUSTOMER_PROFILE_FORM_CONFIG = {
  phone: {
    code: 'phone',
    label: 'Телефон',
    required: true,
    keyField: true,
    placeholder: '+380501234567',
    inputMode: 'tel',
    autocomplete: 'tel',
  },
  questionnaire: {
    version: 'mvp',
    fields: [
      {
        code: 'fullName',
        label: 'ПІБ',
        backendName: 'ФИО',
        type: 'text',
        required: true,
        minLength: 1,
        maxLength: 120,
        list: {
          role: 'title',
          emptyText: 'Без імені',
        },
        form: {
          control: 'input',
          autocomplete: 'name',
        },
      },
      {
        code: 'birthDate',
        label: 'День народження',
        backendName: 'День рождения',
        type: 'date',
        required: false,
        format: 'yyyy-MM-dd',
        form: {
          control: 'date',
        },
      },
      {
        code: 'favoriteDish',
        label: 'Улюблена страва',
        backendName: 'Любимое блюдо',
        type: 'text',
        required: false,
        maxLength: 200,
        form: {
          control: 'textarea',
        },
      },
    ],
  },
} as const satisfies CustomerProfileFormConfig;

const CUSTOMER_PROFILE_FIELDS: readonly CustomerProfileFieldConfig[] =
  CUSTOMER_PROFILE_FORM_CONFIG.questionnaire.fields;

export const CUSTOMER_PROFILE_LIST_TITLE_FIELD: CustomerProfileFieldConfig =
  CUSTOMER_PROFILE_FIELDS.find((field) => field.list?.role === 'title') ??
  CUSTOMER_PROFILE_FIELDS[0];

export const CUSTOMER_PROFILE_LIST_DETAIL_FIELDS: readonly CustomerProfileFieldConfig[] =
  CUSTOMER_PROFILE_FIELDS.filter((field) => field.list?.role === 'detail');

import { CUSTOMER_PROFILE_PAGE_SIZE } from '../../../entities/customer-profile/model/customer-profile.types';
import type { CustomerProfile } from '../../../entities/customer-profile/model/customer-profile.types';
import type {
  PagedDataSourceLoadRequest,
  PagedDataSourcePage,
} from '../../../shared/lib/data-source/paged-data-source';
import type { CustomerProfileListQuery } from './customer-profile-list.datasource';

const MOCK_CUSTOMER_PROFILE_COUNT = 25;

const MOCK_FULL_NAMES = [
  'Олена Коваленко',
  'Андрій Шевченко',
  'Марина Бондар',
  'Дмитро Мельник',
  'Ірина Ткаченко',
  'Сергій Поліщук',
  'Наталія Савчук',
  'Віктор Мороз',
  'Катерина Лисенко',
  'Олександр Романюк',
  'Юлія Гончар',
  'Павло Кравчук',
  'Тетяна Білик',
  'Максим Остапенко',
  'Світлана Клименко',
  'Валерій Руденко',
  'Анна Захарченко',
  'Богдан Павленко',
  'Людмила Сидоренко',
  'Микола Черненко',
  'Віра Кузьменко',
  'Роман Дяченко',
  'Алла Федоренко',
  'Євген Іваненко',
  'Дарина Петренко',
] as const;

const MOCK_FAVORITE_DISHES = ['Борщ', 'Вареники', 'Деруни', 'Голубці', 'Сирники'] as const;

export function createMockCustomerProfilePage(
  request: PagedDataSourceLoadRequest<CustomerProfileListQuery>,
): PagedDataSourcePage<CustomerProfile> {
  const items = filterMockCustomerProfiles(request.query);

  const offset = toOffset(request.continuationToken);
  const pageSize = request.pageSize || CUSTOMER_PROFILE_PAGE_SIZE;
  const pageItems = items.slice(offset, offset + pageSize);
  const nextOffset = offset + pageSize;

  return {
    items: pageItems,
    continuationToken: nextOffset < items.length ? String(nextOffset) : null,
  };
}

function filterMockCustomerProfiles(query: CustomerProfileListQuery): readonly CustomerProfile[] {
  if (query.kind === 'all') {
    return MOCK_CUSTOMER_PROFILES;
  }

  const phone = query.phone;

  return MOCK_CUSTOMER_PROFILES.filter((profile) => profile.phone === phone);
}

const MOCK_CUSTOMER_PROFILES: readonly CustomerProfile[] = Array.from(
  { length: MOCK_CUSTOMER_PROFILE_COUNT },
  (_, index) => {
    const itemNumber = index + 1;
    const phone =
      index === 0 ? '+380501234567' : `+38050${String(1234500 + index).padStart(7, '0')}`;
    const createdAt = `2026-06-${String((index % 25) + 1).padStart(2, '0')}T09:00:00.000Z`;

    return {
      phone,
      physicalCardNumber: '4820001234565',
      createdAt,
      updatedAt: createdAt,
      answers: [
        {
          code: 'fullName',
          name: 'ФИО',
          value: MOCK_FULL_NAMES[index],
        },
        {
          code: 'birthDate',
          name: 'День рождения',
          value: `199${index % 10}-${String((index % 12) + 1).padStart(2, '0')}-${String(
            (index % 27) + 1,
          ).padStart(2, '0')}`,
        },
        {
          code: 'favoriteDish',
          name: 'Любимое блюдо',
          value: `${MOCK_FAVORITE_DISHES[index % MOCK_FAVORITE_DISHES.length]} #${itemNumber}`,
        },
      ],
    };
  },
);

function toOffset(continuationToken: string | null): number {
  if (continuationToken === null) {
    return 0;
  }

  const offset = Number(continuationToken);

  return Number.isInteger(offset) && offset > 0 ? offset : 0;
}

import { HttpErrorResponse } from '@angular/common/http';

import { AdminCustomerProfilesApi } from '../../../entities/customer-profile/api/admin-customer-profiles.api';
import {
  CUSTOMER_PROFILE_PAGE_SIZE,
  CustomerProfile,
  CustomerProfileListError,
} from '../../../entities/customer-profile/model/customer-profile.types';
import { PagedDataSource } from '../../../shared/lib/data-source/paged-data-source';

export type CustomerProfileListQuery =
  | {
      readonly kind: 'all';
    }
  | {
      readonly kind: 'phone';
      readonly phone: string;
    };

export function createCustomerProfileListDataSource(
  api: AdminCustomerProfilesApi,
): PagedDataSource<CustomerProfile, CustomerProfileListQuery, CustomerProfileListError> {
  return new PagedDataSource<CustomerProfile, CustomerProfileListQuery, CustomerProfileListError>({
    pageSize: CUSTOMER_PROFILE_PAGE_SIZE,
    loadPage: (request) =>
      api
        .list({
          phone: request.query.kind === 'phone' ? request.query.phone : undefined,
          pageSize: request.pageSize,
          continuationToken: request.continuationToken,
        }),
    mapError: toCustomerProfileListError,
  });
}

function toCustomerProfileListError(error: unknown): CustomerProfileListError {
  if (error instanceof HttpErrorResponse) {
    const apiError = (error.error as { error?: CustomerProfileListError } | null)?.error;

    return {
      code: apiError?.code ?? `http_${error.status}`,
      message: apiError?.message ?? 'Не вдалося завантажити анкети.',
    };
  }

  return {
    code: 'unknown_error',
    message: 'Не вдалося завантажити анкети.',
  };
}

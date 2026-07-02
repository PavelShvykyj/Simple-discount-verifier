import { of, throwError } from 'rxjs';

import { AdminCustomerProfilesApi } from '../../../entities/customer-profile/api/admin-customer-profiles.api';
import {
  CUSTOMER_PROFILE_PAGE_SIZE,
  CustomerProfileListResponse,
} from '../../../entities/customer-profile/model/customer-profile.types';
import { createCustomerProfileListDataSource } from './customer-profile-list.datasource';

describe('createCustomerProfileListDataSource', () => {
  let api: {
    list: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    api = {
      list: vi.fn(() => throwError(() => new Error('backend offline'))),
    };
  });

  it('requests the first page without loading data until load is called', () => {
    const dataSource = createCustomerProfileListDataSource(
      api as unknown as AdminCustomerProfilesApi,
    );
    const view = dataSource.connect();

    expect(view().status).toBe('idle');
    expect(api.list).not.toHaveBeenCalled();

    dataSource.load({ kind: 'all' });

    expect(api.list).toHaveBeenCalledWith({
      phone: undefined,
      pageSize: CUSTOMER_PROFILE_PAGE_SIZE,
      continuationToken: null,
    });
    expect(view().status).toBe('error');
    expect(view().items).toEqual([]);
    expect(view().continuationToken).toBeNull();
    expect(view().error?.message).toBe('Не вдалося завантажити анкети.');
  });

  it('does not load more items after the first page request fails', () => {
    const dataSource = createCustomerProfileListDataSource(
      api as unknown as AdminCustomerProfilesApi,
    );
    const view = dataSource.connect();

    dataSource.load({ kind: 'all' });
    dataSource.loadMore();

    expect(api.list).toHaveBeenCalledTimes(1);
    expect(view().items).toEqual([]);
    expect(view().continuationToken).toBeNull();
  });

  it('uses exact phone lookup queries and keeps the error state when the request fails', () => {
    const dataSource = createCustomerProfileListDataSource(
      api as unknown as AdminCustomerProfilesApi,
    );
    const view = dataSource.connect();

    dataSource.load({ kind: 'phone', phone: '+380501234567' });

    expect(api.list).toHaveBeenCalledWith({
      phone: '+380501234567',
      pageSize: CUSTOMER_PROFILE_PAGE_SIZE,
      continuationToken: null,
    });
    expect(view().status).toBe('error');
    expect(view().items).toEqual([]);
    expect(view().continuationToken).toBeNull();
  });

  it('keeps API results when the backend request succeeds', () => {
    const response: CustomerProfileListResponse = {
      items: [],
      continuationToken: null,
    };
    api.list.mockReturnValue(of(response));

    const dataSource = createCustomerProfileListDataSource(
      api as unknown as AdminCustomerProfilesApi,
    );
    const view = dataSource.connect();

    dataSource.load({ kind: 'all' });

    expect(view().items).toEqual([]);
    expect(view().continuationToken).toBeNull();
  });
});

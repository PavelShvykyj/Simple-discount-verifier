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
    expect(view().status).toBe('success');
    expect(view().items).toHaveLength(CUSTOMER_PROFILE_PAGE_SIZE);
    expect(view().continuationToken).toBe('20');
  });

  it('loads the next mock page when the local backend is unavailable', () => {
    const dataSource = createCustomerProfileListDataSource(
      api as unknown as AdminCustomerProfilesApi,
    );
    const view = dataSource.connect();

    dataSource.load({ kind: 'all' });
    dataSource.loadMore();

    expect(api.list).toHaveBeenLastCalledWith({
      phone: undefined,
      pageSize: CUSTOMER_PROFILE_PAGE_SIZE,
      continuationToken: '20',
    });
    expect(view().items).toHaveLength(25);
    expect(view().continuationToken).toBeNull();
  });

  it('uses exact phone lookup queries and returns the matching mock profile', () => {
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
    expect(view().items).toHaveLength(1);
    expect(view().items[0].phone).toBe('+380501234567');
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

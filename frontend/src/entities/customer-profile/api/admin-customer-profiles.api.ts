import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AdminCustomerProfileListRequest,
  CustomerProfileListResponse,
  CustomerProfile,
  CustomerProfileUpsertRequest,
} from '../model/customer-profile.types';

@Injectable({ providedIn: 'root' })
export class AdminCustomerProfilesApi {
  private readonly http = inject(HttpClient);

  list(query: AdminCustomerProfileListRequest): Observable<CustomerProfileListResponse> {
    let params = new HttpParams();

    if (query.phone !== undefined) {
      params = params.set('phone', query.phone);
    }

    if (query.pageSize !== undefined) {
      params = params.set('pageSize', query.pageSize);
    }

    if (query.continuationToken) {
      params = params.set('continuationToken', query.continuationToken);
    }

    return this.http.get<CustomerProfileListResponse>('/api/backoffice/customer-profiles', {
      params,
    });
  }

  getByPhone(phone: string): Observable<CustomerProfile> {
    return this.http.get<CustomerProfile>(
      `/api/backoffice/customer-profiles/by-phone/${encodeURIComponent(phone)}`,
    );
  }

  create(request: CustomerProfileUpsertRequest): Observable<CustomerProfile> {
    return this.http.post<CustomerProfile>('/api/backoffice/customer-profiles', request);
  }

  updateByPhone(
    phone: string,
    request: CustomerProfileUpsertRequest,
  ): Observable<CustomerProfile> {
    return this.http.patch<CustomerProfile>(
      `/api/backoffice/customer-profiles/by-phone/${encodeURIComponent(phone)}`,
      request,
    );
  }
}

import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AdminCustomerProfileListRequest,
  CustomerProfileListResponse,
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
}

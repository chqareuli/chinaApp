import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import {
  CreateEntryRequest,
  EntryDetail,
  EntryStatusTotals,
  EntrySummary,
  PagedResult,
} from '../models/entry.models';

@Injectable({ providedIn: 'root' })
export class EntriesService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getMainList(page = 1, pageSize = 25): Observable<PagedResult<EntrySummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<EntrySummary>>(`${this.apiBaseUrl}/entries`, { params });
  }

  search(query: string, limit = 12): Observable<EntrySummary[]> {
    const params = new HttpParams().set('q', query).set('limit', limit);
    return this.http.get<EntrySummary[]>(`${this.apiBaseUrl}/entries/search`, { params });
  }

  getById(id: number): Observable<EntryDetail> {
    return this.http.get<EntryDetail>(`${this.apiBaseUrl}/entries/${id}`);
  }

  create(request: CreateEntryRequest): Observable<EntryDetail> {
    return this.http.post<EntryDetail>(`${this.apiBaseUrl}/entries`, request);
  }

  getStatusTotals(): Observable<EntryStatusTotals> {
    return this.http.get<EntryStatusTotals>(`${this.apiBaseUrl}/accounting/entries-status-totals`);
  }
}

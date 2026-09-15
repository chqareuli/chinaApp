import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import {
  CreateEntryRequest,
  EntryDetail,
  EntryHistoryItem,
  EntryListQuery,
  EntryStatusTotals,
  EntrySummary,
  PagedResult,
  SaveEntryContentRequest,
  VocabularyItem,
} from '../models/entry.models';

@Injectable({ providedIn: 'root' })
export class EntriesService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getMainList(page = 1, pageSize = 25): Observable<PagedResult<EntrySummary>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<EntrySummary>>(`${this.apiBaseUrl}/entries`, { params });
  }

  getAllList(options: EntryListQuery): Observable<PagedResult<EntrySummary>> {
    let params = new HttpParams()
      .set('page', options.page)
      .set('pageSize', options.pageSize)
      .set('sort', options.sort);

    if (options.workEditorId) params = params.set('workEditorId', options.workEditorId);
    if (options.scoreKind) params = params.set('scoreKind', options.scoreKind);
    if (options.authorId) params = params.set('authorId', options.authorId);
    if (options.excludeDrafts) params = params.set('excludeDrafts', true);
    if (options.query?.trim()) {
      params = params.set('q', options.query.trim());
    }
    if (options.status) {
      params = params.set('status', options.status);
    }
    if (options.editorId) {
      params = params.set('editorId', options.editorId);
    }

    return this.http.get<PagedResult<EntrySummary>>(`${this.apiBaseUrl}/entries/all`, { params });
  }

  search(query: string, limit = 12): Observable<EntrySummary[]> {
    const params = new HttpParams().set('q', query).set('limit', limit);
    return this.http.get<EntrySummary[]>(`${this.apiBaseUrl}/entries/search`, { params });
  }

  getById(id: number): Observable<EntryDetail> {
    return this.http.get<EntryDetail>(`${this.apiBaseUrl}/entries/${id}`);
  }

  getHistory(id: number): Observable<EntryHistoryItem[]> {
    return this.http.get<EntryHistoryItem[]>(`${this.apiBaseUrl}/entries/${id}/history`);
  }

  create(request: CreateEntryRequest): Observable<EntryDetail> {
    return this.http.post<EntryDetail>(`${this.apiBaseUrl}/entries`, request);
  }

  saveContent(id: number, request: SaveEntryContentRequest): Observable<EntryDetail> {
    return this.http.put<EntryDetail>(`${this.apiBaseUrl}/entries/${id}/content`, request);
  }

  changeStatus(id: number, targetStatus: string): Observable<EntryDetail> {
    return this.http.post<EntryDetail>(`${this.apiBaseUrl}/entries/${id}/status`, { targetStatus });
  }

  getStatusTotals(): Observable<EntryStatusTotals> {
    return this.http.get<EntryStatusTotals>(`${this.apiBaseUrl}/accounting/entries-status-totals`);
  }

  getVocabularies(category?: VocabularyItem['category']): Observable<VocabularyItem[]> {
    let params = new HttpParams();
    if (category) params = params.set('category', category);
    return this.http.get<VocabularyItem[]>(`${this.apiBaseUrl}/vocabularies`, { params });
  }
}

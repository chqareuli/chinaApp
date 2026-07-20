import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import {
  PublicDictionaryEntry,
  PublicDictionarySearchItem,
  PublicDictionaryStats,
  PublicSearchMode,
} from '../models/public-dictionary.models';

@Injectable({ providedIn: 'root' })
export class PublicDictionaryService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  search(
    query: string,
    mode: PublicSearchMode,
    limit = 25,
  ): Observable<PublicDictionarySearchItem[]> {
    const params = new HttpParams().set('q', query).set('mode', mode).set('limit', limit);
    return this.http.get<PublicDictionarySearchItem[]>(
      `${this.apiBaseUrl}/public/dictionary/search`,
      { params },
    );
  }

  getEntry(id: number): Observable<PublicDictionaryEntry> {
    return this.http.get<PublicDictionaryEntry>(
      `${this.apiBaseUrl}/public/dictionary/entries/${id}`,
    );
  }

  getStats(): Observable<PublicDictionaryStats> {
    return this.http.get<PublicDictionaryStats>(`${this.apiBaseUrl}/public/dictionary/stats`);
  }
}

import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { MediaAssetItem } from '../models/entry.models';

@Injectable({ providedIn: 'root' })
export class MediaService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  listForEntry(entryId: number): Observable<MediaAssetItem[]> {
    return this.http.get<MediaAssetItem[]>(`${this.apiBaseUrl}/entries/${entryId}/media`);
  }

  library(
    page = 1,
    q = '',
  ): Observable<{ items: MediaAssetItem[]; total: number; page: number; pageSize: number }> {
    return this.http.get<{
      items: MediaAssetItem[];
      total: number;
      page: number;
      pageSize: number;
    }>(`${this.apiBaseUrl}/media`, { params: { page, q } });
  }

  upload(
    entryId: number,
    file: File,
    placement: string,
    displayName?: string,
  ): Observable<MediaAssetItem> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (placement) form.append('placement', placement);
    if (displayName?.trim()) form.append('displayName', displayName);
    return this.http.post<MediaAssetItem>(`${this.apiBaseUrl}/entries/${entryId}/media`, form);
  }

  rename(id: number, displayName: string): Observable<void> {
    return this.http.put<void>(this.apiBaseUrl + '/media/' + id + '/name', { displayName });
  }
  replace(id: number, file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.put<void>(this.apiBaseUrl + '/media/' + id + '/content', form);
  }
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiBaseUrl}/media/${id}`);
  }
  reuse(entryId: number, mediaId: number, placement: string): Observable<MediaAssetItem> {
    return this.http.post<MediaAssetItem>(`${this.apiBaseUrl}/entries/${entryId}/media/reuse`, {
      mediaId,
      placement,
    });
  }

  getContent(storageUrl: string): Observable<Blob> {
    return this.http.get(`${this.apiBaseUrl}${storageUrl}`, { responseType: 'blob' });
  }
}

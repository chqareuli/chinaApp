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

  upload(entryId: number, file: File, placement: string): Observable<MediaAssetItem> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (placement) form.append('placement', placement);
    return this.http.post<MediaAssetItem>(`${this.apiBaseUrl}/entries/${entryId}/media`, form);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiBaseUrl}/media/${id}`);
  }

  getContent(storageUrl: string): Observable<Blob> {
    return this.http.get(`${this.apiBaseUrl}${storageUrl}`, { responseType: 'blob' });
  }
}

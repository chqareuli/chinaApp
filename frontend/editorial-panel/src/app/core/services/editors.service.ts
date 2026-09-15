import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CreateEditorRequest, EditorListItem, UpdateEditorRequest } from '../models/admin.models';
import { EditorRole } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class EditorsService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  list(): Observable<EditorListItem[]> {
    return this.http.get<EditorListItem[]>(`${this.apiBaseUrl}/editors`);
  }

  getMe(): Observable<EditorListItem> {
    return this.http.get<EditorListItem>(`${this.apiBaseUrl}/editors/me`);
  }
  updateMe(displayName: string, fields: Partial<EditorListItem> = {}): Observable<EditorListItem> {
    return this.http.put<EditorListItem>(`${this.apiBaseUrl}/editors/me`, {
      ...fields,
      displayName,
    });
  }

  changeOwnPassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/editors/me/password`, {
      currentPassword,
      newPassword,
    });
  }

  create(request: CreateEditorRequest): Observable<EditorListItem> {
    return this.http.post<EditorListItem>(`${this.apiBaseUrl}/editors`, request);
  }

  update(id: number, request: UpdateEditorRequest): Observable<EditorListItem> {
    return this.http.put<EditorListItem>(`${this.apiBaseUrl}/editors/${id}`, request);
  }

  changeRole(id: number, role: EditorRole): Observable<EditorListItem> {
    return this.http.put<EditorListItem>(`${this.apiBaseUrl}/editors/${id}/role`, { role });
  }

  deactivate(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/editors/${id}/deactivate`, {});
  }

  reactivate(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/editors/${id}/reactivate`, {});
  }

  resetPassword(id: number, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/editors/${id}/reset-password`, { newPassword });
  }
}

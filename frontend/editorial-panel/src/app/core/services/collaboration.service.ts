import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import {
  CommentSummary,
  CreateSupportMaterialRequest,
  DirectMessageSummary,
  NotificationSummary,
  ReferenceMaterialDetail,
  SaveReferenceMaterialRequest,
  SupportMaterialSummary,
  UnreadActivitySummary,
} from '../models/collaboration.models';

@Injectable({ providedIn: 'root' })
export class CollaborationService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getComments(entryId: number): Observable<CommentSummary[]> {
    return this.http.get<CommentSummary[]>(`${this.apiBaseUrl}/entries/${entryId}/comments`);
  }

  addComment(entryId: number, commentText: string): Observable<CommentSummary> {
    return this.http.post<CommentSummary>(`${this.apiBaseUrl}/entries/${entryId}/comments`, {
      commentText,
    });
  }

  updateComment(id: number, commentText: string): Observable<CommentSummary> {
    return this.http.put<CommentSummary>(`${this.apiBaseUrl}/comments/${id}`, { commentText });
  }

  archiveComment(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/comments/${id}/archive`, {});
  }

  getSupportMaterials(entryId: number): Observable<SupportMaterialSummary[]> {
    return this.http.get<SupportMaterialSummary[]>(
      `${this.apiBaseUrl}/entries/${entryId}/support-materials`,
    );
  }

  addSupportMaterial(
    entryId: number,
    request: CreateSupportMaterialRequest,
  ): Observable<SupportMaterialSummary> {
    return this.http.post<SupportMaterialSummary>(
      `${this.apiBaseUrl}/entries/${entryId}/support-materials`,
      request,
    );
  }

  getReferenceMaterial(entryId: number): Observable<ReferenceMaterialDetail> {
    return this.http.get<ReferenceMaterialDetail>(
      `${this.apiBaseUrl}/entries/${entryId}/reference-material`,
    );
  }

  saveReferenceMaterial(
    entryId: number,
    request: SaveReferenceMaterialRequest,
  ): Observable<ReferenceMaterialDetail> {
    return this.http.put<ReferenceMaterialDetail>(
      `${this.apiBaseUrl}/entries/${entryId}/reference-material`,
      request,
    );
  }

  getNotifications(unreadOnly = false): Observable<NotificationSummary[]> {
    const params = new HttpParams().set('unreadOnly', unreadOnly);
    return this.http.get<NotificationSummary[]>(`${this.apiBaseUrl}/notifications`, { params });
  }

  markNotificationRead(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/notifications/${id}/read`, {});
  }

  getMessages(): Observable<DirectMessageSummary[]> {
    return this.http.get<DirectMessageSummary[]>(`${this.apiBaseUrl}/messages`);
  }

  sendMessage(recipientEditorId: number, body: string): Observable<DirectMessageSummary> {
    return this.http.post<DirectMessageSummary>(`${this.apiBaseUrl}/messages`, {
      recipientEditorId,
      body,
    });
  }

  markMessageRead(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiBaseUrl}/messages/${id}/read`, {});
  }

  getUnreadActivity(): Observable<UnreadActivitySummary> {
    return this.http.get<UnreadActivitySummary>(`${this.apiBaseUrl}/activity/unread-counts`);
  }
}

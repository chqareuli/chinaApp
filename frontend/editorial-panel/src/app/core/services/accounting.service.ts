import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { EditorAccountingDetail, EditorScoreSummary } from '../models/admin.models';
import { EntryStatusTotals } from '../models/entry.models';

@Injectable({ providedIn: 'root' })
export class AccountingService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getGlobalSummary(): Observable<EditorScoreSummary[]> {
    return this.http.get<EditorScoreSummary[]>(`${this.apiBaseUrl}/accounting/editors`);
  }

  getStatusTotals(): Observable<EntryStatusTotals> {
    return this.http.get<EntryStatusTotals>(`${this.apiBaseUrl}/accounting/entries-status-totals`);
  }

  getEditorDetail(editorId: number): Observable<EditorAccountingDetail> {
    return this.http.get<EditorAccountingDetail>(
      `${this.apiBaseUrl}/accounting/editors/${editorId}`,
    );
  }
}

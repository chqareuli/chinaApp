import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { API_BASE_URL } from '../../../core/config/api.config';
import { PanelFontControl } from '../../../shared/panel-font-control';
import { editorRoleCodes } from '../../../core/models/auth.models';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { EditorAccountingDetail, EditorScoreSummary } from '../../../core/models/admin.models';
import { editorRoleLabels } from '../../../core/models/auth.models';
import { EntryStatusTotals } from '../../../core/models/entry.models';
import { AccountingService } from '../../../core/services/accounting.service';
import { AuthService } from '../../../core/services/auth.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';

@Component({
  selector: 'app-accounting-page',
  imports: [StatusBadge, PanelFontControl, FormsModule, RouterLink],
  templateUrl: './accounting-page.html',
  styleUrl: './accounting-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingPage implements OnInit {
  readonly roleCodes = editorRoleCodes;
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  readonly canExport = computed(() => this.auth.editor()?.role === 'SuperAdmin');
  readonly exporting = signal(false);
  readonly exportError = signal<string | null>(null);
  exportStatus = '';
  exportEditor = '';
  exportFrom = '';
  exportTo = '';
  exportFile(format: 'xml' | 'json' | 'helper'): void {
    if (this.exporting() || !this.canExport()) return;
    this.exportError.set(null);
    if (this.exportFrom && this.exportTo && this.exportFrom > this.exportTo) {
      this.exportError.set('Check dates · 请检查日期');
      return;
    }
    let params = new HttpParams().set('format', format);
    for (const [key, value] of Object.entries({
      status: this.exportStatus,
      editorId: this.exportEditor,
      from: this.exportFrom,
      to: this.exportTo,
    }))
      if (value) params = params.set(key, value);
    this.exporting.set(true);
    this.http
      .get(this.apiUrl + '/entries/export', { params, responseType: 'blob' })
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download =
            format === 'xml'
              ? 'chinese-ge.xml'
              : format === 'helper'
                ? 'chinese-ge-helper-materials.json'
                : 'chinese-ge.json';
          link.click();
          setTimeout(() => URL.revokeObjectURL(url), 1000);
        },
        error: async (error) => {
          let message = 'ჩამოტვირთვა ვერ მოხერხდა.';
          if (error.error instanceof Blob) {
            try {
              message = JSON.parse(await error.error.text()).message || message;
            } catch {}
          }
          this.exportError.set(message);
        },
      });
  }
  private readonly accounting = inject(AccountingService);
  private readonly auth = inject(AuthService);
  readonly totals = signal<EntryStatusTotals | null>(null);
  readonly editors = signal<EditorScoreSummary[]>([]);
  readonly personal = signal<EditorAccountingDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly roleLabels = editorRoleLabels;
  readonly statuses = ['NewEntry', 'ZhReview', 'KaReview', 'Ready', 'Published', 'Archived'];
  readonly canViewGlobal = computed(() => {
    const role = this.auth.editor()?.role;
    return (
      role === 'SuperAdmin' ||
      role === 'ChiefEditor' ||
      role === 'DeputyChiefEditor' ||
      role === 'Editor'
    );
  });
  readonly personalTotals = computed(() => {
    const scores = this.personal()?.scoresByStatus;
    const sum = (values?: Record<string, number>) =>
      Object.values(values ?? {}).reduce((a, b) => a + b, 0);
    return {
      main: sum(scores?.main),
      additional: sum(scores?.additional),
    };
  });
  ngOnInit(): void {
    const id = this.auth.editor()?.id;
    if (!id) {
      this.loading.set(false);
      return;
    }
    const global = this.canViewGlobal()
      ? this.accounting.getGlobalSummary().pipe(catchError(() => of([])))
      : of([]);
    forkJoin({
      totals: this.accounting.getStatusTotals(),
      personal: this.accounting.getEditorDetail(id),
      editors: global,
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => {
          this.totals.set(data.totals);
          this.personal.set(data.personal);
          this.editors.set(data.editors);
        },
        error: () => this.error.set('აღრიცხვის მონაცემები ვერ ჩაიტვირთა.'),
      });
  }
  statusCount(status: string): number {
    return (
      this.totals()?.countsByStatus[status] ??
      this.totals()?.countsByStatus[this.toSnake(status)] ??
      0
    );
  }
  private toSnake(value: string): string {
    return value.replace(/[A-Z]/g, (letter, index) => (index ? '_' : '') + letter.toLowerCase());
  }
}

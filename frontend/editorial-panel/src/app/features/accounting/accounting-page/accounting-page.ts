import { DatePipe } from '@angular/common';
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
  imports: [DatePipe, StatusBadge],
  templateUrl: './accounting-page.html',
  styleUrl: './accounting-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingPage implements OnInit {
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
    return role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'ZhEditor';
  });
  readonly personalTotals = computed(() => {
    const scores = this.personal()?.scoresByStatus;
    const sum = (values?: Record<string, number>) =>
      Object.values(values ?? {}).reduce((a, b) => a + b, 0);
    return {
      main: sum(scores?.main),
      additional: sum(scores?.additional),
      ka: sum(scores?.kaEditor),
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

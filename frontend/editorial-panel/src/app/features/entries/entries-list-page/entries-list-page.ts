import { PanelFontControl } from '../../../shared/panel-font-control';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, finalize, of } from 'rxjs';
import { EntryListQuery, EntrySummary } from '../../../core/models/entry.models';
import { EditorListItem } from '../../../core/models/admin.models';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';
import { EntriesService } from '../../../core/services/entries.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';
import { canSearchContent } from '../../../core/models/content-search';

@Component({
  selector: 'app-entries-list-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, StatusBadge, PanelFontControl],
  templateUrl: './entries-list-page.html',
  styleUrl: './entries-list-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntriesListPage implements OnInit {
  private readonly entriesService = inject(EntriesService);
  private readonly editorsService = inject(EditorsService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private requestSequence = 0;
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly isHome = this.route.snapshot.data['home'] === true;
  readonly authorControl = new FormControl('', { nonNullable: true });

  readonly entries = signal<EntrySummary[]>([]);
  readonly editors = signal<EditorListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = 25;
  readonly totalCount = signal(0);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly canCreateEntry = computed(() => {
    const role = this.auth.editor()?.role;
    return (
      role === 'SuperAdmin' ||
      role === 'ChiefEditor' ||
      role === 'DeputyChiefEditor' ||
      role === 'Editor' ||
      role === 'AssistantEditor'
    );
  });

  readonly queryControl = new FormControl('', { nonNullable: true });
  readonly statusControl = new FormControl('', { nonNullable: true });
  readonly editorControl = new FormControl('', { nonNullable: true });

  readonly statuses = [
    { value: '', label: 'Stage · 状态' },
    { value: 'NewEntry', label: 'New Entry · 新建' },
    { value: 'ZhReview', label: 'CHT-review · 中文审核' },
    { value: 'KaReview', label: 'GEL-review · 格文审核' },
    { value: 'Ready', label: 'Ready · 待发布' },
    { value: 'Published', label: 'Published · 已发布' },
    { value: 'Archived', label: 'Archived · 已归档' },
  ];
  readonly rangeStart = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize, this.totalCount()));

  constructor() {
    this.authorControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.queryControl.setValue(params.get('q') ?? '', { emitEvent: false });
      this.statusControl.setValue(params.get('status') ?? '', { emitEvent: false });
      this.editorControl.setValue(params.get('editorId') ?? '', { emitEvent: false });
      this.statusControl.setValue(params.get('status') ?? '', { emitEvent: false });
      this.resetPageAndLoad();
    });
    this.queryControl.valueChanges
      .pipe(debounceTime(260), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.statusControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.editorControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
  }

  ngOnInit(): void {
    this.editorsService
      .list()
      .pipe(catchError(() => of([] as EditorListItem[])))
      .subscribe((editors) => this.editors.set(editors));
  }

  hasFilters(): boolean {
    return (
      this.authorControl.value.length > 0 ||
      this.queryControl.value.trim().length > 0 ||
      this.statusControl.value.length > 0 ||
      this.editorControl.value.length > 0
    );
  }

  previousPage(): void {
    if (this.page() <= 1) return;
    this.page.update((page) => page - 1);
    this.loadEntries();
  }

  nextPage(): void {
    if (this.page() >= this.pageCount()) return;
    this.page.update((page) => page + 1);
    this.loadEntries();
  }

  goToPage(page: number): void {
    if (this.loading() || page < 1 || page > this.pageCount()) return;
    this.page.set(page);
    this.loadEntries();
  }

  readonly pageNumbers = computed(() => {
    const first = Math.max(1, Math.min(this.page() - 2, this.pageCount() - 4));
    return Array.from({ length: Math.min(5, this.pageCount()) }, (_, index) => first + index);
  });

  clearFilters(): void {
    if (this.route.snapshot.queryParamMap.has('workEditorId')) {
      void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
      return;
    }
    this.authorControl.setValue('', { emitEvent: false });
    this.queryControl.setValue('', { emitEvent: false });
    this.statusControl.setValue('', { emitEvent: false });
    this.editorControl.setValue('', { emitEvent: false });
    this.resetPageAndLoad();
  }

  retry(): void {
    this.loadEntries();
  }

  private resetPageAndLoad(): void {
    this.page.set(1);
    this.loadEntries();
  }

  private loadEntries(): void {
    const requestId = ++this.requestSequence;
    this.loading.set(true);
    this.error.set(null);

    if (this.queryControl.value.trim() && !canSearchContent(this.queryControl.value)) {
      this.entries.set([]);
      this.totalCount.set(0);
      this.loading.set(false);
      this.error.set('Enter at least 1 Chinese character or 2 letters · 至少输入1个汉字或2个字母');
      return;
    }

    const editorValue = Number(this.editorControl.value);
    const options: EntryListQuery = {
      workEditorId: Number(this.route.snapshot.queryParamMap.get('workEditorId')) || undefined,
      scoreKind: this.route.snapshot.queryParamMap.get('scoreKind') || undefined,
      excludeDrafts: false,
      authorId: Number(this.authorControl.value) || undefined,
      page: this.page(),
      pageSize: this.pageSize,
      query: this.queryControl.value,
      status: this.statusControl.value || undefined,
      editorId: Number.isInteger(editorValue) && editorValue > 0 ? editorValue : undefined,
      sort: 'UpdatedDesc',
    };

    this.entriesService
      .getAllList(options)
      .pipe(finalize(() => requestId === this.requestSequence && this.loading.set(false)))
      .subscribe({
        next: (result) => {
          if (requestId !== this.requestSequence) return;
          this.entries.set(result.items);
          this.totalCount.set(result.totalCount);
        },
        error: (error: HttpErrorResponse) => {
          if (requestId !== this.requestSequence) return;
          this.entries.set([]);
          this.totalCount.set(0);
          this.error.set(
            error.status === 0
              ? 'API სერვერთან კავშირი ვერ დამყარდა.'
              : 'სტატიების სრული სიის ჩატვირთვა ვერ მოხერხდა.',
          );
        },
      });
  }
}

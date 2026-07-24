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
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, finalize, of } from 'rxjs';
import { EntryListQuery, EntryListSort, EntrySummary } from '../../../core/models/entry.models';
import { EditorListItem } from '../../../core/models/admin.models';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';
import { EntriesService } from '../../../core/services/entries.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';

@Component({
  selector: 'app-entries-list-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, StatusBadge],
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
    return role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'AssistantEditor';
  });

  readonly queryControl = new FormControl('', { nonNullable: true });
  readonly statusControl = new FormControl('', { nonNullable: true });
  readonly editorControl = new FormControl('', { nonNullable: true });
  readonly sortControl = new FormControl<EntryListSort>('UpdatedDesc', { nonNullable: true });

  readonly statuses = [
    { value: '', label: 'ყველა სტატუსი' },
    { value: 'NewEntry', label: 'ახალი სტატია' },
    { value: 'ZhReview', label: 'ჩინური შემოწმება' },
    { value: 'KaReview', label: 'ქართული შემოწმება' },
    { value: 'Ready', label: 'მზადაა' },
    { value: 'Published', label: 'გამოქვეყნებული' },
    { value: 'Archived', label: 'არქივი' },
  ];
  readonly sorts: { value: EntryListSort; label: string }[] = [
    { value: 'UpdatedDesc', label: 'ბოლოს განახლებული' },
    { value: 'UpdatedAsc', label: 'ძველი განახლებული' },
    { value: 'LemmaAsc', label: 'სიტყვა: ა–ჰ' },
    { value: 'LemmaDesc', label: 'სიტყვა: ჰ–ა' },
    { value: 'Status', label: 'სტატუსის მიხედვით' },
  ];
  readonly rangeStart = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize, this.totalCount()));

  constructor() {
    this.queryControl.valueChanges
      .pipe(debounceTime(260), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.statusControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.editorControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
    this.sortControl.valueChanges
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.resetPageAndLoad());
  }

  ngOnInit(): void {
    this.loadEntries();
    this.editorsService
      .list()
      .pipe(catchError(() => of([] as EditorListItem[])))
      .subscribe((editors) => this.editors.set(editors));
  }

  hasFilters(): boolean {
    return (
      this.queryControl.value.trim().length > 0 ||
      this.statusControl.value.length > 0 ||
      this.editorControl.value.length > 0 ||
      this.sortControl.value !== 'UpdatedDesc'
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

  clearFilters(): void {
    this.queryControl.setValue('', { emitEvent: false });
    this.statusControl.setValue('', { emitEvent: false });
    this.editorControl.setValue('', { emitEvent: false });
    this.sortControl.setValue('UpdatedDesc', { emitEvent: false });
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

    const editorValue = Number(this.editorControl.value);
    const options: EntryListQuery = {
      page: this.page(),
      pageSize: this.pageSize,
      query: this.queryControl.value,
      status: this.statusControl.value || undefined,
      editorId: Number.isInteger(editorValue) && editorValue > 0 ? editorValue : undefined,
      sort: this.sortControl.value,
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

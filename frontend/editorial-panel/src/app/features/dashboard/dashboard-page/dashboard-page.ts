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
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, Subject, switchMap, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EntryStatusTotals, EntrySummary } from '../../../core/models/entry.models';
import { AuthService } from '../../../core/services/auth.service';
import { EntriesService } from '../../../core/services/entries.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';

@Component({
  selector: 'app-dashboard-page',
  imports: [DatePipe, RouterLink, StatusBadge],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage implements OnInit {
  private readonly entriesService = inject(EntriesService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchChanges = new Subject<string>();

  readonly editor = this.auth.editor;
  readonly canCreateEntry = computed(() => this.editor()?.role !== 'KaEditor');
  readonly entries = signal<EntrySummary[]>([]);
  readonly stats = signal<EntryStatusTotals | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly searchQuery = signal('');
  readonly searchResults = signal<EntrySummary[]>([]);
  readonly searchLoading = signal(false);
  readonly searchOpen = computed(() => this.searchQuery().length > 0);

  constructor() {
    this.searchChanges
      .pipe(
        debounceTime(240),
        distinctUntilChanged(),
        tap((query) => {
          this.searchQuery.set(query);
          this.searchLoading.set(query.length > 0);
          if (!query) {
            this.searchResults.set([]);
          }
        }),
        switchMap((query) =>
          query
            ? this.entriesService.search(query).pipe(
                catchError(() => of([])),
                finalize(() => this.searchLoading.set(false)),
              )
            : of([]),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((results) => this.searchResults.set(results));
  }

  ngOnInit(): void {
    this.loadEntries();
    this.entriesService
      .getStatusTotals()
      .pipe(catchError(() => of(null)))
      .subscribe((stats) => this.stats.set(stats));
  }

  onSearchInput(event: Event): void {
    const query = (event.target as HTMLInputElement).value.trim();
    this.searchQuery.set(query);
    this.searchChanges.next(query);
  }

  clearSearch(input: HTMLInputElement): void {
    input.value = '';
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.searchChanges.next('');
    input.focus();
  }

  statusCount(status: string): number {
    const counts = this.stats()?.countsByStatus ?? {};
    return counts[status] ?? counts[this.toPascalStatus(status)] ?? 0;
  }

  previousPage(): void {
    if (this.page() > 1) {
      this.page.update((page) => page - 1);
      this.loadEntries();
    }
  }

  nextPage(): void {
    if (this.page() < this.pageCount()) {
      this.page.update((page) => page + 1);
      this.loadEntries();
    }
  }

  retry(): void {
    this.loadEntries();
  }

  private loadEntries(): void {
    this.loading.set(true);
    this.error.set(null);
    this.entriesService
      .getMainList(this.page(), this.pageSize)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.entries.set(result.items);
          this.totalCount.set(result.totalCount);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(
            error.status === 0
              ? 'API სერვერთან კავშირი ვერ დამყარდა.'
              : 'სტატიების ჩატვირთვა ვერ მოხერხდა.',
          );
        },
      });
  }

  private toPascalStatus(status: string): string {
    return status
      .split('_')
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join('');
  }
}

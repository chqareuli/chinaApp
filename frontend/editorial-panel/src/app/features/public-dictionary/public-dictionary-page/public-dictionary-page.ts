import { DecimalPipe } from '@angular/common';
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
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, EMPTY, finalize, Subject, switchMap } from 'rxjs';
import {
  PublicDictionarySearchItem,
  PublicSearchMode,
} from '../../../core/models/public-dictionary.models';
import { PublicDictionaryService } from '../../../core/services/public-dictionary.service';

@Component({
  selector: 'app-public-dictionary-page',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './public-dictionary-page.html',
  styleUrl: './public-dictionary-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicDictionaryPage implements OnInit {
  private readonly dictionary = inject(PublicDictionaryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchRequests = new Subject<{ query: string; mode: PublicSearchMode }>();

  readonly query = signal('');
  readonly mode = signal<PublicSearchMode>('word');
  readonly results = signal<PublicDictionarySearchItem[]>([]);
  readonly publishedEntries = signal(0);
  readonly searching = signal(false);
  readonly searched = signal(false);
  readonly error = signal<string | null>(null);
  readonly placeholder = computed(() =>
    this.mode() === 'word' ? 'შეიყვანეთ ჩინური სიტყვა ან პინინი' : 'მოძებნეთ სტატიის მთელ ტექსტში',
  );

  constructor() {
    this.searchRequests
      .pipe(
        debounceTime(260),
        switchMap(({ query, mode }) => {
          this.searching.set(true);
          this.searched.set(true);
          this.error.set(null);
          return this.dictionary.search(query, mode).pipe(
            catchError(() => {
              this.error.set('ძიება ვერ შესრულდა. სცადეთ ხელახლა.');
              return EMPTY;
            }),
            finalize(() => this.searching.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((items) => this.results.set(items));
  }

  ngOnInit(): void {
    this.dictionary.getStats().subscribe({
      next: (stats) => this.publishedEntries.set(stats.publishedEntries),
    });
  }

  updateQuery(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
    if (value.trim()) {
      this.searchRequests.next({ query: value.trim(), mode: this.mode() });
    } else {
      this.results.set([]);
      this.searched.set(false);
      this.searching.set(false);
    }
  }

  setMode(mode: PublicSearchMode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    const query = this.query().trim();
    if (query) {
      this.searchRequests.next({ query, mode });
    }
  }

  searchNow(): void {
    const query = this.query().trim();
    if (query) {
      this.searchRequests.next({ query, mode: this.mode() });
    }
  }
}

import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { PublicDictionaryEntry } from '../../../core/models/public-dictionary.models';
import { PublicDictionaryService } from '../../../core/services/public-dictionary.service';

@Component({
  selector: 'app-public-dictionary-entry-page',
  imports: [DatePipe, NgTemplateOutlet, RouterLink],
  templateUrl: './public-dictionary-entry-page.html',
  styleUrl: './public-dictionary-entry-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicDictionaryEntryPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly dictionary = inject(PublicDictionaryService);
  private readonly destroyRef = inject(DestroyRef);

  readonly entry = signal<PublicDictionaryEntry | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        map((params) => Number(params.get('id'))),
        distinctUntilChanged(),
        tap(() => {
          this.entry.set(null);
          this.notFound.set(false);
          this.loading.set(true);
        }),
        switchMap((id) => id
          ? this.dictionary.getEntry(id).pipe(catchError(() => of(null)))
          : of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((entry) => {
        this.entry.set(entry);
        this.notFound.set(entry === null);
        this.loading.set(false);
      });
  }
}

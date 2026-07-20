import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PublicDictionaryEntry } from '../../../core/models/public-dictionary.models';
import { PublicDictionaryService } from '../../../core/services/public-dictionary.service';

@Component({
  selector: 'app-public-dictionary-entry-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './public-dictionary-entry-page.html',
  styleUrl: './public-dictionary-entry-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicDictionaryEntryPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly dictionary = inject(PublicDictionaryService);

  readonly entry = signal<PublicDictionaryEntry | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.loading.set(false);
      this.notFound.set(true);
      return;
    }

    this.dictionary
      .getEntry(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (entry) => this.entry.set(entry),
        error: () => this.notFound.set(true),
      });
  }
}

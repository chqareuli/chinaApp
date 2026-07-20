import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { EntryDetail } from '../../../core/models/entry.models';
import { EntriesService } from '../../../core/services/entries.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';

@Component({
  selector: 'app-entry-detail-page',
  imports: [DatePipe, RouterLink, StatusBadge],
  templateUrl: './entry-detail-page.html',
  styleUrl: './entry-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly entriesService = inject(EntriesService);
  readonly entry = signal<EntryDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(id)) {
      this.error.set('სტატიის ნომერი არასწორია.');
      this.loading.set(false);
      return;
    }

    this.entriesService
      .getById(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (entry) => this.entry.set(entry),
        error: (error: HttpErrorResponse) =>
          this.error.set(error.status === 404 ? 'სტატია ვერ მოიძებნა.' : 'სტატიის ჩატვირთვა ვერ მოხერხდა.'),
      });
  }
}

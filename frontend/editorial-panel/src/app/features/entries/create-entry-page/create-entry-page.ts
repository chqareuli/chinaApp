import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { EntriesService } from '../../../core/services/entries.service';
import { PublicDictionaryEntry } from '../../../core/models/public-dictionary.models';
import { EntryPreviewModal } from '../../../shared/entry-preview-modal/entry-preview-modal';

@Component({
  selector: 'app-create-entry-page',
  imports: [EntryPreviewModal, ReactiveFormsModule, RouterLink],
  templateUrl: './create-entry-page.html',
  styleUrl: './create-entry-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateEntryPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly entriesService = inject(EntriesService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly previewEntry = signal<PublicDictionaryEntry | null>(null);
  readonly form = this.formBuilder.nonNullable.group({
    lemma: ['', [Validators.required, Validators.maxLength(255)]],
  });

  openPreview(): void {
    this.previewEntry.set({
      id: 0,
      lemma: this.form.controls.lemma.value.trim() || '—',
      pinyin: null,
      pinyins: [],
      complexForms: [],
      updatedAtUtc: new Date().toISOString(),
      homonyms: [],
      gramGroups: [],
      directPoses: [],
      senses: [],
    });
  }

  closePreview(): void {
    this.previewEntry.set(null);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.entriesService
      .create({
        lemma: value.lemma.trim(),
        pinyins: [],
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (entry) => void this.router.navigate(['/adminpanel/entries', entry.id]),
        error: (error: HttpErrorResponse) => {
          this.error.set(
            error.status === 403
              ? 'თქვენს როლს ახალი სტატიის შექმნის უფლება არ აქვს.'
              : error.error?.message || 'სტატიის შექმნა ვერ მოხერხდა.',
          );
        },
      });
  }
}

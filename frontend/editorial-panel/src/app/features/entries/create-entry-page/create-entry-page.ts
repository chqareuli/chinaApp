import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { EntriesService } from '../../../core/services/entries.service';

@Component({
  selector: 'app-create-entry-page',
  imports: [ReactiveFormsModule, RouterLink],
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
  readonly form = this.formBuilder.nonNullable.group({
    lemma: ['', [Validators.required, Validators.maxLength(255)]],
    pinyin: ['', Validators.maxLength(255)],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.entriesService
      .create({ lemma: value.lemma.trim(), pinyin: value.pinyin.trim() || null })
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

import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { editorRoleLabels } from '../../../core/models/auth.models';
import { PersonalScoreStatistics, ScoreCountsByStatus } from '../../../core/models/admin.models';
import { AccountingService } from '../../../core/services/accounting.service';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';

@Component({
  selector: 'app-profile-page',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly editors = inject(EditorsService);
  private readonly accounting = inject(AccountingService);
  private readonly fb = inject(FormBuilder);

  readonly editor = this.auth.editor;
  readonly roleLabels = editorRoleLabels;
  readonly profileSaving = signal(false);
  readonly passwordSaving = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly profileError = signal<string | null>(null);
  readonly passwordMessage = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);
  readonly statistics = signal<PersonalScoreStatistics | null>(null);
  readonly scoreBreakdown = signal<ScoreCountsByStatus | null>(null);
  readonly statisticsLoading = signal(true);
  readonly statisticsError = signal<string | null>(null);
  readonly scoreStatuses = [
    { key: 'NewEntry', label: 'ახალი' },
    { key: 'ZhReview', label: 'ჩინური რედაქტირება' },
    { key: 'KaReview', label: 'ქართული რედაქტირება' },
    { key: 'Ready', label: 'მზადაა' },
    { key: 'Published', label: 'გამოქვეყნებულია' },
    { key: 'Archived', label: 'არქივი' },
  ];
  readonly lastActivityAtUtc = computed(() => {
    const statistics = this.statistics();
    if (!statistics) return null;
    const dates = [
      statistics.main.lastAwardedAtUtc,
      statistics.additional.lastAwardedAtUtc,
      statistics.kaEditor.lastAwardedAtUtc,
    ].filter((date): date is string => date !== null);
    return dates.length ? dates.sort().at(-1)! : null;
  });

  readonly profileForm = this.fb.nonNullable.group({
    displayName: [
      this.editor()?.displayName ?? '',
      [Validators.required, Validators.maxLength(160)],
    ],
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  ngOnInit(): void {
    const editorId = this.editor()?.id;
    if (!editorId) {
      this.statisticsLoading.set(false);
      return;
    }

    this.accounting
      .getEditorDetail(editorId)
      .pipe(finalize(() => this.statisticsLoading.set(false)))
      .subscribe({
        next: (detail) => {
          this.statistics.set(detail.personalStatistics);
          this.scoreBreakdown.set(detail.scoresByStatus);
        },
        error: (error: HttpErrorResponse) =>
          this.statisticsError.set(this.errorMessage(error, 'პირადი სტატისტიკა ვერ ჩაიტვირთა.')),
      });
  }

  saveProfile(): void {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid || this.profileSaving()) return;

    this.profileSaving.set(true);
    this.profileError.set(null);
    this.profileMessage.set(null);
    this.editors
      .updateMe(this.profileForm.controls.displayName.value.trim())
      .pipe(finalize(() => this.profileSaving.set(false)))
      .subscribe({
        next: (updated) => {
          this.auth.updateEditor({
            id: updated.id,
            email: updated.email,
            displayName: updated.displayName,
            role: updated.role,
          });
          this.profileForm.controls.displayName.setValue(updated.displayName);
          this.profileForm.markAsPristine();
          this.profileMessage.set('პროფილი განახლებულია.');
        },
        error: (error: HttpErrorResponse) =>
          this.profileError.set(this.errorMessage(error, 'პროფილის განახლება ვერ მოხერხდა.')),
      });
  }

  changePassword(): void {
    this.passwordForm.markAllAsTouched();
    const value = this.passwordForm.getRawValue();
    this.passwordError.set(null);
    this.passwordMessage.set(null);

    if (this.passwordForm.invalid) return;
    if (value.newPassword !== value.confirmPassword) {
      this.passwordError.set('ახალი პაროლი და გამეორება ერთმანეთს არ ემთხვევა.');
      return;
    }
    if (this.passwordSaving()) return;

    this.passwordSaving.set(true);
    this.editors
      .changeOwnPassword(value.currentPassword, value.newPassword)
      .pipe(finalize(() => this.passwordSaving.set(false)))
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.passwordMessage.set('პაროლი წარმატებით შეიცვალა.');
        },
        error: (error: HttpErrorResponse) =>
          this.passwordError.set(this.errorMessage(error, 'პაროლის შეცვლა ვერ მოხერხდა.')),
      });
  }

  scoreCount(scores: Record<string, number>, status: string): number {
    return scores[status] ?? scores[this.toSnakeCase(status)] ?? 0;
  }

  statusScoreTotal(status: string): number {
    const scores = this.scoreBreakdown();
    if (!scores) return 0;
    return (
      this.scoreCount(scores.main, status) +
      this.scoreCount(scores.additional, status) +
      this.scoreCount(scores.kaEditor, status)
    );
  }

  private toSnakeCase(value: string): string {
    return value.replace(/[A-Z]/g, (letter, index) => (index ? '_' : '') + letter.toLowerCase());
  }

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    return error.error?.detail ?? error.error?.message ?? fallback;
  }
}

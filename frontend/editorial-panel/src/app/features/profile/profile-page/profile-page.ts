import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { editorRoleLabels } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {
  private readonly auth = inject(AuthService);
  private readonly editors = inject(EditorsService);
  private readonly fb = inject(FormBuilder);

  readonly editor = this.auth.editor;
  readonly roleLabels = editorRoleLabels;
  readonly profileSaving = signal(false);
  readonly passwordSaving = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly profileError = signal<string | null>(null);
  readonly passwordMessage = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);

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

  private errorMessage(error: HttpErrorResponse, fallback: string): string {
    return error.error?.detail ?? error.error?.message ?? fallback;
  }
}

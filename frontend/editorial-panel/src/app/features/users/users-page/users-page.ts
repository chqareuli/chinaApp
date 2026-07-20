import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CreateEditorRequest, EditorListItem } from '../../../core/models/admin.models';
import { EditorRole, editorRoleLabels } from '../../../core/models/auth.models';
import { EditorsService } from '../../../core/services/editors.service';

@Component({
  selector: 'app-users-page',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage implements OnInit {
  private readonly editorsService = inject(EditorsService);
  private readonly fb = inject(FormBuilder);
  readonly editors = signal<EditorListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly createOpen = signal(false);
  readonly saving = signal(false);
  readonly actionId = signal<number | null>(null);
  readonly resetEditorId = signal<number | null>(null);
  readonly resetPassword = signal('');
  readonly roles: EditorRole[] = [
    'SuperAdmin',
    'ChiefEditor',
    'ZhEditor',
    'KaEditor',
    'AssistantEditor',
  ];
  readonly roleLabels = editorRoleLabels;
  readonly createForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', Validators.required],
    role: ['AssistantEditor' as EditorRole, Validators.required],
    initialPassword: ['', [Validators.required, Validators.minLength(8)]],
  });
  ngOnInit(): void {
    this.load();
  }
  create(): void {
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.editorsService
      .create(this.createForm.getRawValue() as CreateEditorRequest)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (editor) => {
          this.editors.update((items) =>
            [...items, editor].sort((a, b) => a.displayName.localeCompare(b.displayName)),
          );
          this.createForm.reset({
            email: '',
            displayName: '',
            role: 'AssistantEditor',
            initialPassword: '',
          });
          this.createOpen.set(false);
        },
        error: (e: HttpErrorResponse) =>
          this.error.set(this.message(e, 'მომხმარებლის შექმნა ვერ მოხერხდა.')),
      });
  }
  changeRole(editor: EditorListItem, event: Event): void {
    const role = (event.target as HTMLSelectElement).value as EditorRole;
    if (role === editor.role) return;
    this.actionId.set(editor.id);
    this.editorsService
      .changeRole(editor.id, role)
      .pipe(finalize(() => this.actionId.set(null)))
      .subscribe({
        next: (updated) => this.replace(updated),
        error: (e: HttpErrorResponse) => {
          (event.target as HTMLSelectElement).value = editor.role;
          this.error.set(this.message(e, 'როლის შეცვლა ვერ მოხერხდა.'));
        },
      });
  }
  toggleActive(editor: EditorListItem): void {
    this.actionId.set(editor.id);
    const request = editor.isActive
      ? this.editorsService.deactivate(editor.id)
      : this.editorsService.reactivate(editor.id);
    request
      .pipe(finalize(() => this.actionId.set(null)))
      .subscribe({
        next: () => this.replace({ ...editor, isActive: !editor.isActive }),
        error: (e: HttpErrorResponse) =>
          this.error.set(this.message(e, 'მომხმარებლის სტატუსის შეცვლა ვერ მოხერხდა.')),
      });
  }
  beginReset(editorId: number): void {
    this.resetEditorId.set(editorId);
    this.resetPassword.set('');
  }
  updateResetPassword(event: Event): void {
    this.resetPassword.set((event.target as HTMLInputElement).value);
  }
  submitReset(): void {
    const id = this.resetEditorId(),
      password = this.resetPassword();
    if (!id || password.length < 8) return;
    this.actionId.set(id);
    this.editorsService
      .resetPassword(id, password)
      .pipe(finalize(() => this.actionId.set(null)))
      .subscribe({
        next: () => {
          this.resetEditorId.set(null);
          this.resetPassword.set('');
        },
        error: (e: HttpErrorResponse) =>
          this.error.set(this.message(e, 'პაროლის შეცვლა ვერ მოხერხდა.')),
      });
  }
  private load(): void {
    this.editorsService
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (editors) => this.editors.set(editors),
        error: () => this.error.set('მომხმარებლების ჩატვირთვა ვერ მოხერხდა.'),
      });
  }
  private replace(editor: EditorListItem): void {
    this.editors.update((items) => items.map((item) => (item.id === editor.id ? editor : item)));
  }
  private message(error: HttpErrorResponse, fallback: string): string {
    return error.error?.detail ?? error.error?.message ?? fallback;
  }
}

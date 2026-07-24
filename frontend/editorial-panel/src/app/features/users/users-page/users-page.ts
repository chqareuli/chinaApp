import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CreateEditorRequest,
  EditorListItem,
  UpdateEditorRequest,
} from '../../../core/models/admin.models';
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
  readonly editingId = signal<number | null>(null);
  readonly editingSaving = signal(false);
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
    displayName: ['', [Validators.required, Validators.pattern(/\S/)]],
    role: ['AssistantEditor' as EditorRole, Validators.required],
    initialPassword: ['', [Validators.required, Validators.minLength(8)]],
  });
  readonly editForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required, Validators.pattern(/\S/)]],
    role: ['AssistantEditor' as EditorRole, Validators.required],
    isActive: [true],
    newPassword: ['', Validators.minLength(8)],
  });
  ngOnInit(): void {
    this.load();
  }
  toggleCreate(): void {
    if (this.createOpen()) {
      this.createOpen.set(false);
      return;
    }

    this.error.set(null);
    this.editingId.set(null);
    this.createForm.reset({
      email: '',
      displayName: '',
      role: 'AssistantEditor',
      initialPassword: '',
    });
    this.createOpen.set(true);
  }
  create(): void {
    const currentValue = this.createForm.getRawValue();
    this.createForm.patchValue({
      email: currentValue.email.trim(),
      displayName: currentValue.displayName.trim(),
    });
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid) {
      this.error.set('შეავსეთ ყველა ველი სწორად. პაროლი მინიმუმ 8 სიმბოლოს უნდა შეიცავდეს.');
      return;
    }
    if (this.saving()) return;

    const rawValue = this.createForm.getRawValue();
    const request: CreateEditorRequest = {
      ...rawValue,
      email: rawValue.email.trim(),
      displayName: rawValue.displayName.trim(),
    };

    this.saving.set(true);
    this.error.set(null);
    this.editorsService
      .create(request)
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
        error: (e: HttpErrorResponse) => this.error.set(this.createMessage(e)),
      });
  }

  beginEdit(editor: EditorListItem): void {
    this.error.set(null);
    this.createOpen.set(false);
    this.editingId.set(editor.id);
    this.editForm.reset({
      email: editor.email,
      displayName: editor.displayName,
      role: editor.role,
      isActive: editor.isActive,
      newPassword: '',
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editForm.reset({
      email: '',
      displayName: '',
      role: 'AssistantEditor',
      isActive: true,
      newPassword: '',
    });
  }

  saveEdit(editor: EditorListItem): void {
    const currentValue = this.editForm.getRawValue();
    this.editForm.patchValue({
      email: currentValue.email.trim(),
      displayName: currentValue.displayName.trim(),
    });
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid) {
      this.error.set(
        'შეავსეთ სახელი და ელფოსტა სწორად. ახალი პაროლი, თუ შეგყავთ, მინიმუმ 8 სიმბოლო უნდა იყოს.',
      );
      return;
    }
    if (this.editingSaving()) return;

    const rawValue = this.editForm.getRawValue();
    const request: UpdateEditorRequest = {
      email: rawValue.email.trim(),
      displayName: rawValue.displayName.trim(),
      role: rawValue.role,
      isActive: rawValue.isActive,
      newPassword: rawValue.newPassword || null,
    };

    this.editingSaving.set(true);
    this.error.set(null);
    this.editorsService
      .update(editor.id, request)
      .pipe(finalize(() => this.editingSaving.set(false)))
      .subscribe({
        next: (updated) => {
          this.replace(updated);
          this.cancelEdit();
        },
        error: (e: HttpErrorResponse) => this.error.set(this.updateMessage(e)),
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
  private createMessage(error: HttpErrorResponse): string {
    if (error.status === 409) {
      return 'ამ ელფოსტით მომხმარებელი უკვე არსებობს.';
    }
    if (error.status === 400) {
      return 'შეამოწმეთ სახელი, ელფოსტა, როლი და პაროლი.';
    }
    return this.message(error, 'მომხმარებლის შექმნა ვერ მოხერხდა.');
  }
  private updateMessage(error: HttpErrorResponse): string {
    if (error.status === 409) {
      return 'ამ ელფოსტით სხვა მომხმარებელი უკვე არსებობს ან ბოლო სუპერ ადმინის შეცვლა შეუძლებელია.';
    }
    if (error.status === 400) {
      return this.message(error, 'შეამოწმეთ სახელი, ელფოსტა, როლი, სტატუსი და პაროლი.');
    }
    return this.message(error, 'მომხმარებლის მონაცემების შეცვლა ვერ მოხერხდა.');
  }
  private message(error: HttpErrorResponse, fallback: string): string {
    return error.error?.detail ?? error.error?.message ?? fallback;
  }
}

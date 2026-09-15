import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { EditorListItem } from '../../../core/models/admin.models';
import { EditorRole, editorRoleCodes } from '../../../core/models/auth.models';
import { EditorsService } from '../../../core/services/editors.service';
import { PanelFontControl } from '../../../shared/panel-font-control';

@Component({
  selector: 'app-users-page',
  imports: [DatePipe, ReactiveFormsModule, PanelFontControl],
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage implements OnInit {
  private readonly service = inject(EditorsService);
  private readonly fb = inject(FormBuilder);
  readonly roleCodes = editorRoleCodes;
  readonly roles: EditorRole[] = [
    'ChiefEditor',
    'DeputyChiefEditor',
    'Editor',
    'KaEditor',
    'AssistantEditor',
  ];
  readonly roleNames: Record<EditorRole, { en: string; zh: string }> = {
    ChiefEditor: { en: 'Editor-in-Chief', zh: '主编' },
    DeputyChiefEditor: { en: 'Deputy Editor', zh: '副主编' },
    Editor: { en: 'Chinese–X Translation Editor', zh: '汉外翻译编辑' },
    KaEditor: { en: 'Georgian Language Editor', zh: '格文编辑' },
    AssistantEditor: { en: 'Assistant Editor', zh: '助理编辑' },
    SuperAdmin: { en: 'Supervisor', zh: '主管' },
  };
  readonly editors = signal<EditorListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly editingSaving = signal(false);
  readonly createOpen = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingEditor = computed(
    () => this.editors().find((e) => e.id === this.editingId()) ?? null,
  );
  readonly roleFilter = signal('');
  readonly statusFilter = signal('');
  readonly supervisorCount = computed(
    () => this.editors().filter((e) => this.isSupervisor(e) && e.isActive).length,
  );
  readonly filteredEditors = computed(() =>
    this.editors().filter(
      (e) =>
        (!this.roleFilter() || this.roleFor(e) === this.roleFilter()) &&
        (!this.statusFilter() || this.stateFor(e) === this.statusFilter()),
    ),
  );
  readonly createForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    chineseName: ['', Validators.maxLength(200)],
    email: ['', [Validators.required, Validators.email]],
    role: ['' as EditorRole | '', Validators.required],
    initialPassword: ['', [Validators.required, Validators.minLength(8)]],
    weChatAccount: ['', Validators.maxLength(200)],
    isSupervisor: [false],
  });
  readonly editForm = this.fb.nonNullable.group({
    role: ['' as EditorRole | '', Validators.required],
    accountStatus: ['active'],
    isSupervisor: [false],
  });
  ngOnInit(): void {
    this.service
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.editors.set(items),
        error: () => this.error.set('Could not load editors. · 无法加载编辑列表。'),
      });
  }
  roleFor(e: EditorListItem): EditorRole {
    return e.editorialRole || e.role;
  }
  isSupervisor(e: EditorListItem): boolean {
    return e.isSupervisor ?? e.role === 'SuperAdmin';
  }
  stateFor(e: EditorListItem): string {
    return e.isCancelled ? 'cancelled' : e.isActive ? 'active' : 'blocked';
  }
  toggleCreate(): void {
    this.error.set(null);
    this.editingId.set(null);
    this.createOpen.update((v) => !v);
    this.createForm.reset();
    if (this.supervisorCount() >= 2) this.createForm.controls.isSupervisor.disable();
    else this.createForm.controls.isSupervisor.enable();
  }
  create(): void {
    for (const key of ['firstName', 'lastName', 'chineseName', 'email', 'weChatAccount'] as const)
      this.createForm.controls[key].setValue(this.createForm.controls[key].value.trim());
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid) {
      this.error.set(
        'Complete all required fields. Password: at least 8 characters. · 请填写所有必填项，密码至少8位。',
      );
      return;
    }
    if (this.saving()) return;
    const v = this.createForm.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.service
      .create({ ...v, role: v.role as EditorRole, displayName: `${v.firstName} ${v.lastName}` })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (e) => {
          this.editors.update((items) =>
            [...items, e].sort((a, b) => a.displayName.localeCompare(b.displayName)),
          );
          this.createOpen.set(false);
          this.createForm.reset();
        },
        error: (e) => this.error.set(this.message(e)),
      });
  }
  beginEdit(e: EditorListItem): void {
    this.error.set(null);
    this.createOpen.set(false);
    this.editingId.set(e.id);
    this.editForm.reset({
      role: this.roleFor(e),
      accountStatus: this.stateFor(e),
      isSupervisor: this.isSupervisor(e),
    });
    if (this.supervisorCount() >= 2 && !this.isSupervisor(e))
      this.editForm.controls.isSupervisor.disable();
    else this.editForm.controls.isSupervisor.enable();
  }
  cancelEdit(): void {
    this.editingId.set(null);
    this.editForm.reset();
    this.error.set(null);
  }
  saveEdit(e: EditorListItem): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid || this.editingSaving()) return;
    const v = this.editForm.getRawValue();
    this.editingSaving.set(true);
    this.error.set(null);
    this.service
      .update(e.id, {
        email: e.email,
        displayName: e.displayName,
        role: v.role as EditorRole,
        isActive: v.accountStatus === 'active',
        isCancelled: v.accountStatus === 'cancelled',
        isSupervisor: v.isSupervisor,
        newPassword: null,
      })
      .pipe(finalize(() => this.editingSaving.set(false)))
      .subscribe({
        next: (updated) => {
          this.editors.update((items) =>
            items.map((item) => (item.id === updated.id ? updated : item)),
          );
          this.cancelEdit();
        },
        error: (error) => this.error.set(this.message(error)),
      });
  }
  private message(error: HttpErrorResponse): string {
    if (error.status === 409)
      return 'Email already exists, or Supervisor limits would be exceeded. · 邮箱已存在，或主管人数不符合要求。';
    if (error.status === 400) return 'Check the entered information. · 请检查填写的信息。';
    return 'Could not save editor. Please retry. · 无法保存编辑，请重试。';
  }
}

import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { PanelFontControl } from '../../../shared/panel-font-control';
import { editorRoleCodes } from '../../../core/models/auth.models';
import { EditorListItem, EditorAccountingDetail } from '../../../core/models/admin.models';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';
import { AccountingService } from '../../../core/services/accounting.service';
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, RouterLink, PanelFontControl],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly editors = inject(EditorsService);
  private readonly accounting = inject(AccountingService);
  private readonly fb = inject(FormBuilder);
  readonly editor = signal<EditorListItem | null>(null);
  readonly roleCodes = editorRoleCodes;
  readonly detail = signal<EditorAccountingDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profileSaving = signal(false);
  readonly passwordSaving = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly profileError = signal<string | null>(null);
  readonly passwordMessage = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);
  readonly stages = [
    { key: 'NewEntry', label: 'New Entry · 新建', color: 'cge-gray' },
    { key: 'ZhReview', label: 'CHT-review · 中文审核', color: 'cge-red' },
    { key: 'KaReview', label: 'GEL-review · 格鲁吉亚语审核', color: 'cge-blue' },
    { key: 'Ready', label: 'Ready · 待发布', color: 'cge-green' },
    { key: 'Published', label: 'Published · 已发布', color: 'cge-neutral' },
  ];
  readonly profileForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    chineseName: ['', Validators.maxLength(200)],
    email: ['', [Validators.required, Validators.email]],
    weChatAccount: ['', Validators.maxLength(200)],
  });
  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(1024)]],
    confirmPassword: ['', Validators.required],
  });
  ngOnInit(): void {
    this.editors.getMe().subscribe({
      next: (e) => {
        this.editor.set(e);
        this.profileForm.reset({
          firstName: e.firstName ?? e.displayName.split(' ')[0],
          lastName: e.lastName ?? e.displayName.split(' ').slice(1).join(' '),
          chineseName: e.chineseName ?? '',
          email: e.email,
          weChatAccount: e.weChatAccount ?? '',
        });
      },
      error: () => this.profileError.set('Could not load profile. · 无法加载个人信息。'),
    });
    const id = this.auth.editor()?.id;
    if (!id) {
      this.loading.set(false);
      return;
    }
    this.accounting
      .getEditorDetail(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (d) => this.detail.set(d),
        error: () => this.error.set('Could not load work statistics. · 无法加载工作统计。'),
      });
  }
  count(kind: string, status: string): number {
    const d = this.detail();
    const values =
      kind === 'entries'
        ? d?.entriesByStatus
        : kind === 'main'
          ? d?.scoresByStatus.main
          : d?.scoresByStatus.additional;
    return values?.[status] ?? 0;
  }
  total(kind: string): number {
    if (kind !== 'entries') {
      const scores =
        kind === 'main'
          ? this.detail()?.scoresByStatus.main
          : this.detail()?.scoresByStatus.additional;
      return Object.values(scores ?? {}).reduce((sum, value) => sum + value, 0);
    }
    return this.stages.reduce((sum, s) => sum + this.count(kind, s.key), 0);
  }
  workParams(kind: string, status?: string) {
    return { workEditorId: this.editor()?.id, scoreKind: kind, status: status ?? null };
  }
  saveProfile(): void {
    for (const key of ['firstName', 'lastName', 'chineseName', 'email', 'weChatAccount'] as const)
      this.profileForm.controls[key].setValue(this.profileForm.controls[key].value.trim());
    this.profileForm.markAllAsTouched();
    this.profileError.set(null);
    this.profileMessage.set(null);
    if (this.profileForm.invalid) {
      this.profileError.set(
        'Complete the required names and valid email. · 请填写姓名和有效邮箱。',
      );
      return;
    }
    if (this.profileSaving()) return;
    const v = this.profileForm.getRawValue();
    this.profileSaving.set(true);
    this.editors
      .updateMe(`${v.firstName} ${v.lastName}`, v)
      .pipe(finalize(() => this.profileSaving.set(false)))
      .subscribe({
        next: (e) => {
          this.editor.set(e);
          this.auth.updateEditor({
            id: e.id,
            email: e.email,
            displayName: e.displayName,
            role: e.role,
          });
          this.profileForm.markAsPristine();
          this.profileMessage.set('Saved · 已保存');
        },
        error: (e) =>
          this.profileError.set(
            e.status === 409
              ? 'Email already exists · 邮箱已存在'
              : 'Could not save profile · 无法保存个人信息',
          ),
      });
  }
  changePassword(): void {
    this.passwordForm.markAllAsTouched();
    this.passwordError.set(null);
    this.passwordMessage.set(null);
    const v = this.passwordForm.getRawValue();
    if (this.passwordForm.invalid) {
      this.passwordError.set(
        'Complete all fields; use at least 8 characters. · 请填写所有字段，密码至少8位。',
      );
      return;
    }
    if (v.newPassword !== v.confirmPassword) {
      this.passwordError.set('Passwords do not match · 两次密码不一致');
      return;
    }
    if (this.passwordSaving()) return;
    this.passwordSaving.set(true);
    this.editors
      .changeOwnPassword(v.currentPassword, v.newPassword)
      .pipe(finalize(() => this.passwordSaving.set(false)))
      .subscribe({
        next: () => {
          this.passwordForm.reset();
          this.passwordMessage.set('Saved · 已保存');
        },
        error: () =>
          this.passwordError.set(
            'Could not change password. Check the current password. · 无法修改密码，请检查当前密码。',
          ),
      });
  }
}

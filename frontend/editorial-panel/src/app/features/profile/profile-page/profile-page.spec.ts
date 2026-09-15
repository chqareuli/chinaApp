import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { beforeEach, describe, it, expect, vi } from 'vitest';
import { ProfilePage } from './profile-page';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';
import { AccountingService } from '../../../core/services/accounting.service';
describe('Profile forms and work links', () => {
  const editor = {
    id: 1,
    displayName: 'First Last',
    firstName: 'First',
    lastName: 'Last',
    email: 'old@example.test',
    role: 'Editor' as const,
    isActive: true,
    createdAtUtc: '2026-01-01',
  };
  let service: {
    getMe: ReturnType<typeof vi.fn>;
    updateMe: ReturnType<typeof vi.fn>;
    changeOwnPassword: ReturnType<typeof vi.fn>;
  };
  beforeEach(() => {
    service = {
      getMe: vi.fn(() => of(editor)),
      updateMe: vi.fn(() => of({ ...editor, email: 'new@example.test' })),
      changeOwnPassword: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { editor: signal(editor), updateEditor: vi.fn() } },
        { provide: EditorsService, useValue: service },
        {
          provide: AccountingService,
          useValue: {
            getEditorDetail: () =>
              of({
                entriesByStatus: { NewEntry: 2, Ready: 3 },
                scoresByStatus: { main: { Ready: 1 }, additional: { Ready: 2 } },
              }),
          },
        },
      ],
    });
  });
  it('loads and saves all five editable fields', () => {
    const p = TestBed.createComponent(ProfilePage).componentInstance;
    p.ngOnInit();
    p.profileForm.patchValue({
      email: ' new@example.test ',
      chineseName: '中文',
      weChatAccount: 'chat',
    });
    p.saveProfile();
    expect(service.updateMe).toHaveBeenCalledWith('First Last', {
      firstName: 'First',
      lastName: 'Last',
      email: 'new@example.test',
      chineseName: '中文',
      weChatAccount: 'chat',
    });
    expect(p.editor()?.email).toBe('new@example.test');
  });
  it('rejects blank names and mismatching passwords without mutations', () => {
    const p = TestBed.createComponent(ProfilePage).componentInstance;
    p.ngOnInit();
    p.profileForm.patchValue({ firstName: '  ' });
    p.saveProfile();
    expect(service.updateMe).not.toHaveBeenCalled();
    p.passwordForm.setValue({
      currentPassword: 'current',
      newPassword: '12345678',
      confirmPassword: 'different',
    });
    p.changePassword();
    expect(service.changeOwnPassword).not.toHaveBeenCalled();
    expect(p.passwordError()).toContain('Passwords do not match');
  });
  it('keeps entry counts separate from score counts and includes drafts in Total', () => {
    const p = TestBed.createComponent(ProfilePage).componentInstance;
    p.ngOnInit();
    expect(p.total('entries')).toBe(5);
    expect(p.total('main')).toBe(1);
    expect(p.total('additional')).toBe(2);
    expect(p.workParams('main', 'Ready')).toEqual({
      workEditorId: 1,
      scoreKind: 'main',
      status: 'Ready',
    });
  });
});

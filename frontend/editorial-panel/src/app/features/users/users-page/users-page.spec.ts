import { provideRouter } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AccountingService } from '../../../core/services/accounting.service';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import {
  CreateEditorRequest,
  EditorListItem,
  UpdateEditorRequest,
} from '../../../core/models/admin.models';
import { EditorsService } from '../../../core/services/editors.service';
import { UsersPage } from './users-page';
const editor: EditorListItem = {
  id: 4,
  email: 'editor@example.test',
  displayName: 'Test Editor',
  role: 'Editor',
  editorialRole: 'Editor',
  isActive: true,
  createdAtUtc: '2026-08-22T00:00:00Z',
  chineseName: '编辑',
};
class ServiceStub {
  creates: CreateEditorRequest[] = [];
  updates: UpdateEditorRequest[] = [];
  resets: { id: number; password: string }[] = [];
  list() {
    return of([] as EditorListItem[]);
  }
  create(request: CreateEditorRequest) {
    this.creates.push(request);
    return of({ ...editor, ...request, id: 7 });
  }
  update(id: number, request: UpdateEditorRequest) {
    this.updates.push(request);
    return of({ ...editor, ...request, id });
  }
  resetPassword(id: number, password: string) {
    this.resets.push({ id, password });
    return of(undefined);
  }
}
describe('UsersPage reference design', () => {
  let fixture: ComponentFixture<UsersPage>;
  let page: UsersPage;
  let service: ServiceStub;
  beforeEach(async () => {
    service = new ServiceStub();
    await TestBed.configureTestingModule({
      imports: [UsersPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { editor: () => ({ role: 'SuperAdmin' }) } },
        { provide: AccountingService, useValue: { getEditorDetail: () => of(null) } },
        { provide: EditorsService, useValue: service },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(UsersPage);
    page = fixture.componentInstance;
    fixture.detectChanges();
  });
  it('uses all seven reference fields and English–Chinese labels', () => {
    page.toggleCreate();
    fixture.detectChanges();
    const form = fixture.nativeElement.querySelector('form');
    expect(form.textContent).toContain('First name · 名');
    expect(form.textContent).toContain('WeChat account · 微信账号');
    expect(form.textContent).not.toMatch(/[\u10a0-\u10ff]/);
    expect(form.querySelector('input[formControlName="email"]').autocomplete).toBe('off');
    expect(form.querySelector('input[formControlName="initialPassword"]').autocomplete).toBe(
      'new-password',
    );
  });
  it('renders a colleague work summary and totals from status counts', () => {
    page.openProfile(editor);
    page.work.set({
      editorId: editor.id,
      displayName: editor.displayName,
      email: editor.email,
      role: editor.role,
      createdAtUtc: editor.createdAtUtc,
      entriesByStatus: { Ready: 2, Published: 1 },
      scoresByStatus: { main: { Ready: 2 }, additional: { Published: 1 }, kaEditor: {} },
    } as import('../../../core/models/admin.models').EditorAccountingDetail);
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('[role="dialog"] tbody tr');
    expect(rows).toHaveLength(7);
    expect(
      Array.from(rows[6].querySelectorAll('td'), (cell: any) => cell.textContent.trim()),
    ).toEqual(['Total · 总计', '3', '2', '1']);
  });
  it('rejects incomplete add forms with bilingual feedback', () => {
    page.toggleCreate();
    page.create();
    expect(service.creates).toHaveLength(0);
    expect(page.error()).toContain('Complete all required fields');
  });
  it('persists names, WeChat and the independent Supervisor selection', () => {
    page.toggleCreate();
    page.createForm.setValue({
      firstName: ' Test ',
      lastName: ' Editor ',
      chineseName: ' 编辑 ',
      email: ' test@example.test ',
      role: 'ChiefEditor',
      initialPassword: 'Example123!',
      weChatAccount: ' test-chat ',
      isSupervisor: true,
    });
    page.create();
    expect(service.creates[0]).toEqual({
      firstName: 'Test',
      lastName: 'Editor',
      chineseName: '编辑',
      displayName: 'Test Editor',
      email: 'test@example.test',
      role: 'ChiefEditor',
      initialPassword: 'Example123!',
      weChatAccount: 'test-chat',
      isSupervisor: true,
    });
    expect(page.createOpen()).toBe(false);
  });
  it('places edit and password reset in separate forms below the table', () => {
    page.editors.set([editor]);
    page.beginEdit(editor);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('table form')).toBeNull();
    const form = fixture.nativeElement.querySelector('form');
    expect(form.textContent).toContain('Save · 保存');
    expect(form.querySelectorAll('select')).toHaveLength(2);
    expect(form.querySelector('input[type="password"]')).toBeNull();
    const passwordForm = fixture.nativeElement.querySelector('input[type="password"]').form;
    expect(passwordForm).not.toBe(form);
    expect(passwordForm.querySelector('button[type="submit"]').disabled).toBe(true);
    page.newPassword.setValue('Example123!');
    fixture.detectChanges();
    passwordForm.dispatchEvent(new Event('submit', { cancelable: true }));
    expect(service.resets).toEqual([{ id: editor.id, password: 'Example123!' }]);
    expect(service.updates).toHaveLength(0);
    expect(page.newPassword.value).toBe('');
    expect(page.passwordResetFeedback()?.success).toBe(true);
  });
  it('updates status and role without overwriting profile or password', () => {
    page.editors.set([editor]);
    page.beginEdit(editor);
    page.editForm.setValue({ role: 'KaEditor', accountStatus: 'cancelled', isSupervisor: false });
    page.saveEdit(editor);
    expect(service.updates[0]).toEqual({
      email: editor.email,
      displayName: editor.displayName,
      role: 'KaEditor',
      isActive: false,
      isCancelled: true,
      isSupervisor: false,
      newPassword: null,
    });
  });
  it('disables a third active Supervisor selection', () => {
    page.editors.set([
      { ...editor, id: 1, role: 'SuperAdmin' },
      { ...editor, id: 2, role: 'SuperAdmin' },
    ]);
    page.toggleCreate();
    expect(page.supervisorCount()).toBe(2);
    expect(page.createForm.controls.isSupervisor.disabled).toBe(true);
  });
});

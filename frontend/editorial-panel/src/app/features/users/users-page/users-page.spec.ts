import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import {
  CreateEditorRequest,
  EditorListItem,
  UpdateEditorRequest,
} from '../../../core/models/admin.models';
import { EditorsService } from '../../../core/services/editors.service';
import { UsersPage } from './users-page';

const existingEditor: EditorListItem = {
  id: 4,
  email: 'editor@chinese.ge',
  displayName: 'რედაქტორი',
  role: 'ZhEditor',
  isActive: true,
  createdAtUtc: '2026-07-24T00:00:00Z',
};

class EditorsServiceStub {
  readonly createRequests: CreateEditorRequest[] = [];
  readonly updateRequests: Array<{ id: number; request: UpdateEditorRequest }> = [];

  list() {
    return of([] as EditorListItem[]);
  }

  create(request: CreateEditorRequest) {
    this.createRequests.push(request);
    return of({
      id: 7,
      email: request.email,
      displayName: request.displayName,
      role: request.role,
      isActive: true,
      createdAtUtc: '2026-07-24T00:00:00Z',
    } satisfies EditorListItem);
  }

  update(id: number, request: UpdateEditorRequest) {
    this.updateRequests.push({ id, request });
    return of({
      ...existingEditor,
      id,
      email: request.email,
      displayName: request.displayName,
      role: request.role,
      isActive: request.isActive,
    } satisfies EditorListItem);
  }
}

describe('UsersPage', () => {
  let fixture: ComponentFixture<UsersPage>;
  let component: UsersPage;
  let editorsService: EditorsServiceStub;

  beforeEach(async () => {
    editorsService = new EditorsServiceStub();
    await TestBed.configureTestingModule({
      imports: [UsersPage],
      providers: [{ provide: EditorsService, useValue: editorsService }],
    }).compileComponents();

    fixture = TestBed.createComponent(UsersPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('prevents saved login credentials from autofilling the create form', () => {
    component.toggleCreate();
    fixture.detectChanges();

    const email = fixture.nativeElement.querySelector(
      'input[formControlName="email"]',
    ) as HTMLInputElement;
    const password = fixture.nativeElement.querySelector(
      'input[formControlName="initialPassword"]',
    ) as HTMLInputElement;

    expect(email.autocomplete).toBe('off');
    expect(password.autocomplete).toBe('new-password');
    expect(email.value).toBe('');
    expect(password.value).toBe('');
  });

  it('shows validation feedback instead of silently ignoring an invalid form', () => {
    component.toggleCreate();
    component.create();

    expect(editorsService.createRequests).toHaveLength(0);
    expect(component.error()).toContain('შეავსეთ ყველა ველი სწორად');
  });

  it('trims and submits a valid new editor', () => {
    component.toggleCreate();
    component.createForm.setValue({
      displayName: '  ახალი რედაქტორი  ',
      email: '  new.editor@chinese.ge  ',
      role: 'AssistantEditor',
      initialPassword: 'TestPass1!',
    });

    component.create();

    expect(editorsService.createRequests).toEqual([
      {
        displayName: 'ახალი რედაქტორი',
        email: 'new.editor@chinese.ge',
        role: 'AssistantEditor',
        initialPassword: 'TestPass1!',
      },
    ]);
    expect(component.editors()).toHaveLength(1);
    expect(component.createOpen()).toBe(false);
  });

  it('shows the assigned role as text until edit mode is opened', () => {
    component.editors.set([existingEditor]);
    fixture.detectChanges();

    const roleText = fixture.nativeElement.querySelector('.role-text') as HTMLElement;

    expect(roleText.textContent?.trim()).toBe('ჩინურის რედაქტორი');
    expect(fixture.nativeElement.querySelector('.users-table article > select')).toBeNull();
  });

  it('opens a single edit form with current data and an empty password field', () => {
    component.editors.set([existingEditor]);
    component.beginEdit(existingEditor);
    fixture.detectChanges();

    expect(component.editingId()).toBe(existingEditor.id);
    expect(component.editForm.getRawValue()).toEqual({
      displayName: existingEditor.displayName,
      email: existingEditor.email,
      role: existingEditor.role,
      isActive: true,
      newPassword: '',
    });

    const password = fixture.nativeElement.querySelector(
      'input[formControlName="newPassword"]',
    ) as HTMLInputElement;
    expect(password.autocomplete).toBe('new-password');
    expect(password.value).toBe('');
  });

  it('updates name, email, role, status and an optional new password together', () => {
    component.editors.set([existingEditor]);
    component.beginEdit(existingEditor);
    component.editForm.setValue({
      displayName: '  განახლებული რედაქტორი  ',
      email: '  updated@chinese.ge  ',
      role: 'KaEditor',
      isActive: false,
      newPassword: 'NewPass1!',
    });

    component.saveEdit(existingEditor);

    expect(editorsService.updateRequests).toEqual([
      {
        id: existingEditor.id,
        request: {
          displayName: 'განახლებული რედაქტორი',
          email: 'updated@chinese.ge',
          role: 'KaEditor',
          isActive: false,
          newPassword: 'NewPass1!',
        },
      },
    ]);
    expect(component.editors()[0]).toEqual(
      expect.objectContaining({
        displayName: 'განახლებული რედაქტორი',
        email: 'updated@chinese.ge',
        role: 'KaEditor',
        isActive: false,
      }),
    );
    expect(component.editingId()).toBeNull();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CreateEditorRequest, EditorListItem } from '../../../core/models/admin.models';
import { EditorsService } from '../../../core/services/editors.service';
import { UsersPage } from './users-page';

class EditorsServiceStub {
  readonly createRequests: CreateEditorRequest[] = [];

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
});

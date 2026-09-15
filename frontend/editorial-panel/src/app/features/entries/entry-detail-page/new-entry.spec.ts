import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { EntryDetail } from '../../../core/models/entry.models';
import { EntriesService } from '../../../core/services/entries.service';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { MediaService } from '../../../core/services/media.service';
import { EntryDetailPage } from './entry-detail-page';

describe('New entry in the full editor', () => {
  let component: EntryDetailPage;
  const navigate = vi.fn().mockResolvedValue(true);
  const create = vi.fn();
  const saveContent = vi.fn();
  let persisted: EntryDetail;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: ActivatedRoute, useValue: { snapshot: { data: { newEntry: true } } } },
        { provide: Router, useValue: { navigate } },
        {
          provide: EntriesService,
          useValue: {
            create,
            saveContent,
            getVocabularies: () => of([]),
            getHistory: () => of([]),
          },
        },
        {
          provide: AuthService,
          useValue: { editor: () => ({ role: 'Editor', displayName: 'Editor' }) },
        },
        { provide: CollaborationService, useValue: {} },
        { provide: MediaService, useValue: {} },
      ],
    });
    component = TestBed.runInInjectionContext(() => new EntryDetailPage());
    component.ngOnInit();
    persisted = { ...component.entry()!, id: 24, rowVersion: 3 };
    create.mockReturnValue(of(persisted));
    saveContent.mockImplementation((_id, body) => of({ ...persisted, ...body, rowVersion: 4 }));
  });

  it('does not create a server entry merely by opening the editor', () => {
    expect(component.isNew()).toBe(true);
    expect(component.loading()).toBe(false);
    expect(create).not.toHaveBeenCalled();
  });

  it('saves structure entered before the first save using the created row version', () => {
    component.startEditing();
    component.entryForm.controls.lemma.setValue('测试');
    component.startStructure('sense');
    component.rootSenses.at(0).get('definition')!.setValue('ტესტი');
    component.saveContent();
    expect(create).toHaveBeenCalledOnce();
    expect(saveContent).toHaveBeenCalledWith(
      24,
      expect.objectContaining({
        lemma: '测试',
        rowVersion: 3,
        senses: [expect.objectContaining({ definition: 'ტესტი' })],
      }),
    );
    expect(navigate).toHaveBeenCalledWith(['/adminpanel/entries', 24], { replaceUrl: true });
  });

  it('retains edits and retries the same draft if saving content fails after creation', () => {
    component.startEditing();
    component.entryForm.controls.lemma.setValue('测试');
    saveContent.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 500 })));
    component.saveContent();
    expect(component.editing()).toBe(true);
    expect(component.entry()?.id).toBe(24);
    expect(component.entryForm.controls.lemma.value).toBe('测试');
    expect(navigate).not.toHaveBeenCalled();
    component.saveContent();
    expect(create).toHaveBeenCalledOnce();
    expect(saveContent).toHaveBeenCalledTimes(2);
    expect(component.saveSuccess()).toBe(true);
    expect(component.editing()).toBe(false);
  });
});

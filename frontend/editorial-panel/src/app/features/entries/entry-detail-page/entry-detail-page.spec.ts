import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { EntryDetail } from '../../../core/models/entry.models';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { EntriesService } from '../../../core/services/entries.service';
import { MediaService } from '../../../core/services/media.service';
import { EntryDetailPage } from './entry-detail-page';

const entry: EntryDetail = {
  id: 14,
  lemma: '中国',
  pinyin: 'Zhōngguó',
  pinyins: [{ id: 1, value: 'Zhōngguó', order: 0 }],
  complexForms: [{ id: 2, value: '中國', order: 0 }],
  markers: [],
  crossReferences: [],
  status: 'NewEntry',
  mainAuthorDisplayName: 'რედაქტორი',
  lastEditorDisplayName: 'რედაქტორი',
  updatedAtUtc: '2026-07-24T00:00:00Z',
  createdAtUtc: '2026-07-24T00:00:00Z',
  rowVersion: 1,
  homonyms: [],
  gramGroups: [],
  directPoses: [],
  senses: [],
  capabilities: {
    canEditContent: true,
    allowedStatusTransitions: ['ZhReview'],
  },
};

class EntriesServiceStub {
  readonly statusRequests: Array<{ id: number; targetStatus: string }> = [];

  changeStatus(id: number, targetStatus: string) {
    this.statusRequests.push({ id, targetStatus });
    return of({ ...entry, status: targetStatus });
  }

  getHistory() {
    return of([]);
  }
}

class CollaborationServiceStub {
  readonly deletedComments: number[] = [];
  readonly deletedSupportMaterials: Array<{ entryId: number; id: number }> = [];

  deleteComment(id: number) {
    this.deletedComments.push(id);
    return of(undefined);
  }

  deleteSupportMaterial(entryId: number, id: number) {
    this.deletedSupportMaterials.push({ entryId, id });
    return of(undefined);
  }
}

describe('EntryDetailPage status confirmation', () => {
  let component: EntryDetailPage;
  let entriesService: EntriesServiceStub;
  let collaborationService: CollaborationServiceStub;

  beforeEach(() => {
    entriesService = new EntriesServiceStub();
    collaborationService = new CollaborationServiceStub();
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: EntriesService, useValue: entriesService },
        { provide: CollaborationService, useValue: collaborationService },
        { provide: MediaService, useValue: {} },
        { provide: AuthService, useValue: { editor: () => ({ role: 'SuperAdmin' }) } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => String(entry.id) } } },
        },
      ],
    });

    component = TestBed.runInInjectionContext(() => new EntryDetailPage());
    Object.assign(component, { entryId: entry.id });
    component.entry.set(entry);
  });

  it('does not send a status request before confirmation', () => {
    component.changeStatus('ZhReview');

    expect(component.statusConfirmation()).toBe('ZhReview');
    expect(entriesService.statusRequests).toHaveLength(0);
  });

  it('cancels a pending status change without sending a request', () => {
    component.changeStatus('ZhReview');
    component.cancelStatusChange();

    expect(component.statusConfirmation()).toBeNull();
    expect(entriesService.statusRequests).toHaveLength(0);
  });

  it('sends the status request only after confirmation', () => {
    component.changeStatus('ZhReview');
    component.confirmStatusChange();

    expect(entriesService.statusRequests).toEqual([{ id: entry.id, targetStatus: 'ZhReview' }]);
    expect(component.statusConfirmation()).toBeNull();
    expect(component.entry()?.status).toBe('ZhReview');
  });

  it('starts the full homonym structure with one grammar group and sense', () => {
    component.startStructure('homonym');

    expect(component.structureKind()).toBe('homonym');
    expect(component.homonyms.length).toBe(1);
    expect(component.homonyms.at(0).get('text')?.value).toBe('');
    expect(component.formArrayAt(component.homonyms.at(0), 'gramGroups').length).toBe(1);
    expect(
      component.formArrayAt(component.formArrayAt(component.homonyms.at(0), 'gramGroups').at(0), 'senses').length,
    ).toBe(1);
  });

  it('includes the entered homonym text in the save request', () => {
    component.startStructure('homonym');
    component.homonyms.at(0).get('text')?.setValue('同形词');

    const request = (
      component as unknown as { toSaveRequest: (rowVersion: number) => { homonyms: Array<{ text: string }> } }
    ).toSaveRequest(entry.rowVersion);

    expect(request.homonyms[0].text).toBe('同形词');
  });

  it('opens the embedded notes panel on comments by default', () => {
    expect(component.notesTab()).toBe('comments');

    component.setNotesTab('support');

    expect(component.notesTab()).toBe('support');
    expect(component.supportLanguageLabel('Chinese')).toBe('ჩინური');
  });

  it('does not delete a comment until deletion is confirmed', () => {
    component.requestCommentDelete(31);

    expect(component.pendingCommentDeleteId()).toBe(31);
    expect(collaborationService.deletedComments).toEqual([]);

    component.deleteComment(31);

    expect(collaborationService.deletedComments).toEqual([31]);
    expect(component.pendingCommentDeleteId()).toBeNull();
  });

  it('does not delete support material until deletion is confirmed', () => {
    component.requestSupportMaterialDelete(42);

    expect(component.pendingSupportMaterialDeleteId()).toBe(42);
    expect(collaborationService.deletedSupportMaterials).toEqual([]);

    component.deleteSupportMaterial(42);

    expect(collaborationService.deletedSupportMaterials).toEqual([{ entryId: entry.id, id: 42 }]);
    expect(component.pendingSupportMaterialDeleteId()).toBeNull();
  });

  it('starts a direct grammar group structure', () => {
    component.startStructure('grammar');

    expect(component.structureKind()).toBe('grammar');
    expect(component.rootGramGroups.length).toBe(1);
    expect(component.formArrayAt(component.rootGramGroups.at(0), 'senses').length).toBe(1);
  });

  it('keeps entry-level decorations shared across direct grammar groups', () => {
    component.startStructure('grammar');
    component.rootMarkers.push(
      new FormBuilder().group({ id: [null], category: ['Style'], value: ['bookish'] }),
    );
    component.addGroupTo(component.rootGramGroups);

    const request = (
      component as unknown as {
        toSaveRequest: (rowVersion: number) => {
          markers: Array<{ category: string; value: string }>;
          gramGroups: unknown[];
        };
      }
    ).toSaveRequest(entry.rowVersion);

    expect(request.gramGroups).toHaveLength(2);
    expect(request.markers).toEqual([
      { id: null, category: 'Style', value: 'bookish' },
    ]);
  });

  it('starts a direct POS structure with an implicit sense', () => {
    component.startStructure('pos');

    expect(component.structureKind()).toBe('pos');
    expect(component.directPoses.length).toBe(1);
    expect(component.formArrayAt(component.directPoses.at(0), 'senses').length).toBe(1);
  });

  it('starts a direct implicit sense structure', () => {
    component.startStructure('sense');

    expect(component.structureKind()).toBe('sense');
    expect(component.rootSenses.length).toBe(1);
  });
});

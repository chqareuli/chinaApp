import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup, FormControl } from '@angular/forms';
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
  it('saves incomplete new-entry structure with a warning instead of blocking the draft', () => {
    component.startEditing();
    component.entryForm.controls.lemma.setValue('草稿');
    component.startStructure('grammar');
    const save = vi.fn().mockReturnValue(of(entry));
    Object.assign(entriesService, { saveContent: save });
    component.saveContent();
    expect(save).toHaveBeenCalled();
    expect(component.draftWarning()).toContain('Incomplete draft');
  });
  it('collapses reference groups independently and opens a private message for the selected author', () => {
    component.toggleReferenceGroup('g1');
    component.toggleReferenceGroup('g3');
    component.toggleReferenceGroup('g1');
    expect(component.collapsedReferenceGroups()).toEqual(['g3']);
    component.openPrivateMessage({ authorEditorId: 9, authorDisplayName: 'Editor' } as any);
    expect(component.privateMessageRecipient()).toEqual({ id: 9, name: 'Editor' });
    expect(component.privateMessageText()).toBe('');
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

  it('starts exactly two empty homographs and lets the editor choose POS or direct sense', () => {
    component.startStructure('homonym');
    expect(component.homonyms.length).toBe(2);
    const first = component.homonyms.at(0);
    expect(component.formArrayAt(first, 'gramGroups').length).toBe(0);
    component.addSenseTo(component.formArrayAt(first, 'directSenses'));
    expect(component.formArrayAt(first, 'directSenses').length).toBe(1);
    component.addGroupTo(component.formArrayAt(component.homonyms.at(1), 'gramGroups'));
    expect(component.formArrayAt(component.homonyms.at(1), 'gramGroups').length).toBe(1);
    component.removeHomonym(0);
    expect(component.homonyms.length).toBe(2);
    expect(component.pendingHomonymRemoval()).toBe(0);
    component.confirmHomonymRemoval();
    expect(component.homonyms.length).toBe(1);
  });
  it('keeps numbering separate from manually entered homograph suffixes and recalculates it', () => {
    component.startStructure('homonym');
    component.homonyms.at(0).get('text')!.setValue('XINGHUA 1');
    component.homonyms.at(1).get('text')!.setValue('XINGHUA 2');
    expect(component.homographParts(0)).toEqual({ text: 'XINGHUA', number: 1 });
    expect(component.homographParts(1)).toEqual({ text: 'XINGHUA', number: 2 });
    component.removeHomonym(0);
    component.pendingHomonymRemoval.set(null);
    expect(component.homonyms.length).toBe(2);
    component.removeHomonym(0);
    component.confirmHomonymRemoval();
    expect(component.homographParts(0)).toEqual({ text: 'XINGHUA', number: null });
  });
  it('groups late and nested replies with their original topic while new topics remain separate', () => {
    const comment = (id: number, parent: number | null, time: number) =>
      ({ id, replyToCommentId: parent, createdAtUtc: `2026-10-28T10:0${time}:00Z` }) as any;
    component.comments.set([
      comment(1, null, 0),
      comment(2, null, 1),
      comment(3, 1, 2),
      comment(4, 3, 3),
      comment(5, 2, 4),
    ]);
    expect(
      component.commentThreads().map((thread) => thread.comments.map((comment) => comment.id)),
    ).toEqual([
      [1, 3, 4],
      [2, 5],
    ]);
  });
  it('includes the entered homonym text in the save request', () => {
    component.startStructure('homonym');
    component.homonyms.at(0).get('text')?.setValue('同形词');

    const request = (
      component as unknown as {
        toSaveRequest: (rowVersion: number) => { homonyms: Array<{ text: string }> };
      }
    ).toSaveRequest(entry.rowVersion);

    expect(request.homonyms[0].text).toBe('同形词');
  });

  it('cancels unsaved nested changes and returns to read-only', () => {
    expect(component.canEditFields()).toBe(false);
    component.startEditing();
    expect(component.canEditFields()).toBe(true);
    component.entryForm.controls.lemma.setValue('unsaved');
    component.startStructure('homonym');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.cancelEditing();
    expect(component.entryForm.controls.lemma.value).toBe(entry.lemma);
    expect(component.homonyms.length).toBe(entry.homonyms.length);
    expect(component.canEditFields()).toBe(false);
  });

  it('toggles frames without modifying entry content', () => {
    const before = component.entryForm.getRawValue();
    const visible = component.framesVisible();
    component.toggleFrames();
    expect(component.framesVisible()).toBe(!visible);
    expect(component.entryForm.getRawValue()).toEqual(before);
    component.toggleFrames();
  });

  it('limits homographs and prevents mixed direct-sense/POS structures', () => {
    component.startStructure('homonym');
    for (let i = 0; i < 12; i++) component.addHomonym();
    expect(component.homonyms.length).toBe(10);
    const h = component.homonyms.at(0);
    const groups = component.formArrayAt(h, 'gramGroups');
    const senses = component.formArrayAt(h, 'directSenses');
    component.addSenseTo(senses);
    component.addGroupTo(groups);
    expect(groups.length).toBe(0);
    component.removeSenseFrom(senses, 0);
    component.addGroupTo(groups);
    component.addSenseTo(senses);
    expect(senses.length).toBe(0);
    const childSenses = component.formArrayAt(groups.at(0), 'senses');
    component.removeSenseFrom(childSenses, 0);
    expect(childSenses.length).toBe(1);
  });

  it('updates linked homograph labels when pronunciation changes and preserves the selection', () => {
    component.startStructure('homonym');
    component.pinyins.push(
      new FormGroup({
        id: new FormControl(7),
        value: new FormControl('hé'),
        order: new FormControl(0),
      }),
    );
    const node = component.homonyms.at(0);
    component.linkHomographPronunciation(node, '0');
    expect(component.homographLabel(0)).toBe('hé');
    component.pinyins.at(0).get('value')?.setValue('huó');
    expect(component.homographLabel(0)).toBe('huó');
    expect(node.get('pinyinId')?.value).toBe(7);
    expect(component.homographPronunciationIndex(node)).toBe('0');
  });
  it('preserves existing POS and sense controls when adding homographs', () => {
    component.startStructure('grammar');
    const group = component.rootGramGroups.at(0);
    group.get('pos')?.setValue('名');
    component.addHomonym();
    expect(component.homonyms.length).toBe(2);
    expect(component.rootGramGroups.length).toBe(0);
    expect(component.formArrayAt(component.homonyms.at(0), 'gramGroups').at(0)).toBe(group);
  });

  it('preserves the typed image name when selecting an image file', () => {
    component.mediaDisplayName.set('ჩემი სურათი');
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', {
      value: [new File(['image'], 'different-filename.png', { type: 'image/png' })],
    });
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    component.selectMedia({ target: input } as unknown as Event);
    expect(component.mediaDisplayName()).toBe('ჩემი სურათი');
  });

  it('uses the last focused field of each example for the single image button', () => {
    const first = new FormGroup({}),
      second = new FormGroup({});
    const chinese = document.createElement('textarea'),
      georgian = document.createElement('textarea');
    const open = vi.spyOn(component, 'openNodeMedia').mockImplementation(() => {});
    component.openExampleMedia(first, chinese, georgian);
    expect(open).not.toHaveBeenCalled();
    component.rememberExampleMediaField(first, 'zh');
    component.rememberExampleMediaField(second, 'ka');
    component.openExampleMedia(first, chinese, georgian);
    expect(open).toHaveBeenLastCalledWith('example', first, chinese, 'zh');
    component.rememberExampleMediaField(first, 'ka');
    component.openExampleMedia(first, chinese, georgian);
    expect(open).toHaveBeenLastCalledWith('example', first, georgian, 'ka');
  });

  it('uploads an image at the cursor and resolves its saved placement on reload', () => {
    component.startEditing();
    component.startStructure('sense');
    const sense = component.rootSenses.at(0);
    sense.get('id')?.setValue(71);
    const field = document.createElement('textarea');
    field.value = 'before after';
    field.setSelectionRange(7, 7);
    sense.get('definition')?.setValue(field.value);
    component.openNodeMedia('sense', sense, field, 'definition');
    const file = new File(['image'], 'image.png', { type: 'image/png' });
    const asset = {
      id: 19,
      originalFilename: 'image.png',
      placement: component.mediaPlacement(),
      storageUrl: '/media/19/content',
    };
    const upload = vi.fn().mockReturnValue(of(asset));
    Object.assign(TestBed.inject(MediaService), {
      upload,
      getContent: () => of(new Blob(['image'])),
    });
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    component.selectedMedia.set(file);
    component.mediaDisplayName.set('image');
    component.uploadMedia();
    expect(upload).toHaveBeenCalledWith(
      entry.id,
      file,
      asset.placement,
      component.mediaDisplayName(),
    );
    expect(component.nodeMedia('sense', sense)).toEqual([]);
    expect(component.inlineNodeMedia('sense', sense, 'definition')).toEqual([
      { id: 19, offset: 7, url: 'blob:test', alt: 'image.png', label: 'image' },
    ]);
    const reloaded = JSON.parse(JSON.stringify(asset));
    component.media.set([reloaded]);
    sense.get('definition')?.setValue('new before after');
    expect(component.inlineNodeMedia('sense', sense, 'definition')[0].offset).toBe(11);
    expect(component.selectedMedia()).toBeNull();
    expect(component.mediaSaving()).toBe(false);
  });

  it('targets media by stable block id and blocks unsaved-block uploads', () => {
    component.startEditing();
    component.startStructure('sense');
    const sense = component.rootSenses.at(0);
    component.openNodeMedia('sense', sense, document.createElement('textarea'), 'definition');
    component.selectedMedia.set(new File(['x'], 'image.png', { type: 'image/png' }));
    component.uploadMedia();
    expect(component.mediaSaving()).toBe(false);
    sense.get('id')?.setValue(71);
    component.openNodeMedia('sense', sense, document.createElement('textarea'), 'definition');
    expect(JSON.parse(component.mediaPlacement().slice(7)).nodeId).toBe(71);
    component.media.set([
      { id: 1, placement: 'sense:71' },
      { id: 2, placement: 'example:71' },
    ] as any);
    expect(component.nodeMedia('sense', sense).map((x) => x.id)).toEqual([1]);
  });

  it('resizes paired example text within reference limits without editing content', () => {
    const first = document.createElement('textarea');
    const second = document.createElement('textarea');
    first.style.fontSize = '16px';
    second.style.fontSize = '16px';
    first.value = '中文';
    second.value = 'ქართული';
    document.body.append(first, second);
    component.resizeEditorText([first, second], 1);
    expect(first.style.fontSize).toBe('17px');
    expect(second.style.fontSize).toBe('17px');
    component.resizeEditorText([first], 100);
    component.resizeEditorText([second], -100);
    expect(first.style.fontSize).toBe('40px');
    expect(second.style.fontSize).toBe('12px');
    expect(first.value).toBe('中文');
    expect(second.value).toBe('ქართული');
    first.remove();
    second.remove();
  });

  it('blocks a status transition when reference changes are unsaved', () => {
    component.referenceForm.controls.g1.setValue('unsaved');
    component.referenceForm.markAsDirty();
    component.changeStatus('Ready');
    expect(component.statusConfirmation()).toBeNull();
    expect(component.statusError()).toContain('Save');
  });

  it('shows the actual direct-sense and example hierarchy', () => {
    component.startStructure('sense');
    component.addExampleTo(component.formArrayAt(component.rootSenses.at(0), 'examples'));
    expect(component.structureLines()).toEqual(['Entry → ∅ H → ∅ POS → S1 → E (1)']);
  });

  it('opens the embedded notes panel on reference material by default', () => {
    expect(component.notesTab()).toBe('reference');

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

  it('keeps inline elements on the grammar group where they were added', () => {
    component.startStructure('grammar');
    component
      .formArrayAt(component.rootGramGroups.at(0), 'markers')
      .push(new FormBuilder().group({ id: [null], category: ['Style'], value: ['bookish'] }));
    component.addGroupTo(component.rootGramGroups);

    const request = (
      component as unknown as {
        toSaveRequest: (rowVersion: number) => {
          gramGroups: Array<{ markers: Array<{ category: string; value: string }> }>;
        };
      }
    ).toSaveRequest(entry.rowVersion);

    expect(request.gramGroups).toHaveLength(2);
    expect(request.gramGroups[0].markers).toEqual([
      { id: null, category: 'Style', value: 'bookish' },
    ]);
    expect(request.gramGroups[1].markers).toEqual([]);
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

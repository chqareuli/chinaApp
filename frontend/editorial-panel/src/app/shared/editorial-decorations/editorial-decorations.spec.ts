import { TestBed } from '@angular/core/testing';
import { FormArray, FormBuilder, FormGroup } from '@angular/forms';
import { of } from 'rxjs';
import { EntriesService } from '../../core/services/entries.service';
import { EditorialDecorations } from './editorial-decorations';

class EntriesServiceStub {
  readonly queries: string[] = [];

  search(query: string) {
    this.queries.push(query);
    return of([
      {
        id: 4,
        lemma: '中国',
        pinyin: 'Zhōngguó',
        status: 'Published',
        mainAuthorDisplayName: null,
        lastEditorDisplayName: 'Editor',
        updatedAtUtc: '2026-08-03T00:00:00Z',
      },
    ]);
  }
}

describe('EditorialDecorations XR linking', () => {
  let component: EditorialDecorations;
  let entries: EntriesServiceStub;
  const fb = new FormBuilder();
  it('adds controlled Style values without duplicates and limits them to two', () => {
    component.vocabularies = [
      { id: 1, category: 'style', sortOrder: 0, code: 'a', label: 'A' },
      { id: 2, category: 'style', sortOrder: 0, code: 'b', label: 'B' },
      { id: 3, category: 'style', sortOrder: 0, code: 'c', label: 'C' },
    ];
    const choose = (value: string) =>
      component.selectMarker('Style', { target: { value } } as unknown as Event);
    choose('a');
    choose('a');
    choose('b');
    choose('c');
    expect(component.markers.getRawValue().map((m) => m['value'])).toEqual(['a', 'b']);
    component.readonly = true;
    choose('c');
    expect(component.markers.length).toBe(2);
  });

  beforeEach(() => {
    entries = new EntriesServiceStub();
    TestBed.configureTestingModule({
      providers: [{ provide: EntriesService, useValue: entries }],
    });
    component = TestBed.runInInjectionContext(() => new EditorialDecorations());
    component.markers = new FormArray<FormGroup>([]);
    component.crossReferences = new FormArray<FormGroup>([]);
  });

  it('recognizes an exact entry match while the target is typed', async () => {
    component.openXrAdd();
    component.xrDraft.controls.target.setValue('中国');
    component.targetChanged();
    await new Promise((resolve) => setTimeout(resolve, 300));

    expect(entries.queries).toEqual(['中国']);
    expect(component.xrDraft.controls.targetEntryId.value).toBe(4);
    expect(component.xrDraft.controls.targetEntryLemma.value).toBe('中国');
  });

  it('marks a selected target as linked before the article is saved', async () => {
    component.openXrAdd();
    component.xrDraft.controls.target.setValue('中国');
    component.targetChanged();
    await new Promise((resolve) => setTimeout(resolve, 300));
    component.saveXr();

    expect(component.crossReferences.length).toBe(1);
    expect(component.xrLinkState(0)).toBe('linked');
    expect(component.xrLinkText(0)).toContain('中国');
  });

  it('marks a persisted reference without a target entry as unresolved', () => {
    component.crossReferences.push(
      fb.group({
        id: [12],
        type: ['see'],
        label: ['იხ.'],
        target: ['不存在'],
        targetEntryId: [null],
        targetEntryLemma: [null],
        displayText: [''],
        placement: ['Standalone'],
      }),
    );

    expect(component.xrLinkState(0)).toBe('unresolved');
    expect(component.xrLinkText(0)).toBe('ვერ დალინკდა');
  });
});

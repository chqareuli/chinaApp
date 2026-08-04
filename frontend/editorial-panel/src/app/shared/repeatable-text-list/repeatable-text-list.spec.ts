import { FormArray, FormGroup } from '@angular/forms';
import { RepeatableTextList } from './repeatable-text-list';

describe('RepeatableTextList', () => {
  let component: RepeatableTextList;

  beforeEach(() => {
    component = new RepeatableTextList();
    component.items = new FormArray<FormGroup>([]);
  });

  it('adds several compact values from the modal editor', () => {
    component.openAdd();
    expect(component.editorOpen()).toBe(true);

    component.draft.setValue('wèi');
    component.save();
    component.openAdd();
    component.draft.setValue('wéi');
    component.save();

    expect(component.items.getRawValue()).toEqual([
      { id: null, value: 'wèi', order: 0 },
      { id: null, value: 'wéi', order: 1 },
    ]);
    expect(component.editorOpen()).toBe(false);
  });

  it('edits and removes a saved value', () => {
    component.openAdd();
    component.draft.setValue('中國');
    component.save();

    component.openEdit(0);
    component.draft.setValue('中華');
    component.save();
    expect(component.itemValue(0)).toBe('中華');

    component.remove(0);
    expect(component.items.length).toBe(0);
  });

  it('does not add a duplicate value', () => {
    component.openAdd();
    component.draft.setValue('wèi');
    component.save();
    component.openAdd();
    component.draft.setValue('WÈI');
    component.save();

    expect(component.items.length).toBe(1);
    expect(component.duplicateError()).toBe(true);
  });
});

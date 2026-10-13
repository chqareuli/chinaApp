import { FormArray, FormGroup } from '@angular/forms';
import { RepeatableTextList } from './repeatable-text-list';

describe('RepeatableTextList', () => {
  it('blocks extra values at the limit while allowing existing values to be edited', () => {
    const list = new RepeatableTextList();
    list.items = new FormArray<FormGroup>([]);
    list.maxItems = 1;
    list.addInline('hé');
    list.addInline('huó');
    expect(list.items.length).toBe(1);
    list.editInline(0, 'hè');
    expect(list.itemValue(0)).toBe('hè');
  });
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

  it('commits typed pinyin when the entry is saved without a blur event', () => {
    component.queueInline(null, ' cèshì ');
    expect(component.flushInline()).toBe(true);
    expect(component.itemValue(0)).toBe('cèshì');
    expect(component.flushInline()).toBe(true);
    expect(component.items.length).toBe(1);
  });

  it('rejects duplicate inline edits and preserves the saved values', () => {
    component.queueInline(null, 'wèi');
    component.flushInline();
    component.queueInline(null, 'wéi');
    component.flushInline();
    component.queueInline(1, 'WÈI');
    expect(component.flushInline()).toBe(false);
    expect(component.itemValue(1)).toBe('wéi');
    component.queueInline(1, 'wēi');
    expect(component.flushInline()).toBe(true);
    expect(component.itemValue(1)).toBe('wēi');
  });

  it('discards a pending edit when that value is removed', () => {
    component.queueInline(null, 'wèi');
    component.flushInline();
    component.queueInline(0, '');
    component.remove(0);
    expect(component.flushInline()).toBe(true);
    expect(component.items.length).toBe(0);
  });
});

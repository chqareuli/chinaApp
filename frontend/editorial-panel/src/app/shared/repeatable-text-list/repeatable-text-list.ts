import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-repeatable-text-list',
  imports: [ReactiveFormsModule],
  templateUrl: './repeatable-text-list.html',
  styleUrl: './repeatable-text-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RepeatableTextList {
  @Input({ required: true }) items!: FormArray<FormGroup>;
  @Input() label = 'მნიშვნელობა';
  @Input() addLabel = 'დამატება';
  @Input() placeholder = '';
  @Input() dialogTitle = '';
  @Input() inputLang = '';
  @Input() readonly = false;
  @Input() maxLength = 255;
  @Output() readonly contentChanged = new EventEmitter<void>();

  readonly editingIndex = signal<number | null>(null);
  readonly editorOpen = signal(false);
  readonly duplicateError = signal(false);
  readonly draft = new FormControl('', { nonNullable: true });
  private pendingInline: { index: number | null; value: string } | null = null;

  queueInline(index: number | null, value: string): void {
    this.pendingInline = { index, value };
    this.duplicateError.set(false);
    this.contentChanged.emit();
  }

  flushInline(): boolean {
    const pending = this.pendingInline;
    if (!pending) return true;
    if (pending.index === null && !pending.value.trim()) {
      this.pendingInline = null;
      return true;
    }
    if (!pending.value.trim() || pending.value.trim().length > this.maxLength) return false;
    this.editingIndex.set(pending.index);
    this.draft.setValue(pending.value);
    this.save();
    if (this.duplicateError()) return false;
    this.pendingInline = null;
    return true;
  }

  addInline(value: string): void {
    if (!value.trim()) return;
    this.editingIndex.set(null);
    this.draft.setValue(value);
    this.save();
  }

  editInline(index: number, value: string): void {
    this.editingIndex.set(index);
    this.draft.setValue(value);
    this.save();
  }

  openAdd(): void {
    this.editingIndex.set(null);
    this.draft.setValue('');
    this.duplicateError.set(false);
    this.editorOpen.set(true);
  }

  openEdit(index: number): void {
    this.editingIndex.set(index);
    this.draft.setValue(String(this.items.at(index).get('value')?.value ?? ''));
    this.duplicateError.set(false);
    this.editorOpen.set(true);
  }

  cancel(): void {
    this.editorOpen.set(false);
    this.editingIndex.set(null);
    this.duplicateError.set(false);
  }

  stopModalClick(event: Event): void {
    event.stopPropagation();
  }

  save(): void {
    const value = this.draft.value.trim();
    if (!value || value.length > this.maxLength) {
      this.draft.markAsTouched();
      return;
    }

    const editing = this.editingIndex();
    const duplicate = this.items.controls.some(
      (item, index) =>
        index !== editing &&
        String(item.get('value')?.value ?? '')
          .trim()
          .toLocaleLowerCase() === value.toLocaleLowerCase(),
    );
    if (duplicate) {
      this.duplicateError.set(true);
      return;
    }

    if (editing === null) {
      this.items.push(
        new FormGroup({
          id: new FormControl<number | null>(null),
          value: new FormControl(value, {
            nonNullable: true,
            validators: [Validators.required, Validators.maxLength(this.maxLength)],
          }),
          order: new FormControl(this.items.length, { nonNullable: true }),
        }),
      );
    } else {
      this.items.at(editing).get('value')?.setValue(value);
    }

    this.renumber();
    this.contentChanged.emit();
    this.cancel();
  }

  remove(index: number): void {
    if (this.pendingInline?.index === index) this.pendingInline = null;
    else if (this.pendingInline?.index != null && this.pendingInline.index > index)
      this.pendingInline.index--;
    this.items.removeAt(index);
    this.renumber();
    this.contentChanged.emit();
    if (this.editingIndex() === index) this.cancel();
  }

  itemValue(index: number): string {
    return String(this.items.at(index).get('value')?.value ?? '');
  }

  private renumber(): void {
    this.items.controls.forEach((item, index) =>
      item.get('order')?.setValue(index, { emitEvent: false }),
    );
  }
}

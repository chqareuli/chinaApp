import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { EditorialMarker, VocabularyItem } from '../../core/models/entry.models';

type MarkerCategory = EditorialMarker['category'];

@Component({
  selector: 'app-editorial-decorations',
  imports: [ReactiveFormsModule],
  templateUrl: './editorial-decorations.html',
  styleUrl: './editorial-decorations.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorialDecorations {
  private readonly fb = new FormBuilder();

  @Input({ required: true }) markers!: FormArray<FormGroup>;
  @Input({ required: true }) crossReferences!: FormArray<FormGroup>;
  @Input() vocabularies: VocabularyItem[] = [];
  @Input() readonly = false;
  @Output() readonly contentChanged = new EventEmitter<void>();

  readonly markerEditorOpen = signal(false);
  readonly markerEditingIndex = signal<number | null>(null);
  readonly xrEditorOpen = signal(false);
  readonly xrEditingIndex = signal<number | null>(null);

  readonly markerDraft = this.fb.group({
    category: ['Style' as MarkerCategory, Validators.required],
    value: ['', Validators.required],
  });
  readonly xrDraft = this.fb.group({
    type: ['see', Validators.required],
    target: ['', [Validators.required, Validators.maxLength(500)]],
    displayText: ['', Validators.maxLength(500)],
    placement: ['Standalone', Validators.required],
  });

  readonly categories: Array<{ value: MarkerCategory; label: string }> = [
    { value: 'Style', label: 'style' },
    { value: 'Domain', label: 'domain' },
    { value: 'Abbr', label: 'Abbr.' },
    { value: 'Lang', label: 'Lang.' },
  ];
  readonly xrTypes = [
    { value: 'see', label: 'იხ.' },
    { value: 'seeAlso', label: 'ასევე.' },
    { value: 'equivalent', label: 'იგივეა,რაც' },
    { value: 'antonym', label: 'იხ. ანტ.' },
    { value: 'illustration', label: 'იხ. სურ.' },
  ];

  valuesFor(category: MarkerCategory = this.markerDraft.controls.category.value ?? 'Style'): VocabularyItem[] {
    return this.vocabularies.filter((item) => item.category === category.toLocaleLowerCase());
  }

  markerCategoryChanged(): void {
    this.markerDraft.controls.value.setValue(this.valuesFor()[0]?.code ?? '');
  }

  openMarkerAdd(): void {
    this.markerEditingIndex.set(null);
    this.markerDraft.setValue({ category: 'Style', value: this.valuesFor('Style')[0]?.code ?? '' });
    this.markerEditorOpen.set(true);
  }

  openMarkerEdit(index: number): void {
    const item = this.markers.at(index).getRawValue() as EditorialMarker;
    this.markerEditingIndex.set(index);
    this.markerDraft.setValue({ category: item.category, value: item.value });
    this.markerEditorOpen.set(true);
  }

  saveMarker(): void {
    if (this.markerDraft.invalid) return;
    const value = this.markerDraft.getRawValue();
    const editing = this.markerEditingIndex();
    if (editing === null) {
      this.markers.push(this.fb.group({ id: [null as number | null], category: [value.category], value: [value.value] }));
    } else {
      this.markers.at(editing).patchValue(value);
    }
    this.markerEditorOpen.set(false);
    this.contentChanged.emit();
  }

  removeMarker(index: number): void {
    this.markers.removeAt(index);
    this.contentChanged.emit();
  }

  openXrAdd(): void {
    this.xrEditingIndex.set(null);
    this.xrDraft.setValue({ type: 'see', target: '', displayText: '', placement: 'Standalone' });
    this.xrEditorOpen.set(true);
  }

  openXrEdit(index: number): void {
    const item = this.crossReferences.at(index).getRawValue();
    this.xrEditingIndex.set(index);
    this.xrDraft.setValue({
      type: item.type ?? 'see', target: item.target ?? '', displayText: item.displayText ?? '', placement: item.placement ?? 'Standalone',
    });
    this.xrEditorOpen.set(true);
  }

  saveXr(): void {
    if (this.xrDraft.invalid) {
      this.xrDraft.markAllAsTouched();
      return;
    }
    const value = this.xrDraft.getRawValue();
    const label = this.xrTypes.find((item) => item.value === value.type)?.label ?? 'იხ.';
    const editing = this.xrEditingIndex();
    const payload = { ...value, label };
    if (editing === null) {
      this.crossReferences.push(this.fb.group({ id: [null as number | null], ...Object.fromEntries(Object.entries(payload).map(([key, item]) => [key, [item]])) }));
    } else {
      this.crossReferences.at(editing).patchValue(payload);
    }
    this.xrEditorOpen.set(false);
    this.contentChanged.emit();
  }

  removeXr(index: number): void {
    this.crossReferences.removeAt(index);
    this.contentChanged.emit();
  }

  markerText(index: number): string {
    const item = this.markers.at(index).getRawValue();
    return `${this.categories.find((category) => category.value === item.category)?.label ?? item.category}: ${item.value}`;
  }

  xrText(index: number): string {
    const item = this.crossReferences.at(index).getRawValue();
    return `${item.label} ${item.displayText || item.target}`;
  }
}

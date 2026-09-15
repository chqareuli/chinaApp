import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  Output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  of,
  Subject,
  switchMap,
} from 'rxjs';
import { EditorialMarker, EntrySummary, VocabularyItem } from '../../core/models/entry.models';
import { EntriesService } from '../../core/services/entries.service';

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
  private readonly entries = inject(EntriesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly targetQueries = new Subject<string>();

  @Input({ required: true }) markers!: FormArray<FormGroup>;
  @Input({ required: true }) crossReferences!: FormArray<FormGroup>;
  @Input() vocabularies: VocabularyItem[] = [];
  @Input() readonly = false;
  @Input() scopeOnly = false;
  @Input() layout: 'default' | 'definition' | 'example' = 'default';
  selectMarker(category: MarkerCategory, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const value = select.value;
    select.value = '';
    if (!value || this.readonly || !this.valuesFor(category).some((v) => v.code === value)) return;
    const same = this.markers.controls.filter((m) => m.get('category')?.value === category);
    if (
      same.some((m) => m.get('value')?.value === value) ||
      ((category === 'Style' || category === 'Domain') && same.length >= 2)
    )
      return;
    this.markers.push(
      this.fb.group({ id: [null as number | null], category: [category], value: [value] }),
    );
    this.contentChanged.emit();
  }
  markerLimit(category: MarkerCategory): boolean {
    return (
      (category === 'Style' || category === 'Domain') &&
      this.markers.controls.filter((m) => m.get('category')?.value === category).length >= 2
    );
  }
  @Output() readonly contentChanged = new EventEmitter<void>();

  readonly markerEditorOpen = signal(false);
  readonly markerEditingIndex = signal<number | null>(null);
  readonly xrEditorOpen = signal(false);
  readonly xrEditingIndex = signal<number | null>(null);
  readonly xrSuggestions = signal<EntrySummary[]>([]);
  readonly xrSearching = signal(false);

  readonly markerDraft = this.fb.group({
    category: ['Style' as MarkerCategory, Validators.required],
    value: ['', Validators.required],
  });
  readonly xrDraft = this.fb.group({
    type: ['see', Validators.required],
    target: ['', [Validators.required, Validators.maxLength(500)]],
    targetEntryId: [null as number | null, Validators.required],
    targetEntryLemma: [null as string | null],
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

  constructor() {
    this.targetQueries
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((query) => {
          const term = query.trim();
          if (!term) {
            this.xrSearching.set(false);
            return of([] as EntrySummary[]);
          }
          this.xrSearching.set(true);
          return this.entries.search(term, 6).pipe(
            catchError(() => of([] as EntrySummary[])),
            finalize(() => this.xrSearching.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((suggestions) => {
        this.xrSuggestions.set(suggestions);
        const target = this.normalizeTarget(this.xrDraft.controls.target.value ?? '');
        const exact = suggestions.find((item) => this.normalizeTarget(item.lemma) === target);
        if (exact) {
          this.xrDraft.patchValue(
            { targetEntryId: exact.id, targetEntryLemma: exact.lemma },
            { emitEvent: false },
          );
        }
      });
  }

  valuesFor(
    category: MarkerCategory = this.markerDraft.controls.category.value ?? 'Style',
  ): VocabularyItem[] {
    return this.vocabularies.filter((item) => item.category === category.toLocaleLowerCase());
  }

  markerCategoryChanged(): void {
    this.markerDraft.controls.value.setValue(this.valuesFor()[0]?.code ?? '');
  }

  openMarkerAdd(category: MarkerCategory = 'Style'): void {
    this.xrEditorOpen.set(false);
    this.markerEditingIndex.set(null);
    this.markerDraft.setValue({ category, value: this.valuesFor(category)[0]?.code ?? '' });
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
      this.markers.push(
        this.fb.group({
          id: [null as number | null],
          category: [value.category],
          value: [value.value],
        }),
      );
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
    this.markerEditorOpen.set(false);
    this.xrEditingIndex.set(null);
    this.xrDraft.setValue({
      type: 'see',
      target: '',
      targetEntryId: null,
      targetEntryLemma: null,
      displayText: '',
      placement: 'Inline',
    });
    this.xrSuggestions.set([]);
    this.xrEditorOpen.set(true);
  }

  openXrEdit(index: number): void {
    const item = this.crossReferences.at(index).getRawValue();
    this.xrEditingIndex.set(index);
    this.xrDraft.setValue({
      type: item.type ?? 'see',
      target: item.target ?? '',
      targetEntryId: item.targetEntryId ?? null,
      targetEntryLemma: item.targetEntryLemma ?? null,
      displayText: item.displayText ?? '',
      placement: item.placement ?? 'Standalone',
    });
    this.xrSuggestions.set([]);
    this.targetQueries.next(item.target ?? '');
    this.xrEditorOpen.set(true);
  }

  targetChanged(): void {
    const target = this.xrDraft.controls.target.value ?? '';
    this.xrDraft.patchValue({ targetEntryId: null, targetEntryLemma: null }, { emitEvent: false });
    this.targetQueries.next(target);
  }

  selectTarget(entry: EntrySummary): void {
    this.xrDraft.patchValue({
      target: entry.lemma,
      targetEntryId: entry.id,
      targetEntryLemma: entry.lemma,
    });
    this.xrSuggestions.set([]);
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
      this.crossReferences.push(
        this.fb.group({
          id: [null as number | null],
          ...Object.fromEntries(Object.entries(payload).map(([key, item]) => [key, [item]])),
        }),
      );
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
    return `${this.categories.find((category) => category.value === item.category)?.label ?? item.category} · ${this.markerValueLabel(item.category, item.value)}`;
  }

  private markerValueLabel(category: MarkerCategory, value: string): string {
    return (
      this.vocabularies.find(
        (item) => item.category === category.toLowerCase() && item.code === value,
      )?.label ?? value
    );
  }

  xrText(index: number): string {
    const item = this.crossReferences.at(index).getRawValue();
    return `${item.label} ${item.displayText || item.target}`;
  }

  xrLinkState(index: number): 'linked' | 'unresolved' | 'pending' {
    const item = this.crossReferences.at(index);
    if (item.get('targetEntryId')?.value) return 'linked';
    return item.get('id')?.value ? 'unresolved' : 'pending';
  }

  xrLinkText(index: number): string {
    const item = this.crossReferences.at(index).getRawValue();
    if (item.targetEntryId) return `დალინკებულია: ${item.targetEntryLemma || item.target}`;
    return item.id ? 'ვერ დალინკდა' : 'შენახვისას შემოწმდება';
  }

  private normalizeTarget(value: string): string {
    return value.trim().replace(/，/g, ',');
  }
}

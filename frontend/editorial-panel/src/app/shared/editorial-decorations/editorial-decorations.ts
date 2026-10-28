import { RouterLink } from '@angular/router';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  of,
  Subject,
  switchMap,
  Subscription,
} from 'rxjs';
import { EditorialMarker, EntrySummary, VocabularyItem } from '../../core/models/entry.models';
import { EntriesService } from '../../core/services/entries.service';

type MarkerCategory = EditorialMarker['category'];

@Component({
  selector: 'app-editorial-decorations',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './editorial-decorations.html',
  styleUrl: './editorial-decorations.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorialDecorations implements OnChanges {
  private readonly fb = new FormBuilder();
  private readonly entries = inject(EntriesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly targetQueries = new Subject<string>();

  @Input({ required: true }) markers!: FormArray<FormGroup>;
  @Input({ required: true }) crossReferences!: FormArray<FormGroup>;
  @Input() vocabularies: VocabularyItem[] = [];
  @Input() readonly = false;
  @Input() scopeOnly = false;
  @Input() textField?: HTMLTextAreaElement;
  @Input() textControl: AbstractControl | null = null;
  @Input() fieldName = 'definition';
  private insertionRange: { start: number; end: number } | null = null;
  private observedControl: AbstractControl | null = null;
  private textSubscription?: Subscription;
  ngOnChanges(): void {
    if (this.observedControl === this.textControl) return;
    this.textSubscription?.unsubscribe();
    this.observedControl = this.textControl;
    let previous = String(this.textControl?.value ?? '');
    this.textSubscription = this.textControl?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        const next = String(value ?? '');
        this.reconcileText(previous, next);
        previous = next;
      });
  }
  reconcileText(previous: string, next: string): void {
    let start = 0;
    while (start < previous.length && start < next.length && previous[start] === next[start])
      start++;
    let oldEnd = previous.length,
      newEnd = next.length;
    while (oldEnd > start && newEnd > start && previous[oldEnd - 1] === next[newEnd - 1]) {
      oldEnd--;
      newEnd--;
    }
    for (const annotations of [this.markers, this.crossReferences]) {
      for (let i = annotations.length - 1; i >= 0; i--) {
        const item = annotations.at(i);
        if (item.get('textField')?.value !== this.fieldName) continue;
        const offset = item.get('textOffset')?.value,
          text = item.get('displayText')?.value;
        if (typeof offset !== 'number' || !text) continue;
        if (offset >= oldEnd) item.get('textOffset')?.setValue(offset + newEnd - oldEnd);
        else if (offset + text.length > start) annotations.removeAt(i);
      }
    }
  }
  private insertText(text: string): number | null {
    if (!this.textControl || !this.textField) return null;
    const value = String(this.textControl.value ?? '');
    const range = this.insertionRange ?? {
      start: this.textField.selectionStart,
      end: this.textField.selectionEnd,
    };
    const shift = text.length - (range.end - range.start);
    for (const annotation of this.observedControl === this.textControl
      ? []
      : [...this.markers.controls, ...this.crossReferences.controls]) {
      if (annotation.get('textField')?.value !== this.fieldName) continue;
      const offset = annotation.get('textOffset')?.value;
      if (typeof offset === 'number' && offset >= range.end)
        annotation.get('textOffset')?.setValue(offset + shift);
    }
    this.textControl.setValue(value.slice(0, range.start) + text + value.slice(range.end));
    this.textControl.markAsDirty();
    this.textField.focus();
    this.textField.setSelectionRange(range.start + text.length, range.start + text.length);
    this.insertionRange = null;
    return range.start;
  }
  @Input() layout: 'default' | 'definition' | 'example' = 'default';
  selectMarker(category: MarkerCategory, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const value = select.value;
    select.value = '';
    if (!value || this.readonly || !this.valuesFor(category).some((v) => v.code === value)) return;
    if (category === 'Abbr' && this.textControl) {
      const displayText = this.markerValueLabel(category, value);
      const textOffset = this.insertText(displayText + ' ');
      this.markers.push(
        this.fb.group({
          id: [null],
          category: [category],
          value: [value],
          displayText: [displayText],
          textOffset: [textOffset],
          textField: [this.fieldName],
        }),
      );
      this.contentChanged.emit();
      return;
    }
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
  readonly xrError = signal('');

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
        const exactMatches = suggestions.filter(
          (item) => this.normalizeTarget(item.lemma) === target,
        );
        const exact = exactMatches.length === 1 ? exactMatches[0] : null;
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
    const item = this.markers.at(index);
    const value = String(this.textControl?.value ?? '');
    const offset = item.get('textOffset')?.value;
    const text = item.get('displayText')?.value;
    this.markers.removeAt(index);
    if (
      item.get('textField')?.value === this.fieldName &&
      typeof offset === 'number' &&
      text &&
      value.slice(offset, offset + text.length) === text
    ) {
      this.textControl?.setValue(value.slice(0, offset) + value.slice(offset + text.length));
      this.textControl?.markAsDirty();
    }
    this.contentChanged.emit();
  }

  openXrAdd(): void {
    if (this.readonly) return;
    this.insertionRange = this.textField
      ? { start: this.textField.selectionStart, end: this.textField.selectionEnd }
      : null;
    this.markerEditorOpen.set(false);
    this.xrError.set('');
    this.xrEditingIndex.set(null);
    this.xrDraft.setValue({
      type: 'see',
      target: '',
      targetEntryId: null,
      targetEntryLemma: null,
      displayText: '',
      placement: this.textControl && this.textField ? 'Inline' : 'Attached',
    });
    this.xrSuggestions.set([]);
    this.xrEditorOpen.set(true);
  }

  openXrEdit(index: number): void {
    this.xrError.set('');
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
    const inlineText = `${label} ${value.target}`;
    let offset = editing === null ? this.insertText(inlineText) : null;
    let editedGroup: FormGroup | null = null;
    if (editing !== null) {
      editedGroup = this.crossReferences.at(editing);
      const old = editedGroup.getRawValue();
      if (this.textControl && old.textField === this.fieldName && old.displayText) {
        const body = String(this.textControl.value ?? '');
        let start = old.textOffset;
        if (
          typeof start !== 'number' ||
          body.slice(start, start + old.displayText.length) !== old.displayText
        ) {
          start = body.indexOf(old.displayText);
          if (start < 0 || body.indexOf(old.displayText, start + 1) >= 0) {
            this.xrError.set(
              'Locate the original link in the text before changing it. · 请先定位原链接。',
            );
            return;
          }
        }
        this.crossReferences.removeAt(editing);
        this.insertionRange = { start, end: start + old.displayText.length };
        offset = this.insertText(inlineText);
      }
    }
    const payload = {
      ...value,
      label,
      ...(offset !== null
        ? { textOffset: offset, textField: this.fieldName, displayText: inlineText }
        : {}),
    };
    if (editing === null) {
      this.crossReferences.push(
        this.fb.group({
          id: [null as number | null],
          ...Object.fromEntries(Object.entries(payload).map(([key, item]) => [key, [item]])),
        }),
      );
    } else {
      editedGroup!.patchValue(payload);
      if (this.crossReferences.controls.indexOf(editedGroup!) < 0)
        this.crossReferences.insert(Math.min(editing, this.crossReferences.length), editedGroup!);
    }
    this.xrEditorOpen.set(false);
    this.contentChanged.emit();
  }

  removeXr(index: number): void {
    const item = this.crossReferences.at(index).getRawValue();
    this.crossReferences.removeAt(index);
    if (this.textControl && item.textField === this.fieldName && item.displayText) {
      const value = String(this.textControl.value ?? '');
      const offset = item.textOffset;
      if (
        typeof offset === 'number' &&
        value.slice(offset, offset + item.displayText.length) === item.displayText
      )
        this.textControl.setValue(
          value.slice(0, offset) + value.slice(offset + item.displayText.length),
        );
    }
    this.contentChanged.emit();
  }

  orderedMarkerIndices(): number[] {
    const rank: Record<string, number> = { Style: 0, Domain: 1, Lang: 2 };
    return this.markers.controls
      .map((_, i) => i)
      .sort(
        (a, b) =>
          (rank[this.markers.at(a).get('category')?.value] ?? 3) -
          (rank[this.markers.at(b).get('category')?.value] ?? 3),
      );
  }
  markerText(index: number): string {
    const item = this.markers.at(index).getRawValue();
    const label = this.markerValueLabel(item.category, item.value);
    const symbol = /[\u3400-\u9fff]/u.test(item.value) ? item.value : label.split(' · ')[0].trim();
    return item.category === 'Style'
      ? `〈${symbol.replace(/[〈〉<>]/g, '')}〉`
      : item.category === 'Domain'
        ? `【${symbol.replace(/[【】\[\]]/g, '')}】`
        : label;
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
    return item.textField ? item.displayText : `${item.label} ${item.displayText || item.target}`;
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

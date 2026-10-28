import { EntrySearchHighlight } from '../../../shared/entry-search-highlight';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  DestroyRef,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  ViewChild,
  ViewChildren,
  QueryList,
} from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, finalize, of, switchMap, tap } from 'rxjs';
import {
  CommentSummary,
  ReferenceMaterialDetail,
  SupportMaterialLanguage,
  SupportMaterialSummary,
} from '../../../core/models/collaboration.models';
import {
  ApiProblem,
  ComplexFormItem,
  CrossReference,
  DirectPos,
  EditorialMarker,
  EntryDetail,
  EntryHistoryItem,
  Example,
  GramGroup,
  Homonym,
  MediaAssetItem,
  PinyinItem,
  SaveEntryContentRequest,
  Sense,
  VocabularyItem,
} from '../../../core/models/entry.models';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { EntriesService } from '../../../core/services/entries.service';
import { MediaService } from '../../../core/services/media.service';
import {
  captureMediaPlacement,
  readMediaPlacement,
  resolveMediaOffset,
} from '../../../shared/inline-media';
import { getStatusMeta } from '../../../shared/status-badge/status-badge';
import { EditorialDecorations } from '../../../shared/editorial-decorations/editorial-decorations';
import { RepeatableTextList } from '../../../shared/repeatable-text-list/repeatable-text-list';
import { InlineEntryText } from '../../../shared/inline-entry-text';
import { LinkedMessageBody } from '../../../shared/linked-message-body/linked-message-body';
import { ReferenceText } from '../../../shared/reference-text';
import { InlineRichField } from '../../../shared/inline-rich-field';

type EntryTab = 'content' | 'reference' | 'media' | 'history';
type EntryNotesTab = 'reference' | 'comments' | 'support';

@Component({
  selector: 'app-entry-detail-page',
  imports: [
    DatePipe,
    DecimalPipe,
    NgTemplateOutlet,
    ReactiveFormsModule,
    EditorialDecorations,
    RepeatableTextList,
    InlineEntryText,
    LinkedMessageBody,
    ReferenceText,
    InlineRichField,
  ],
  templateUrl: './entry-detail-page.html',
  styleUrl: './entry-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryDetailPage implements OnInit, OnDestroy {
  private readonly searchHighlight = inject(EntrySearchHighlight);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly entriesService = inject(EntriesService);
  private readonly collaboration = inject(CollaborationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly mediaService = inject(MediaService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  @ViewChild('mediaInput') private mediaInput?: ElementRef<HTMLInputElement>;
  @ViewChildren(RepeatableTextList) private writtenForms?: QueryList<RepeatableTextList>;

  private entryId = 0;
  readonly isNew = signal(false);
  readonly framesVisible = signal(
    localStorage.getItem('cx-entry-editor-hierarchy-frames') !== 'off',
  );
  readonly editing = signal(false);
  readonly readerPreview = signal(false);
  readonly canEditFields = computed(() => this.canEdit() && this.editing());
  startEditing(): void {
    if (!this.canEdit() || this.saving()) return;
    this.saveSuccess.set(false);
    this.readerPreview.set(false);
    this.editing.set(true);
  }
  cancelEditing(): void {
    if (this.saving() || !window.confirm('Discard unsaved changes? · 放弃未保存的更改吗？')) return;
    const item = this.entry();
    if (item) this.populateEntryForm(item);
    this.referenceForm.reset(this.referenceBaseline);
    this.saveError.set(null);
    this.statusConfirmation.set(null);
    this.editing.set(false);
  }
  toggleFrames(): void {
    this.framesVisible.update((value) => !value);
    localStorage.setItem('cx-entry-editor-hierarchy-frames', this.framesVisible() ? 'on' : 'off');
  }
  readonly structureVisible = signal(false);
  readonly editingHomograph = signal<number | null>(null);
  homographLabel(index: number): string {
    const label = this.homographParts(index);
    return label.number ? `${label.text} ${label.number}` : label.text;
  }
  normalizeHomographName(value: unknown): string {
    return String(value ?? '')
      .normalize('NFC')
      .trim()
      .replace(/(?:\s+\d+)+$/u, '')
      .trim();
  }
  normalizeHomograph(node: FormGroup): void {
    if (node.get('pinyinId')?.value == null && node.get('pinyinOrder')?.value == null) {
      node.get('text')?.setValue(this.normalizeHomographName(node.get('text')?.value));
      this.markContentChanged();
    }
  }
  homographParts(index: number): { text: string; number: number | null } {
    const values = this.homonyms.controls.map((node) => {
      const linked =
        node.get('pinyinId')?.value != null
          ? this.pinyins.controls.find((p) => p.get('id')?.value === node.get('pinyinId')?.value)
          : node.get('pinyinOrder')?.value != null
            ? this.pinyins.at(node.get('pinyinOrder')?.value)
            : null;
      return this.normalizeHomographName(linked?.get('value')?.value ?? node.get('text')?.value);
    });
    const value = values[index];
    if (!value) return { text: `H${index + 1}`, number: null };
    const matching = values.filter(
      (item) => item.toLocaleLowerCase() === value.toLocaleLowerCase(),
    ).length;
    return {
      text: value,
      number:
        matching > 1
          ? values
              .slice(0, index + 1)
              .filter((item) => item.toLocaleLowerCase() === value.toLocaleLowerCase()).length
          : null,
    };
  }
  homographPronunciationIndex(node: FormGroup): string {
    const id = node.get('pinyinId')?.value;
    const index =
      id != null
        ? this.pinyins.controls.findIndex((p) => p.get('id')?.value === id)
        : node.get('pinyinOrder')?.value;
    return index != null && index >= 0 ? String(index) : '';
  }
  linkHomographPronunciation(node: FormGroup, value: string): void {
    const pinyin = value === '' ? null : this.pinyins.at(Number(value));
    node.patchValue({
      pinyinId: pinyin?.get('id')?.value ?? null,
      pinyinOrder: pinyin && pinyin.get('id')?.value == null ? Number(value) : null,
      text: pinyin?.get('value')?.value ?? node.get('text')?.value ?? '',
    });
    this.markContentChanged();
    this.editingHomograph.set(null);
  }
  readonly entry = signal<EntryDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeTab = signal<EntryTab>('content');
  readonly notesTab = signal<EntryNotesTab>('reference');
  readonly referenceGroups = [
    { key: 'g1', title: 'Chinese–English · 汉英', source: '汉英词典' },
    { key: 'g2', title: 'Chinese explanatory · 汉语释义', source: '现代汉语词典' },
    { key: 'g3', title: 'Chinese–Russian · 汉俄', source: '汉俄词典' },
    { key: 'g4', title: 'Other reference material · 其他参考资料', source: '' },
    { key: 'g5', title: 'Additional information · 补充资料', source: '' },
  ];
  readonly collapsedReferenceGroups = signal<string[]>([]);
  toggleReferenceGroup(group: string): void {
    this.collapsedReferenceGroups.update((groups) =>
      groups.includes(group) ? groups.filter((item) => item !== group) : [...groups, group],
    );
  }
  readonly privateMessageRecipient = signal<{ id: number; name: string } | null>(null);
  readonly privateMessageText = signal('');
  readonly privateMessageSaving = signal(false);
  readonly privateMessageError = signal<string | null>(null);
  openPrivateMessage(comment: CommentSummary): void {
    this.privateMessageRecipient.set({
      id: comment.authorEditorId,
      name: comment.authorDisplayName,
    });
    this.privateMessageText.set('');
    this.privateMessageError.set(null);
  }
  replyParent(id: number): CommentSummary | undefined {
    return this.comments().find((comment) => comment.id === id);
  }
  sendPrivateMessage(): void {
    const recipient = this.privateMessageRecipient();
    if (!recipient || !this.privateMessageText().trim() || this.privateMessageSaving()) return;
    this.privateMessageSaving.set(true);
    this.collaboration
      .sendMessage(recipient.id, this.privateMessageText().trim())
      .pipe(finalize(() => this.privateMessageSaving.set(false)))
      .subscribe({
        next: () => this.privateMessageRecipient.set(null),
        error: (error) =>
          this.privateMessageError.set(
            this.problemMessage(error, 'Unable to send message · 无法发送消息'),
          ),
      });
  }
  commentRole(role?: string | null): string {
    return (
      (
        {
          SuperAdmin: 'SV · 主管',
          ChiefEditor: 'EC · 主编',
          DeputyChiefEditor: 'DE · 副主编',
          Editor: 'CE · 汉外翻译编辑',
          KaEditor: 'GE · 格文编辑',
        } as Record<string, string>
      )[role || ''] ||
      role ||
      ''
    );
  }
  readonly notesExpanded = signal(false);
  readonly saving = signal(false);
  readonly saveSuccess = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly draftWarning = signal<string | null>(null);
  readonly concurrencyConflict = signal(false);
  readonly changingStatus = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);
  readonly statusConfirmation = signal<string | null>(null);

  readonly comments = signal<CommentSummary[]>([]);
  readonly commentThreads = computed(() => {
    const comments = this.comments();
    const byId = new Map(comments.map((comment) => [comment.id, comment]));
    const threads = new Map<number, { id: number; comments: CommentSummary[] }>();
    for (const comment of [...comments].sort(
      (a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc) || a.id - b.id,
    )) {
      let root = comment;
      const visited = new Set<number>([root.id]);
      while (
        root.replyToCommentId &&
        byId.has(root.replyToCommentId) &&
        !visited.has(root.replyToCommentId)
      ) {
        root = byId.get(root.replyToCommentId)!;
        visited.add(root.id);
      }
      let thread = threads.get(root.id);
      if (!thread) {
        thread = { id: root.id, comments: [] };
        threads.set(root.id, thread);
      }
      thread.comments.push(comment);
    }
    return [...threads.values()];
  });
  readonly commentsLoading = signal(false);
  readonly commentText = signal('');
  readonly commentTarget = signal<number | null>(null);
  readonly replyingTo = signal<CommentSummary | null>(null);
  readonly commentsPinned = signal(false);
  readonly commentFontSize = signal(16);
  readonly referenceQuery = signal('');
  readonly referenceGroup = signal('all');
  readonly referencePinned = signal(false);
  readonly referenceRaw = signal(false);
  readonly referenceFontSize = signal(16);
  commentOn(node: FormGroup): void {
    const id = node.get('id')?.value;
    if (!id) return;
    this.commentTarget.set(id);
    this.replyingTo.set(null);
    this.notesExpanded.set(true);
    this.setNotesTab('comments');
  }
  replyTo(comment: CommentSummary): void {
    this.replyingTo.set(comment);
    this.commentTarget.set(comment.targetSegmentId ?? null);
  }
  replyAuthor(id: number): string {
    return this.comments().find((comment) => comment.id === id)?.authorDisplayName ?? '';
  }
  commentTargetLabel(id: number): string {
    const visit = (node: FormGroup | FormArray): string | null => {
      if (node instanceof FormGroup && node.get('id')?.value === id) {
        return (
          node.get('definition')?.value ||
          [node.get('zh')?.value, node.get('ka')?.value].filter(Boolean).join(' — ') ||
          null
        );
      }
      for (const child of Object.values(node.controls)) {
        if (child instanceof FormGroup || child instanceof FormArray) {
          const result = visit(child);
          if (result) return result;
        }
      }
      return null;
    };
    return visit(this.entryForm) ?? 'Entry · 词目';
  }
  resizeNotes(kind: 'comments' | 'reference', delta: number): void {
    const size = kind === 'comments' ? this.commentFontSize : this.referenceFontSize;
    size.update((value) => Math.min(40, Math.max(12, value + delta)));
  }
  selectReferenceGroup(group: string): void {
    this.referenceGroup.set(group);
    if (group !== 'all')
      this.collapsedReferenceGroups.update((items) => items.filter((item) => item !== group));
  }
  referenceVisible(group: string): boolean {
    if (this.referenceGroup() !== 'all' && this.referenceGroup() !== group) return false;
    const query = this.referenceQuery().trim().toLocaleLowerCase();
    return (
      !query ||
      String(this.referenceForm.get(group)?.value ?? '')
        .toLocaleLowerCase()
        .includes(query)
    );
  }
  readonly commentSaving = signal(false);
  referenceLines(group: string): string[] {
    return String(this.referenceForm.get(group)?.value ?? '')
      .split(/\r?\n|\/{2,}/)
      .map((line) => line.trim())
      .filter(Boolean);
  }
  readonly editingCommentId = signal<number | null>(null);
  readonly editingCommentText = signal('');
  readonly commentError = signal<string | null>(null);
  readonly pendingCommentDeleteId = signal<number | null>(null);
  readonly deletingCommentId = signal<number | null>(null);

  readonly supportMaterials = signal<SupportMaterialSummary[]>([]);
  readonly sortedSupportMaterials = computed(() => {
    const languageOrder: Record<SupportMaterialLanguage, number> = {
      Chinese: 0,
      English: 1,
      Russian: 2,
      Other: 3,
    };

    return [...this.supportMaterials()].sort(
      (left, right) => languageOrder[left.language] - languageOrder[right.language],
    );
  });
  readonly supportMaterialsLoading = signal(false);
  readonly supportMaterialSaving = signal(false);
  readonly supportMaterialError = signal<string | null>(null);
  readonly pendingSupportMaterialDeleteId = signal<number | null>(null);
  readonly deletingSupportMaterialId = signal<number | null>(null);

  readonly referenceLoading = signal(false);
  readonly referenceSaving = signal(false);
  readonly referenceError = signal<string | null>(null);

  readonly history = signal<EntryHistoryItem[]>([]);
  readonly historyLoading = signal(false);
  readonly historyError = signal<string | null>(null);

  readonly media = signal<MediaAssetItem[]>([]);
  readonly mediaUrls = signal<Record<number, string>>({});
  readonly mediaLoading = signal(false);
  readonly mediaSaving = signal(false);
  readonly mediaError = signal<string | null>(null);
  readonly selectedMedia = signal<File | null>(null);
  readonly mediaDisplayName = signal('');
  readonly selectedMediaPreview = signal<string | null>(null);
  readonly mediaTarget = signal<{ kind: 'sense' | 'example'; node: FormGroup } | null>(null);
  readonly useExistingMedia = signal(false);
  readonly mediaLibraryItems = signal<MediaAssetItem[]>([]);
  readonly mediaLibraryLoading = signal(false);
  readonly mediaLibraryPage = signal(1);
  readonly mediaLibraryTotal = signal(0);
  private mediaLibraryQuery = '';
  private mediaLibraryRequest = 0;
  browseMedia(query = this.mediaLibraryQuery, page = 1): void {
    this.useExistingMedia.set(true);
    this.mediaLibraryQuery = query;
    this.mediaLibraryLoading.set(true);
    this.mediaError.set(null);
    const request = ++this.mediaLibraryRequest;
    this.mediaService
      .library(page, query)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          if (request !== this.mediaLibraryRequest) return;
          this.mediaLibraryItems.set(result.items);
          this.mediaLibraryPage.set(result.page);
          this.mediaLibraryTotal.set(result.total);
          this.mediaLibraryLoading.set(false);
          result.items.forEach((item) => this.loadMediaUrl(item));
        },
        error: () => {
          if (request === this.mediaLibraryRequest) {
            this.mediaLibraryLoading.set(false);
            this.mediaError.set('სურათების ჩატვირთვა ვერ მოხერხდა.');
          }
        },
      });
  }
  insertExistingMedia(asset: MediaAssetItem): void {
    if (
      !this.entryId ||
      !this.canEditFields() ||
      this.mediaSaving() ||
      !this.mediaTarget()?.node.get('id')?.value
    )
      return;
    this.mediaSaving.set(true);
    this.mediaError.set(null);
    this.mediaService
      .reuse(this.entryId, asset.id, this.mediaPlacement())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.mediaSaving.set(false)),
      )
      .subscribe({
        next: (item) => {
          this.media.update((items) => [item, ...items]);
          this.loadMediaUrl(item);
          this.mediaTarget.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.mediaError.set(this.problemMessage(error, 'სურათის ჩასმა ვერ მოხერხდა.')),
      });
  }
  readonly exampleMediaFields = signal(new Map<FormGroup, 'zh' | 'ka'>());
  rememberExampleMediaField(node: FormGroup, field: 'zh' | 'ka'): void {
    this.exampleMediaFields.update((fields) => new Map(fields).set(node, field));
  }
  openExampleMedia(
    node: FormGroup,
    chinese: HTMLTextAreaElement,
    georgian: HTMLTextAreaElement,
  ): void {
    const field = this.exampleMediaFields().get(node);
    if (field) this.openNodeMedia('example', node, field === 'zh' ? chinese : georgian, field);
  }
  openNodeMedia(
    kind: 'sense' | 'example',
    node: FormGroup,
    field: HTMLTextAreaElement,
    fieldName: 'definition' | 'zh' | 'ka',
  ): void {
    if (!this.canEditFields()) return;
    this.clearSelectedMedia();
    this.mediaError.set(null);
    this.useExistingMedia.set(false);
    this.mediaDisplayName.set('');
    this.mediaTarget.set({ kind, node });
    this.mediaPlacement.set(
      captureMediaPlacement(
        kind,
        node.get('id')?.value,
        fieldName,
        field.value,
        field.selectionStart ?? field.value.length,
      ),
    );
  }
  closeNodeMedia(): void {
    if (this.mediaSaving()) return;
    this.clearSelectedMedia();
    this.mediaTarget.set(null);
  }
  nodeMedia(kind: 'sense' | 'example', node: FormGroup): MediaAssetItem[] {
    const id = node.get('id')?.value;
    return id ? this.media().filter((item) => item.placement === kind + ':' + id) : [];
  }
  inlineNodeMedia(kind: 'sense' | 'example', node: FormGroup, field: 'definition' | 'zh' | 'ka') {
    const id = node.get('id')?.value;
    const body = node.get(field)?.value || '';
    return this.media().flatMap((asset) => {
      const placement = readMediaPlacement(asset.placement);
      const url = this.mediaUrl(asset.id);
      return placement &&
        url &&
        placement.kind === kind &&
        placement.nodeId === id &&
        placement.field === field
        ? [
            {
              id: asset.id,
              offset: resolveMediaOffset(placement, body),
              url,
              alt: asset.displayName || asset.originalFilename,
              label: asset.displayName || asset.originalFilename.replace(/\.[^.]+$/, ''),
            },
          ]
        : [];
    });
  }
  hasInlineFormatting(node: FormGroup, field: string): boolean {
    return ['markers', 'crossReferences'].some((key) =>
      this.formArrayAt(node, key).controls.some((item) => item.get('textField')?.value === field),
    );
  }
  resizeEditorText(fields: HTMLTextAreaElement[], delta: number): void {
    for (const field of fields) {
      const size = parseFloat(getComputedStyle(field).fontSize) || 16;
      field.style.fontSize = Math.min(40, Math.max(12, size + delta)) + 'px';
      const formatted = field.parentElement?.querySelector<HTMLElement>('.formatted-editor-text');
      if (formatted) formatted.style.fontSize = field.style.fontSize;
      field.style.height = 'auto';
      field.style.height = field.scrollHeight + 'px';
    }
  }
  addDefinitionPoint(field: HTMLTextAreaElement, sense: FormGroup): void {
    const value = field.value;
    const count = (value.match(/[①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳]/g) ?? []).length;
    if (count >= 20) return;
    const position = field.selectionStart ?? value.length;
    const token =
      (position && value[position - 1] !== '\n' ? '\n' : '') +
      String.fromCodePoint(0x2460 + count) +
      ' ';
    sense
      .get('definition')
      ?.setValue(value.slice(0, position) + token + value.slice(field.selectionEnd ?? position));
    this.markContentChanged();
    field.focus();
    field.setSelectionRange(position + token.length, position + token.length);
  }
  readonly mediaPlacement = signal('cover');
  readonly pendingMediaDeleteId = signal<number | null>(null);
  readonly deletingMediaId = signal<number | null>(null);
  readonly vocabularies = signal<VocabularyItem[]>([]);
  readonly vocabularyError = signal<string | null>(null);

  readonly entryForm = this.fb.group({
    lemma: ['', [Validators.required, Validators.maxLength(255)]],
    pinyins: this.fb.array<FormGroup>([]),
    complexForms: this.fb.array<FormGroup>([]),
    homonyms: this.fb.array<FormGroup>([]),
    gramGroups: this.fb.array<FormGroup>([]),
    directPoses: this.fb.array<FormGroup>([]),
    senses: this.fb.array<FormGroup>([]),
  });

  private referenceBaseline = { g1: '', g2: '', g3: '', g4: '', g5: '' };
  readonly referenceSaved = signal(false);
  readonly referenceForm = this.fb.group({
    g1: [''],
    g2: [''],
    g3: [''],
    g4: [''],
    g5: [''],
  });

  readonly supportMaterialForm = this.fb.nonNullable.group({
    language: ['Chinese' as SupportMaterialLanguage, Validators.required],
    commentText: ['', [Validators.required, Validators.maxLength(4000)]],
  });

  readonly canEdit = computed(() => {
    const item = this.entry();
    if (!item) return false;
    return item.capabilities?.canEditContent ?? this.fallbackCanEdit(item.status);
  });

  readonly allowedTransitions = computed(() => {
    const item = this.entry();
    if (!item) return [];
    return item.capabilities?.allowedStatusTransitions ?? this.fallbackTransitions(item.status);
  });

  readonly canManageComments = computed(() => {
    const role = this.auth.editor()?.role;
    return role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'DeputyChiefEditor';
  });

  get homonyms(): FormArray<FormGroup> {
    return this.entryForm.controls.homonyms;
  }
  nodeReferences(node: FormGroup): CrossReference[] {
    return this.formArrayAt(node, 'crossReferences').getRawValue() as CrossReference[];
  }

  get pinyins(): FormArray<FormGroup> {
    return this.entryForm.controls.pinyins;
  }

  get complexForms(): FormArray<FormGroup> {
    return this.entryForm.controls.complexForms;
  }

  get rootGramGroups(): FormArray<FormGroup> {
    return this.entryForm.controls.gramGroups;
  }

  get directPoses(): FormArray<FormGroup> {
    return this.entryForm.controls.directPoses;
  }

  get rootSenses(): FormArray<FormGroup> {
    return this.entryForm.controls.senses;
  }

  ngOnInit(): void {
    if (this.route.snapshot.data['newEntry']) {
      this.isNew.set(true);
      this.editing.set(true);
      const draft: EntryDetail = {
        id: 0,
        lemma: '',
        status: 'NewEntry',
        pinyin: null,
        pinyins: [],
        complexForms: [],
        homonyms: [],
        gramGroups: [],
        directPoses: [],
        senses: [],
        rowVersion: 0,
        mainAuthorDisplayName: null,
        lastEditorDisplayName: this.auth.editor()?.displayName ?? '',
        createdAtUtc: '',
        updatedAtUtc: '',
        capabilities: { canEditContent: true, allowedStatusTransitions: [] },
      };
      this.entry.set(draft);
      this.populateEntryForm(draft);
      this.loading.set(false);
      this.loadVocabularies();
      return;
    }
    this.searchHighlight.query.set(this.route.snapshot.queryParamMap.get('q') ?? '');
    this.entryId = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(this.entryId) || this.entryId < 1) {
      this.error.set('სტატიის ნომერი არასწორია.');
      this.loading.set(false);
      return;
    }

    this.loadEntry();
    this.loadComments();
    this.loadSupportMaterials();
    this.loadReferenceMaterial();
    this.loadHistory();
    this.loadMedia();
    this.loadVocabularies();
  }

  ngOnDestroy(): void {
    this.searchHighlight.query.set('');
    const selectedPreview = this.selectedMediaPreview();
    if (selectedPreview) URL.revokeObjectURL(selectedPreview);
    Object.values(this.mediaUrls()).forEach((url) => URL.revokeObjectURL(url));
  }

  setTab(tab: EntryTab): void {
    this.activeTab.set(tab);
  }

  setNotesTab(tab: EntryNotesTab): void {
    if (tab !== 'reference') this.referencePinned.set(false);
    this.notesTab.set(tab);
    this.notesExpanded.set(true);
  }

  gramGroupsAt(homonymIndex: number): FormArray<FormGroup> {
    return this.homonyms.at(homonymIndex).get('gramGroups') as FormArray<FormGroup>;
  }

  sensesAt(homonymIndex: number, gramGroupIndex: number): FormArray<FormGroup> {
    return this.gramGroupsAt(homonymIndex).at(gramGroupIndex).get('senses') as FormArray<FormGroup>;
  }

  examplesAt(
    homonymIndex: number,
    gramGroupIndex: number,
    senseIndex: number,
  ): FormArray<FormGroup> {
    return this.sensesAt(homonymIndex, gramGroupIndex)
      .at(senseIndex)
      .get('examples') as FormArray<FormGroup>;
  }

  formArrayAt(group: FormGroup, name: string): FormArray<FormGroup> {
    return group.get(name) as FormArray<FormGroup>;
  }

  posOptions(): VocabularyItem[] {
    return this.vocabularies().filter((item) => item.category === 'pos');
  }

  markContentChanged(): void {
    this.entryForm.markAsDirty();
  }

  toRoman(value: number): string {
    if (!Number.isInteger(value) || value < 1) return '';

    const numerals: ReadonlyArray<readonly [number, string]> = [
      [1000, 'M'],
      [900, 'CM'],
      [500, 'D'],
      [400, 'CD'],
      [100, 'C'],
      [90, 'XC'],
      [50, 'L'],
      [40, 'XL'],
      [10, 'X'],
      [9, 'IX'],
      [5, 'V'],
      [4, 'IV'],
      [1, 'I'],
    ];
    let remainder = value;
    let result = '';

    for (const [amount, numeral] of numerals) {
      while (remainder >= amount) {
        result += numeral;
        remainder -= amount;
      }
    }

    return result;
  }

  structureLines(): string[] {
    const lines: string[] = [];
    const senses = (items: FormArray<FormGroup>, prefix: string) =>
      items.controls.forEach((sense, i) => {
        const examples = this.formArrayAt(sense, 'examples').length;
        lines.push(prefix + ' → S' + (i + 1) + (examples ? ' → E (' + examples + ')' : ' → ∅ E'));
      });
    const groups = (items: FormArray<FormGroup>, prefix: string) =>
      items.controls.forEach((group, i) => {
        const label =
          prefix +
          ' → POS ' +
          this.toRoman(i + 1) +
          ' [' +
          (group.get('pos')?.value || 'არ არის არჩეული') +
          ']';
        const children = this.formArrayAt(group, 'senses');
        if (children.length) senses(children, label);
        else lines.push(label + ' → ∅ S');
      });
    this.homonyms.controls.forEach((h, i) => {
      const prefix = 'Entry → H' + (i + 1);
      const children = this.formArrayAt(h, 'gramGroups');
      const direct = this.formArrayAt(h, 'directSenses');
      groups(children, prefix);
      senses(direct, prefix + ' → ∅ POS');
      if (!children.length && !direct.length) lines.push(prefix + ' → ∅ POS → ∅ S');
    });
    groups(this.rootGramGroups, 'Entry → ∅ H');
    groups(this.directPoses, 'Entry → ∅ H');
    senses(this.rootSenses, 'Entry → ∅ H → ∅ POS');
    return lines.length ? lines : ['Entry → ∅ H → ∅ POS → ∅ S'];
  }

  structureKind(): 'homonym' | 'grammar' | 'pos' | 'sense' | null {
    if (this.homonyms.length) return 'homonym';
    if (this.rootGramGroups.length) return 'grammar';
    if (this.directPoses.length) return 'pos';
    if (this.rootSenses.length) return 'sense';
    return null;
  }

  startStructure(kind: 'homonym' | 'grammar' | 'pos' | 'sense'): void {
    if (this.structureKind() !== null) return;
    if (kind === 'homonym') {
      this.homonyms.push(this.createHomonym());
      this.homonyms.push(this.createHomonym());
    }
    if (kind === 'grammar') this.rootGramGroups.push(this.createGramGroup(1));
    if (kind === 'pos') this.directPoses.push(this.createDirectPos());
    if (kind === 'sense') this.rootSenses.push(this.createSense(1));
    this.entryForm.markAsDirty();
  }

  addHomonym(): void {
    if (!this.homonyms.length && (this.rootGramGroups.length || this.rootSenses.length)) {
      const first = this.createHomonym();
      for (const [source, key] of [
        [this.rootGramGroups, 'gramGroups'],
        [this.rootSenses, 'directSenses'],
      ] as const) {
        const destination = this.formArrayAt(first, key);
        while (source.length) {
          const control = source.at(0);
          source.removeAt(0);
          destination.push(control);
        }
      }
      this.homonyms.push(first);
    }
    if (this.homonyms.length >= 10) return;
    this.homonyms.push(this.createHomonym());
    this.entryForm.markAsDirty();
  }

  readonly pendingHomonymRemoval = signal<number | null>(null);
  removeHomonym(index: number): void {
    const node = this.homonyms.at(index);
    if (!node) return;
    if (
      node.get('text')?.value ||
      ['gramGroups', 'directSenses', 'markers', 'crossReferences'].some(
        (key) => this.formArrayAt(node, key).length,
      )
    ) {
      this.pendingHomonymRemoval.set(index);
      return;
    }
    this.deleteHomonym(index);
  }
  confirmHomonymRemoval(): void {
    const index = this.pendingHomonymRemoval();
    if (index !== null) this.deleteHomonym(index);
    this.pendingHomonymRemoval.set(null);
  }
  private deleteHomonym(index: number): void {
    this.homonyms.removeAt(index);
    this.editingHomograph.set(null);
    this.entryForm.markAsDirty();
  }

  addGramGroup(homonymIndex: number): void {
    const groups = this.gramGroupsAt(homonymIndex);
    groups.push(this.createGramGroup(groups.length + 1));
    this.entryForm.markAsDirty();
  }

  addGroupTo(groups: FormArray<FormGroup>): void {
    if (groups.parent?.get('directSenses')?.value?.length) return;
    groups.push(this.createGramGroup(groups.length + 1));
    this.entryForm.markAsDirty();
  }

  removeGroupFrom(groups: FormArray<FormGroup>, index: number): void {
    groups.removeAt(index);
    groups.controls.forEach((group, groupIndex) => group.get('number')?.setValue(groupIndex + 1));
    this.entryForm.markAsDirty();
  }

  addDirectPos(): void {
    this.directPoses.push(this.createDirectPos());
    this.entryForm.markAsDirty();
  }

  removeDirectPos(index: number): void {
    this.directPoses.removeAt(index);
    this.entryForm.markAsDirty();
  }

  addSenseTo(senses: FormArray<FormGroup>): void {
    if (
      senses.parent?.get('directSenses') === senses &&
      senses.parent?.get('gramGroups')?.value?.length
    )
      return;
    senses.push(this.createSense(senses.length + 1));
    this.entryForm.markAsDirty();
  }

  removeSenseFrom(senses: FormArray<FormGroup>, index: number): void {
    if (senses.parent?.get('pos') && senses.length === 1) return;
    senses.removeAt(index);
    this.renumberSenses(senses);
    this.entryForm.markAsDirty();
  }

  addExampleTo(examples: FormArray<FormGroup>): void {
    examples.push(this.createExample());
    this.entryForm.markAsDirty();
  }

  removeExampleFrom(examples: FormArray<FormGroup>, index: number): void {
    examples.removeAt(index);
    this.entryForm.markAsDirty();
  }

  removeGramGroup(homonymIndex: number, gramGroupIndex: number): void {
    const groups = this.gramGroupsAt(homonymIndex);
    groups.removeAt(gramGroupIndex);
    groups.controls.forEach((group, index) => group.get('number')?.setValue(index + 1));
    this.entryForm.markAsDirty();
  }

  addSense(homonymIndex: number, gramGroupIndex: number): void {
    const senses = this.sensesAt(homonymIndex, gramGroupIndex);
    senses.push(this.createSense(senses.length + 1));
    this.entryForm.markAsDirty();
  }

  removeSense(homonymIndex: number, gramGroupIndex: number, senseIndex: number): void {
    const senses = this.sensesAt(homonymIndex, gramGroupIndex);
    senses.removeAt(senseIndex);
    this.renumberSenses(senses);
    this.entryForm.markAsDirty();
  }

  addExample(homonymIndex: number, gramGroupIndex: number, senseIndex: number): void {
    this.examplesAt(homonymIndex, gramGroupIndex, senseIndex).push(this.createExample());
    this.entryForm.markAsDirty();
  }

  removeExample(
    homonymIndex: number,
    gramGroupIndex: number,
    senseIndex: number,
    exampleIndex: number,
  ): void {
    this.examplesAt(homonymIndex, gramGroupIndex, senseIndex).removeAt(exampleIndex);
    this.entryForm.markAsDirty();
  }

  saveContent(): void {
    const item = this.entry();
    if (!item || !this.canEditFields() || this.saving() || this.referenceSaving()) return;

    if (this.writtenForms?.some((list) => !list.flushInline())) {
      this.saveError.set(
        'გადაამოწმეთ პინინი და ტრადიციული ფორმები — მნიშვნელობები არ უნდა მეორდებოდეს.',
      );
      return;
    }

    const parents = [
      this.rootGramGroups,
      ...this.homonyms.controls.map((h) => this.formArrayAt(h, 'gramGroups')),
    ];
    const isDraft = item.status === 'NewEntry';
    const duplicatePos = parents.some((groups) => {
      const values = groups.controls
        .map((g) => String(g.get('pos')?.value ?? '').trim())
        .filter(Boolean);
      return new Set(values).size !== values.length;
    });
    if (duplicatePos && !isDraft) {
      this.saveError.set(
        'ერთი მშობლის ქვეშ ერთი და იგივე POS არ უნდა მეორდებოდეს — მნიშვნელობები ერთ POS ჯგუფში დაამატეთ.',
      );
      return;
    }
    this.entryForm.markAllAsTouched();
    if (this.entryForm.controls.lemma.invalid || (this.entryForm.invalid && !isDraft)) {
      this.saveError.set('შეავსეთ სტატიის სათაური და გადაამოწმეთ ფორმა.');
      return;
    }
    this.draftWarning.set(
      isDraft && (this.entryForm.invalid || duplicatePos)
        ? 'Incomplete draft; complete its structure before review · 草稿尚未完成，请在送审前补全结构'
        : null,
    );

    this.saving.set(true);
    this.saveError.set(null);
    this.referenceError.set(null);
    this.saveSuccess.set(false);
    this.concurrencyConflict.set(false);

    const creating = item.id === 0;
    const source = creating
      ? this.entriesService
          .create({ lemma: this.entryForm.controls.lemma.value!.trim(), pinyins: [] })
          .pipe(
            tap((created) => {
              this.entryId = created.id;
              this.entry.set(created);
            }),
            switchMap((created) =>
              this.entriesService.saveContent(created.id, this.toSaveRequest(created.rowVersion)),
            ),
          )
      : this.entriesService.saveContent(item.id, this.toSaveRequest(item.rowVersion));
    source
      .pipe(
        tap((updated) => this.entry.set(updated)),
        switchMap((updated) => {
          if (!this.referenceForm.dirty) return of(updated);
          const value = this.referenceForm.getRawValue();
          return this.collaboration
            .saveReferenceMaterial(updated.id, {
              g1: value.g1 ?? '',
              g2: value.g2 ?? '',
              g3: value.g3 ?? '',
              g4: value.g4 ?? '',
              g5: value.g5 ?? '',
            })
            .pipe(
              tap((detail) => {
                this.populateReference(detail);
                this.referenceForm.markAsPristine();
                this.referenceSaved.set(true);
              }),
              switchMap(() => of(updated)),
              catchError((error) => {
                this.referenceError.set(
                  'სტატია შეინახა, მაგრამ G1–G5 ვერ შეინახა. ხელახლა დააჭირეთ Save-ს.',
                );
                throw error;
              }),
            );
        }),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (updated) => {
          this.entry.set(updated);
          this.populateEntryForm(updated);
          this.editing.set(false);
          this.saveSuccess.set(true);
          if (this.isNew()) {
            this.isNew.set(false);
            void this.router.navigate(['/adminpanel/entries', updated.id], { replaceUrl: true });
          }
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 409) {
            this.concurrencyConflict.set(true);
            this.saveError.set('სტატია სხვამ შეცვალა. განაახლეთ მონაცემები და შემდეგ შეინახეთ.');
            return;
          }
          this.saveError.set(
            this.referenceError() ?? this.problemMessage(error, 'სტატიის შენახვა ვერ მოხერხდა.'),
          );
        },
      });
  }

  reloadAfterConflict(): void {
    this.concurrencyConflict.set(false);
    this.loadEntry();
  }

  changeStatus(targetStatus: string): void {
    const item = this.entry();
    if (!item || !item.id || this.changingStatus()) return;
    if (this.entryForm.dirty || this.referenceForm.dirty) {
      this.statusError.set('სტატუსის შეცვლამდე შეინახეთ ცვლილებები — Save · 保存.');
      return;
    }

    const validationErrors = this.publishValidationErrors(targetStatus);
    if (validationErrors.length) {
      this.statusError.set(validationErrors.join(' '));
      return;
    }

    this.statusError.set(null);
    this.statusConfirmation.set(targetStatus);
  }

  confirmStatusChange(): void {
    const item = this.entry();
    const targetStatus = this.statusConfirmation();
    if (!item || !targetStatus || this.changingStatus()) return;

    this.statusConfirmation.set(null);
    this.changingStatus.set(targetStatus);
    this.statusError.set(null);
    this.entriesService
      .changeStatus(item.id, targetStatus)
      .pipe(finalize(() => this.changingStatus.set(null)))
      .subscribe({
        next: (updated) => {
          this.entry.set(updated);
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) =>
          this.statusError.set(this.problemMessage(error, 'სტატუსის შეცვლა ვერ მოხერხდა.')),
      });
  }

  cancelStatusChange(): void {
    this.statusConfirmation.set(null);
  }

  updateCommentText(event: Event): void {
    this.commentText.set((event.target as HTMLTextAreaElement).value);
  }

  addComment(): void {
    const text = this.commentText().trim();
    if (!text || this.commentSaving()) return;

    this.commentSaving.set(true);
    this.commentError.set(null);
    this.collaboration
      .addComment(this.entryId, text, {
        targetSegmentId: this.commentTarget(),
        replyToCommentId: this.replyingTo()?.id,
      })
      .pipe(finalize(() => this.commentSaving.set(false)))
      .subscribe({
        next: (comment) => {
          this.comments.update((comments) => [...comments, comment]);
          this.commentText.set('');
          this.replyingTo.set(null);
          this.commentTarget.set(null);
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) =>
          this.commentError.set(this.problemMessage(error, 'კომენტარის დამატება ვერ მოხერხდა.')),
      });
  }

  startCommentEdit(comment: CommentSummary): void {
    this.editingCommentId.set(comment.id);
    this.editingCommentText.set(comment.commentText);
  }

  updateEditingCommentText(event: Event): void {
    this.editingCommentText.set((event.target as HTMLTextAreaElement).value);
  }

  saveCommentEdit(): void {
    const id = this.editingCommentId();
    const text = this.editingCommentText().trim();
    if (!id || !text) return;

    this.collaboration.updateComment(id, text).subscribe({
      next: (updated) => {
        this.comments.update((comments) =>
          comments.map((item) => (item.id === id ? updated : item)),
        );
        this.editingCommentId.set(null);
      },
      error: (error: HttpErrorResponse) =>
        this.commentError.set(this.problemMessage(error, 'კომენტარის შეცვლა ვერ მოხერხდა.')),
    });
  }

  requestCommentDelete(id: number): void {
    this.pendingCommentDeleteId.set(id);
  }

  deleteComment(id: number): void {
    if (this.deletingCommentId()) return;
    this.deletingCommentId.set(id);
    this.commentError.set(null);
    this.collaboration
      .deleteComment(id)
      .pipe(finalize(() => this.deletingCommentId.set(null)))
      .subscribe({
        next: () => {
          this.comments.update((comments) => comments.filter((item) => item.id !== id));
          this.pendingCommentDeleteId.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.commentError.set(this.problemMessage(error, 'კომენტარის წაშლა ვერ მოხერხდა.')),
      });
  }

  canEditComment(comment: CommentSummary): boolean {
    return comment.authorEditorId === this.auth.editor()?.id || this.canManageComments();
  }

  addSupportMaterial(): void {
    if (this.supportMaterialForm.invalid || this.supportMaterialSaving()) {
      this.supportMaterialForm.markAllAsTouched();
      return;
    }

    const value = this.supportMaterialForm.getRawValue();
    this.supportMaterialSaving.set(true);
    this.supportMaterialError.set(null);
    this.collaboration
      .addSupportMaterial(this.entryId, {
        language: value.language,
        commentText: value.commentText.trim(),
      })
      .pipe(finalize(() => this.supportMaterialSaving.set(false)))
      .subscribe({
        next: (item) => {
          this.supportMaterials.update((items) => [item, ...items]);
          this.supportMaterialForm.reset({ language: 'Chinese', commentText: '' });
        },
        error: (error: HttpErrorResponse) =>
          this.supportMaterialError.set(
            this.problemMessage(error, 'დამხმარე მასალის დამატება ვერ მოხერხდა.'),
          ),
      });
  }

  requestSupportMaterialDelete(id: number): void {
    this.pendingSupportMaterialDeleteId.set(id);
  }

  deleteSupportMaterial(id: number): void {
    if (this.deletingSupportMaterialId()) return;
    this.deletingSupportMaterialId.set(id);
    this.supportMaterialError.set(null);
    this.collaboration
      .deleteSupportMaterial(this.entryId, id)
      .pipe(finalize(() => this.deletingSupportMaterialId.set(null)))
      .subscribe({
        next: () => {
          this.supportMaterials.update((items) => items.filter((item) => item.id !== id));
          this.pendingSupportMaterialDeleteId.set(null);
        },
        error: (error: HttpErrorResponse) =>
          this.supportMaterialError.set(
            this.problemMessage(error, 'დამხმარე მასალის წაშლა ვერ მოხერხდა.'),
          ),
      });
  }

  supportLanguageLabel(language: SupportMaterialLanguage): string {
    return {
      English: 'ინგლისური',
      Chinese: 'ჩინური',
      Russian: 'რუსული',
      Other: 'სხვა',
    }[language];
  }

  saveReferenceMaterial(): void {
    if (!this.canEditFields() || this.referenceSaving() || this.saving()) return;
    if (!this.entryId) {
      this.saveContent();
      return;
    }
    this.referenceSaving.set(true);
    this.referenceSaved.set(false);
    this.referenceError.set(null);
    const value = this.referenceForm.getRawValue();
    this.collaboration
      .saveReferenceMaterial(this.entryId, {
        g1: value.g1 ?? '',
        g2: value.g2 ?? '',
        g3: value.g3 ?? '',
        g4: value.g4 ?? '',
        g5: value.g5 ?? '',
      })
      .pipe(finalize(() => this.referenceSaving.set(false)))
      .subscribe({
        next: (detail) => {
          this.populateReference(detail);
          this.referenceForm.markAsPristine();
          this.referenceSaved.set(true);
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) =>
          this.referenceError.set(
            this.problemMessage(error, 'საცნობარო მასალის შენახვა ვერ მოხერხდა.'),
          ),
      });
  }

  selectMedia(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.clearSelectedMedia(false);
    this.mediaError.set(null);
    if (!file) return;

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowedTypes.includes(file.type)) {
      this.mediaError.set('დასაშვებია მხოლოდ JPEG, PNG ან WebP სურათი.');
      input.value = '';
      return;
    }
    if (file.size <= 0 || file.size > 5 * 1024 * 1024) {
      this.mediaError.set('სურათის ზომა უნდა იყოს 5 MB-მდე.');
      input.value = '';
      return;
    }

    this.selectedMedia.set(file);
    this.selectedMediaPreview.set(URL.createObjectURL(file));
  }

  updateMediaPlacement(event: Event): void {
    this.mediaPlacement.set((event.target as HTMLSelectElement).value);
  }

  uploadMedia(): void {
    const file = this.selectedMedia();
    if (!file || !this.canEditFields() || !this.entryId || this.mediaSaving()) return;
    if (!this.mediaDisplayName().trim()) {
      this.mediaError.set('შეიყვანეთ სურათის სახელი.');
      return;
    }
    if (this.mediaTarget() && !this.mediaTarget()!.node.get('id')?.value) return;

    this.mediaSaving.set(true);
    this.mediaError.set(null);
    this.mediaService
      .upload(this.entryId, file, this.mediaPlacement(), this.mediaDisplayName())
      .pipe(finalize(() => this.mediaSaving.set(false)))
      .subscribe({
        next: (item) => {
          this.media.update((items) => [item, ...items]);
          this.loadMediaUrl(item);
          this.clearSelectedMedia();
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) =>
          this.mediaError.set(this.problemMessage(error, 'სურათის ატვირთვა ვერ მოხერხდა.')),
      });
  }

  requestMediaDelete(id: number): void {
    this.pendingMediaDeleteId.set(id);
  }

  deleteMedia(id: number): void {
    if (!this.canEdit() || this.deletingMediaId()) return;
    this.deletingMediaId.set(id);
    this.mediaError.set(null);
    this.mediaService
      .delete(id)
      .pipe(finalize(() => this.deletingMediaId.set(null)))
      .subscribe({
        next: () => {
          const url = this.mediaUrls()[id];
          if (url) URL.revokeObjectURL(url);
          this.mediaUrls.update((urls) => {
            const next = { ...urls };
            delete next[id];
            return next;
          });
          this.media.update((items) => items.filter((item) => item.id !== id));
          this.pendingMediaDeleteId.set(null);
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) =>
          this.mediaError.set(this.problemMessage(error, 'სურათის წაშლა ვერ მოხერხდა.')),
      });
  }

  mediaUrl(id: number): string | null {
    return this.mediaUrls()[id] ?? null;
  }

  clearSelectedMedia(resetInput = true): void {
    const preview = this.selectedMediaPreview();
    if (preview) URL.revokeObjectURL(preview);
    this.selectedMedia.set(null);
    this.selectedMediaPreview.set(null);
    if (resetInput && this.mediaInput) this.mediaInput.nativeElement.value = '';
  }

  statusLabel(status: string): string {
    return getStatusMeta(status).label;
  }

  historyActionLabel(action: string): string {
    return (
      {
        Created: 'სტატია შეიქმნა',
        StatusChanged: 'Stage changed · 阶段更改',
        ContentEdited: 'Entry edited · 编辑词目',
        CommentAdded: 'კომენტარი დაემატა',
        Archived: 'დაარქივდა',
        Other: 'სხვა ცვლილება',
      }[action] ?? action
    );
  }

  historyDetail(item: EntryHistoryItem): string | null {
    if (item.action === 'StatusChanged') {
      const before = this.statusFromAuditValue(item.oldValue);
      const after = this.statusFromAuditValue(item.newValue);
      if (before && after) return `${this.statusLabel(before)} → ${this.statusLabel(after)}`;
    }
    return (
      item.notes ||
      (item.action === 'ContentEdited' ? 'Entry content updated · 词目内容已更新' : null)
    );
  }
  historySection(item: EntryHistoryItem): string {
    if (item.action === 'StatusChanged') return 'Stage · 状态';
    if (item.action === 'CommentAdded') return 'Comments · 评论';
    if (/comment/i.test(item.notes || '')) return 'Comments · 评论';
    if (/reference/i.test(item.notes || '')) return 'Reference materials · 参考资料';
    return 'Entry · 词目';
  }

  private loadEntry(): void {
    this.loading.set(true);
    this.error.set(null);
    this.entriesService
      .getById(this.entryId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (entry) => {
          this.entry.set(entry);
          this.populateEntryForm(entry);
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(
            error.status === 404 ? 'სტატია ვერ მოიძებნა.' : 'სტატიის ჩატვირთვა ვერ მოხერხდა.',
          ),
      });
  }

  private loadVocabularies(): void {
    this.entriesService.getVocabularies().subscribe({
      next: (items) => this.vocabularies.set(items),
      error: () => this.vocabularyError.set('სელექტების მნიშვნელობები ვერ ჩაიტვირთა.'),
    });
  }

  private loadComments(): void {
    this.commentsLoading.set(true);
    this.collaboration
      .getComments(this.entryId)
      .pipe(finalize(() => this.commentsLoading.set(false)))
      .subscribe({
        next: (comments) => this.comments.set(comments),
        error: () => this.commentError.set('კომენტარების ჩატვირთვა ვერ მოხერხდა.'),
      });
  }

  private loadSupportMaterials(): void {
    this.supportMaterialsLoading.set(true);
    this.supportMaterialError.set(null);
    this.collaboration
      .getSupportMaterials(this.entryId)
      .pipe(finalize(() => this.supportMaterialsLoading.set(false)))
      .subscribe({
        next: (items) => this.supportMaterials.set(items),
        error: () => this.supportMaterialError.set('დამხმარე მასალის ჩატვირთვა ვერ მოხერხდა.'),
      });
  }

  private loadReferenceMaterial(): void {
    this.referenceLoading.set(true);
    this.collaboration
      .getReferenceMaterial(this.entryId)
      .pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 404) return of(null);
          this.referenceError.set('საცნობარო მასალის ჩატვირთვა ვერ მოხერხდა.');
          return of(null);
        }),
        finalize(() => this.referenceLoading.set(false)),
      )
      .subscribe((detail) => {
        if (detail) this.populateReference(detail);
      });
  }

  private loadHistory(): void {
    this.historyLoading.set(true);
    this.historyError.set(null);
    this.entriesService
      .getHistory(this.entryId)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: (items) => this.history.set(items),
        error: () => this.historyError.set('ცვლილებების ისტორიის ჩატვირთვა ვერ მოხერხდა.'),
      });
  }

  private loadMedia(): void {
    this.mediaLoading.set(true);
    this.mediaError.set(null);
    this.mediaService
      .listForEntry(this.entryId)
      .pipe(finalize(() => this.mediaLoading.set(false)))
      .subscribe({
        next: (items) => {
          this.media.set(items);
          items.forEach((item) => this.loadMediaUrl(item));
        },
        error: (error: HttpErrorResponse) =>
          this.mediaError.set(this.problemMessage(error, 'სურათების ჩატვირთვა ვერ მოხერხდა.')),
      });
  }

  private loadMediaUrl(item: MediaAssetItem): void {
    this.mediaService.getContent(item.storageUrl).subscribe({
      next: (blob) => {
        const previous = this.mediaUrls()[item.id];
        if (previous) URL.revokeObjectURL(previous);
        const url = URL.createObjectURL(blob);
        this.mediaUrls.update((urls) => ({ ...urls, [item.id]: url }));
      },
      error: () => this.mediaError.set(`სურათი „${item.originalFilename}“ ვერ ჩაიტვირთა.`),
    });
  }

  private populateEntryForm(entry: EntryDetail): void {
    this.entryForm.controls.lemma.setValue(entry.lemma);
    this.pinyins.clear();
    for (const pinyin of entry.pinyins ?? []) this.pinyins.push(this.createPinyin(pinyin));
    this.complexForms.clear();
    for (const form of entry.complexForms ?? [])
      this.complexForms.push(this.createComplexForm(form));
    this.homonyms.clear();
    for (const homonym of entry.homonyms) this.homonyms.push(this.createHomonym(homonym));
    this.rootGramGroups.clear();
    for (const group of entry.gramGroups ?? [])
      this.rootGramGroups.push(this.createGramGroup(group.number, group));
    this.directPoses.clear();
    for (const pos of entry.directPoses ?? []) this.directPoses.push(this.createDirectPos(pos));
    this.rootSenses.clear();
    for (const sense of entry.senses ?? [])
      this.rootSenses.push(this.createSense(sense.number, sense));
    this.entryForm.markAsPristine();
  }

  private populateReference(detail: ReferenceMaterialDetail): void {
    this.referenceForm.setValue({
      g1: detail.g1,
      g2: detail.g2,
      g3: detail.g3,
      g4: detail.g4,
      g5: detail.g5,
    });
    this.referenceBaseline = {
      g1: detail.g1 ?? '',
      g2: detail.g2 ?? '',
      g3: detail.g3 ?? '',
      g4: detail.g4 ?? '',
      g5: detail.g5 ?? '',
    };
    this.referenceForm.markAsPristine();
  }

  private statusFromAuditValue(value: string | null): string | null {
    if (!value) return null;
    try {
      const parsed = JSON.parse(value) as { status?: string };
      return parsed.status ?? null;
    } catch {
      return null;
    }
  }

  private createHomonym(homonym?: Homonym & { senses?: Sense[] }): FormGroup {
    return this.fb.group({
      id: [homonym?.id ?? null],
      pinyinId: [homonym?.pinyinId ?? null],
      pinyinOrder: [homonym?.pinyinOrder ?? null],
      text: [homonym?.text ?? ''],
      directSenses: this.fb.array<FormGroup>(
        (homonym?.directSenses ?? []).map((s) => this.createSense(s.number, s)),
      ),
      markers: this.createMarkers(homonym?.markers),
      crossReferences: this.createCrossReferences(homonym?.crossReferences),
      gramGroups: this.fb.array<FormGroup>(
        homonym
          ? (
              homonym.gramGroups ?? [
                {
                  id: null,
                  posSegmentId: null,
                  number: 1,
                  pos: null,
                  markers: [],
                  crossReferences: [],
                  senses: homonym.senses ?? [],
                },
              ]
            ).map((group) => this.createGramGroup(group.number, group))
          : [],
      ),
    });
  }

  private createPinyin(pinyin: PinyinItem): FormGroup {
    return this.fb.group({ id: [pinyin.id], value: [pinyin.value], order: [pinyin.order] });
  }

  private createComplexForm(form: ComplexFormItem): FormGroup {
    return this.fb.group({ id: [form.id], value: [form.value], order: [form.order] });
  }

  private createGramGroup(number: number, group?: GramGroup): FormGroup {
    return this.fb.group({
      id: [group?.id ?? null],
      posSegmentId: [group?.posSegmentId ?? null],
      number: [number],
      pos: [group?.pos ?? '', Validators.required],
      markers: this.createMarkers(group?.markers),
      crossReferences: this.createCrossReferences(group?.crossReferences),
      senses: this.fb.array<FormGroup>(
        group
          ? group.senses.map((sense) => this.createSense(sense.number, sense))
          : [this.createSense(1)],
      ),
    });
  }

  private createDirectPos(pos?: DirectPos): FormGroup {
    return this.fb.group({
      id: [pos?.id ?? null],
      pos: [pos?.pos ?? '', Validators.required],
      markers: this.createMarkers(pos?.markers),
      crossReferences: this.createCrossReferences(pos?.crossReferences),
      senses: this.fb.array<FormGroup>(
        pos
          ? pos.senses.map((sense) => this.createSense(sense.number, sense))
          : [this.createSense(1)],
      ),
    });
  }

  private createSense(number: number, sense?: Sense): FormGroup {
    return this.fb.group({
      id: [sense?.id ?? null],
      definitionId: [sense?.definitionId ?? null],
      number: [number],
      definition: [sense?.definition ?? ''],
      markers: this.createMarkers(sense?.markers),
      crossReferences: this.createCrossReferences(sense?.crossReferences),
      examples: this.fb.array<FormGroup>(
        (sense?.examples ?? []).map((example) => this.createExample(example)),
      ),
    });
  }

  private createExample(example?: Example): FormGroup {
    return this.fb.group({
      id: [example?.id ?? null],
      zhSegmentId: [example?.zhSegmentId ?? null],
      kaSegmentId: [example?.kaSegmentId ?? null],
      zh: [example?.zh ?? ''],
      ka: [example?.ka ?? ''],
      markers: this.createMarkers(example?.markers),
      crossReferences: this.createCrossReferences(example?.crossReferences),
    });
  }

  private createMarkers(items: EditorialMarker[] = []): FormArray<FormGroup> {
    return this.fb.array<FormGroup>(
      items.map((item) =>
        this.fb.group({
          id: [item.id],
          category: [item.category],
          value: [item.value],
          textOffset: [item.textOffset ?? null],
          textField: [item.textField ?? null],
          displayText: [item.displayText ?? null],
        }),
      ),
    );
  }

  private createCrossReferences(items: CrossReference[] = []): FormArray<FormGroup> {
    return this.fb.array<FormGroup>(
      items.map((item) =>
        this.fb.group({
          id: [item.id],
          type: [item.type],
          label: [item.label],
          target: [item.target],
          targetEntryId: [item.targetEntryId ?? null],
          targetEntryLemma: [item.targetEntryLemma ?? null],
          displayText: [item.displayText ?? ''],
          placement: [item.placement],
          textOffset: [item.textOffset ?? null],
          textField: [item.textField ?? null],
        }),
      ),
    );
  }

  private renumberSenses(senses: FormArray<FormGroup>): void {
    senses.controls.forEach((sense, index) => sense.get('number')?.setValue(index + 1));
  }

  private toSaveRequest(rowVersion: number): SaveEntryContentRequest {
    const raw = this.entryForm.getRawValue();
    const homonyms = (raw.homonyms ?? []) as unknown as Homonym[];
    const gramGroups = (raw.gramGroups ?? []) as unknown as GramGroup[];
    const directPoses = (raw.directPoses ?? []) as unknown as DirectPos[];
    const senses = (raw.senses ?? []) as unknown as Sense[];
    const pinyins = (raw.pinyins ?? []) as unknown as PinyinItem[];
    const complexForms = (raw.complexForms ?? []) as unknown as ComplexFormItem[];

    const mapExample = (example: Example): Example => ({
      id: example.id ?? null,
      zhSegmentId: example.zhSegmentId ?? null,
      kaSegmentId: example.kaSegmentId ?? null,
      zh: example.zh ?? '',
      ka: example.ka ?? '',
      markers: example.markers ?? [],
      crossReferences: example.crossReferences ?? [],
    });
    const mapSense = (sense: Sense, index: number): Sense => ({
      id: sense.id ?? null,
      definitionId: sense.definitionId ?? null,
      number: index + 1,
      definition: sense.definition ?? '',
      markers: sense.markers ?? [],
      crossReferences: sense.crossReferences ?? [],
      examples: (sense.examples ?? []).map(mapExample),
    });
    const mapGroup = (group: GramGroup, index: number): GramGroup => ({
      id: group.id ?? null,
      posSegmentId: group.posSegmentId ?? null,
      number: index + 1,
      pos: group.pos?.trim() || null,
      markers: group.markers ?? [],
      crossReferences: group.crossReferences ?? [],
      senses: (group.senses ?? []).map(mapSense),
    });
    const mapDirectPos = (pos: DirectPos): DirectPos => ({
      id: pos.id ?? null,
      pos: pos.pos?.trim() ?? '',
      markers: pos.markers ?? [],
      crossReferences: pos.crossReferences ?? [],
      senses: (pos.senses ?? []).map(mapSense),
    });

    return {
      lemma: raw.lemma?.trim() ?? '',
      pinyins: pinyins.map((item, index) => ({
        id: item.id ?? null,
        value: item.value.trim(),
        order: index,
      })),
      complexForms: complexForms.map((item, index) => ({
        id: item.id ?? null,
        value: item.value.trim(),
        order: index,
      })),
      rowVersion,
      homonyms: homonyms.map((homonym) => ({
        pinyinId: homonym.pinyinId ?? null,
        pinyinOrder: homonym.pinyinOrder ?? null,
        id: homonym.id ?? null,
        text: homonym.text?.trim() ?? '',
        directSenses: (homonym.directSenses ?? []).map(mapSense),
        markers: homonym.markers ?? [],
        crossReferences: homonym.crossReferences ?? [],
        gramGroups: (homonym.gramGroups ?? []).map(mapGroup),
      })),
      gramGroups: gramGroups.map(mapGroup),
      directPoses: directPoses.map(mapDirectPos),
      senses: senses.map(mapSense),
    };
  }

  private publishValidationErrors(targetStatus: string): string[] {
    if (this.normalizeStatus(targetStatus) !== 'Published') return [];
    const content = this.toSaveRequest(this.entry()?.rowVersion ?? 0);
    const errors: string[] = [];
    if (!content.lemma) errors.push('გამოქვეყნებისთვის სათაური აუცილებელია.');
    if (
      content.homonyms.some(
        (homonym) => !homonym.gramGroups.length && !homonym.directSenses?.length,
      )
    ) {
      errors.push('ყველა ჰომონიმს უნდა ჰქონდეს მინიმუმ ერთი გრამატიკული ჯგუფი.');
    }
    const groups = [
      ...content.homonyms.flatMap((homonym) => homonym.gramGroups),
      ...content.gramGroups,
    ];
    if (groups.some((group) => !group.pos) || content.directPoses.some((pos) => !pos.pos)) {
      errors.push('ყველა გრამატიკულ ჯგუფს უნდა ჰქონდეს არჩეული POS.');
    }
    if (
      groups.some((group) => !group.senses.length) ||
      content.directPoses.some((pos) => !pos.senses.length)
    ) {
      errors.push('გამოქვეყნებისთვის საჭიროა მინიმუმ ერთი მნიშვნელობა.');
    }
    const senses = [
      ...content.homonyms.flatMap((h) => h.directSenses ?? []),
      ...groups.flatMap((group) => group.senses),
      ...content.directPoses.flatMap((pos) => pos.senses),
      ...content.senses,
    ];
    if (!senses.length) errors.push('გამოქვეყნებისთვის საჭიროა მინიმუმ ერთი მნიშვნელობა.');
    if (senses.some((sense) => !sense.definition)) {
      errors.push('ყველა მნიშვნელობას უნდა ჰქონდეს განმარტება.');
    }
    return errors;
  }

  private fallbackCanEdit(status: string): boolean {
    const role = this.auth.editor()?.role;
    const normalized = this.normalizeStatus(status);
    if (
      role === 'SuperAdmin' ||
      role === 'ChiefEditor' ||
      role === 'DeputyChiefEditor' ||
      role === 'Editor'
    )
      return true;
    if (role === 'AssistantEditor') return normalized === 'NewEntry';
    return role === 'KaEditor' && normalized === 'KaReview';
  }

  private fallbackTransitions(status: string): string[] {
    const role = this.auth.editor()?.role;
    const current = this.normalizeStatus(status);
    const all = ['NewEntry', 'ZhReview', 'KaReview', 'Ready', 'Published', 'Archived'];
    if (role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'DeputyChiefEditor')
      return all.filter((item) => item !== current);
    if (role === 'Editor') {
      const working = ['NewEntry', 'ZhReview', 'KaReview', 'Ready'];
      return working.includes(current) ? working.filter((item) => item !== current) : [];
    }
    if (role === 'KaEditor' && current === 'KaReview') return ['ZhReview', 'Ready'];
    return [];
  }

  private normalizeStatus(status: string): string {
    const compact = status.replace(/[_-]/g, '').toLowerCase();
    return (
      {
        newentry: 'NewEntry',
        zhreview: 'ZhReview',
        kareview: 'KaReview',
        ready: 'Ready',
        published: 'Published',
        archived: 'Archived',
      }[compact] ?? status
    );
  }

  private problemMessage(error: HttpErrorResponse, fallback: string): string {
    const problem = error.error as ApiProblem | null;
    return problem?.detail ?? problem?.message ?? fallback;
  }
}

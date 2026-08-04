import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
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
import { StatusBadge } from '../../../shared/status-badge/status-badge';
import { EditorialDecorations } from '../../../shared/editorial-decorations/editorial-decorations';
import { RepeatableTextList } from '../../../shared/repeatable-text-list/repeatable-text-list';

type EntryTab = 'content' | 'reference' | 'media' | 'history';
type EntryNotesTab = 'comments' | 'support';

@Component({
  selector: 'app-entry-detail-page',
  imports: [
    DatePipe,
    DecimalPipe,
    NgTemplateOutlet,
    ReactiveFormsModule,
    RouterLink,
    StatusBadge,
    EditorialDecorations,
    RepeatableTextList,
  ],
  templateUrl: './entry-detail-page.html',
  styleUrl: './entry-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryDetailPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly entriesService = inject(EntriesService);
  private readonly collaboration = inject(CollaborationService);
  private readonly mediaService = inject(MediaService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  @ViewChild('mediaInput') private mediaInput?: ElementRef<HTMLInputElement>;

  private entryId = 0;
  readonly entry = signal<EntryDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeTab = signal<EntryTab>('content');
  readonly notesTab = signal<EntryNotesTab>('comments');
  readonly saving = signal(false);
  readonly saveSuccess = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly concurrencyConflict = signal(false);
  readonly changingStatus = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);
  readonly statusConfirmation = signal<string | null>(null);

  readonly comments = signal<CommentSummary[]>([]);
  readonly commentsLoading = signal(false);
  readonly commentText = signal('');
  readonly commentSaving = signal(false);
  readonly editingCommentId = signal<number | null>(null);
  readonly editingCommentText = signal('');
  readonly commentError = signal<string | null>(null);
  readonly pendingCommentDeleteId = signal<number | null>(null);
  readonly deletingCommentId = signal<number | null>(null);

  readonly supportMaterials = signal<SupportMaterialSummary[]>([]);
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
  readonly selectedMediaPreview = signal<string | null>(null);
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

  readonly referenceForm = this.fb.group({
    g1: [''],
    g2: [''],
    g3: [''],
    g4: [''],
    g5: [''],
  });

  readonly supportMaterialForm = this.fb.nonNullable.group({
    language: ['English' as SupportMaterialLanguage, Validators.required],
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
    return role === 'SuperAdmin' || role === 'ChiefEditor';
  });

  get homonyms(): FormArray<FormGroup> {
    return this.entryForm.controls.homonyms;
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
    const selectedPreview = this.selectedMediaPreview();
    if (selectedPreview) URL.revokeObjectURL(selectedPreview);
    Object.values(this.mediaUrls()).forEach((url) => URL.revokeObjectURL(url));
  }

  setTab(tab: EntryTab): void {
    this.activeTab.set(tab);
  }

  setNotesTab(tab: EntryNotesTab): void {
    this.notesTab.set(tab);
  }

  gramGroupsAt(homonymIndex: number): FormArray<FormGroup> {
    return this.homonyms.at(homonymIndex).get('gramGroups') as FormArray<FormGroup>;
  }

  sensesAt(homonymIndex: number, gramGroupIndex: number): FormArray<FormGroup> {
    return this.gramGroupsAt(homonymIndex).at(gramGroupIndex).get('senses') as FormArray<FormGroup>;
  }

  examplesAt(homonymIndex: number, gramGroupIndex: number, senseIndex: number): FormArray<FormGroup> {
    return this.sensesAt(homonymIndex, gramGroupIndex).at(senseIndex).get('examples') as FormArray<FormGroup>;
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

  structureKind(): 'homonym' | 'grammar' | 'pos' | 'sense' | null {
    if (this.homonyms.length) return 'homonym';
    if (this.rootGramGroups.length) return 'grammar';
    if (this.directPoses.length) return 'pos';
    if (this.rootSenses.length) return 'sense';
    return null;
  }

  startStructure(kind: 'homonym' | 'grammar' | 'pos' | 'sense'): void {
    if (this.structureKind() !== null) return;
    if (kind === 'homonym') this.homonyms.push(this.createHomonym());
    if (kind === 'grammar') this.rootGramGroups.push(this.createGramGroup(1));
    if (kind === 'pos') this.directPoses.push(this.createDirectPos());
    if (kind === 'sense') this.rootSenses.push(this.createSense(1));
    this.entryForm.markAsDirty();
  }

  addHomonym(): void {
    this.homonyms.push(this.createHomonym());
    this.entryForm.markAsDirty();
  }

  removeHomonym(index: number): void {
    this.homonyms.removeAt(index);
    this.entryForm.markAsDirty();
  }

  addGramGroup(homonymIndex: number): void {
    const groups = this.gramGroupsAt(homonymIndex);
    groups.push(this.createGramGroup(groups.length + 1));
    this.entryForm.markAsDirty();
  }

  addGroupTo(groups: FormArray<FormGroup>): void {
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
    senses.push(this.createSense(senses.length + 1));
    this.entryForm.markAsDirty();
  }

  removeSenseFrom(senses: FormArray<FormGroup>, index: number): void {
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

  removeExample(homonymIndex: number, gramGroupIndex: number, senseIndex: number, exampleIndex: number): void {
    this.examplesAt(homonymIndex, gramGroupIndex, senseIndex).removeAt(exampleIndex);
    this.entryForm.markAsDirty();
  }

  saveContent(): void {
    const item = this.entry();
    if (!item || !this.canEdit() || this.saving()) return;

    this.entryForm.markAllAsTouched();
    if (this.entryForm.invalid) {
      this.saveError.set('შეავსეთ სტატიის სათაური და გადაამოწმეთ ფორმა.');
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.saveSuccess.set(false);
    this.concurrencyConflict.set(false);

    this.entriesService
      .saveContent(item.id, this.toSaveRequest(item.rowVersion))
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (updated) => {
          this.entry.set(updated);
          this.entryForm.markAsPristine();
          this.saveSuccess.set(true);
          this.loadHistory();
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 409) {
            this.concurrencyConflict.set(true);
            this.saveError.set('სტატია სხვამ შეცვალა. განაახლეთ მონაცემები და შემდეგ შეინახეთ.');
            return;
          }
          this.saveError.set(this.problemMessage(error, 'სტატიის შენახვა ვერ მოხერხდა.'));
        },
      });
  }

  reloadAfterConflict(): void {
    this.concurrencyConflict.set(false);
    this.loadEntry();
  }

  changeStatus(targetStatus: string): void {
    const item = this.entry();
    if (!item || this.changingStatus()) return;

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
      .addComment(this.entryId, text)
      .pipe(finalize(() => this.commentSaving.set(false)))
      .subscribe({
        next: (comment) => {
          this.comments.update((comments) => [comment, ...comments]);
          this.commentText.set('');
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
          this.supportMaterialForm.reset({ language: 'English', commentText: '' });
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
    }[language];
  }

  saveReferenceMaterial(): void {
    if (!this.canEdit() || this.referenceSaving()) return;
    this.referenceSaving.set(true);
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

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
    if (!allowedTypes.includes(file.type)) {
      this.mediaError.set('დასაშვებია მხოლოდ JPEG, PNG, WebP ან GIF სურათი.');
      input.value = '';
      return;
    }
    if (file.size <= 0 || file.size > 10 * 1024 * 1024) {
      this.mediaError.set('სურათის ზომა უნდა იყოს 10 MB-მდე.');
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
    if (!file || !this.canEdit() || this.mediaSaving()) return;

    this.mediaSaving.set(true);
    this.mediaError.set(null);
    this.mediaService
      .upload(this.entryId, file, this.mediaPlacement())
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
    const key = this.normalizeStatus(status);
    return (
      {
        NewEntry: 'ახალი სტატია',
        ZhReview: 'ჩინური შემოწმება',
        KaReview: 'ქართული შემოწმება',
        Ready: 'მზადაა',
        Published: 'გამოქვეყნებული',
        Archived: 'არქივი',
      }[key] ?? status
    );
  }

  historyActionLabel(action: string): string {
    return (
      {
        Created: 'სტატია შეიქმნა',
        StatusChanged: 'სტატუსი შეიცვალა',
        ContentEdited: 'შინაარსი განახლდა',
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
    return item.notes;
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
        error: () =>
          this.supportMaterialError.set('დამხმარე მასალის ჩატვირთვა ვერ მოხერხდა.'),
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
    for (const form of entry.complexForms ?? []) this.complexForms.push(this.createComplexForm(form));
    this.homonyms.clear();
    for (const homonym of entry.homonyms) this.homonyms.push(this.createHomonym(homonym));
    this.rootGramGroups.clear();
    for (const group of entry.gramGroups ?? []) this.rootGramGroups.push(this.createGramGroup(group.number, group));
    this.directPoses.clear();
    for (const pos of entry.directPoses ?? []) this.directPoses.push(this.createDirectPos(pos));
    this.rootSenses.clear();
    for (const sense of entry.senses ?? []) this.rootSenses.push(this.createSense(sense.number, sense));
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

  private createHomonym(homonym?: Homonym): FormGroup {
    return this.fb.group({
      id: [homonym?.id ?? null],
      markers: this.createMarkers(homonym?.markers),
      crossReferences: this.createCrossReferences(homonym?.crossReferences),
      gramGroups: this.fb.array<FormGroup>(
        homonym
          ? homonym.gramGroups.map((group) => this.createGramGroup(group.number, group))
          : [this.createGramGroup(1)],
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
        pos ? pos.senses.map((sense) => this.createSense(sense.number, sense)) : [this.createSense(1)],
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
      examples: this.fb.array<FormGroup>((sense?.examples ?? []).map((example) => this.createExample(example))),
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
      items.map((item) => this.fb.group({ id: [item.id], category: [item.category], value: [item.value] })),
    );
  }

  private createCrossReferences(items: CrossReference[] = []): FormArray<FormGroup> {
    return this.fb.array<FormGroup>(
      items.map((item) =>
        this.fb.group({
          id: [item.id], type: [item.type], label: [item.label], target: [item.target],
          targetEntryId: [item.targetEntryId ?? null], targetEntryLemma: [item.targetEntryLemma ?? null],
          displayText: [item.displayText ?? ''], placement: [item.placement],
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
      zh: example.zh?.trim() ?? '',
      ka: example.ka?.trim() ?? '',
      markers: example.markers ?? [],
      crossReferences: example.crossReferences ?? [],
    });
    const mapSense = (sense: Sense, index: number): Sense => ({
      id: sense.id ?? null,
      definitionId: sense.definitionId ?? null,
      number: index + 1,
      definition: sense.definition?.trim() ?? '',
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
        id: homonym.id ?? null,
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
    if (content.homonyms.some((homonym) => !homonym.gramGroups.length)) {
      errors.push('ყველა ჰომონიმს უნდა ჰქონდეს მინიმუმ ერთი გრამატიკული ჯგუფი.');
    }
    const groups = [...content.homonyms.flatMap((homonym) => homonym.gramGroups), ...content.gramGroups];
    if (groups.some((group) => !group.pos) || content.directPoses.some((pos) => !pos.pos)) {
      errors.push('ყველა გრამატიკულ ჯგუფს უნდა ჰქონდეს არჩეული POS.');
    }
    if (groups.some((group) => !group.senses.length) || content.directPoses.some((pos) => !pos.senses.length)) {
      errors.push('გამოქვეყნებისთვის საჭიროა მინიმუმ ერთი მნიშვნელობა.');
    }
    const senses = [
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
    if (role === 'SuperAdmin' || role === 'ChiefEditor') return true;
    if (role === 'AssistantEditor') return normalized === 'NewEntry';
    if (role === 'ZhEditor') return normalized === 'ZhReview';
    return role === 'KaEditor' && normalized === 'KaReview';
  }

  private fallbackTransitions(status: string): string[] {
    const role = this.auth.editor()?.role;
    const current = this.normalizeStatus(status);
    const all = ['NewEntry', 'ZhReview', 'KaReview', 'Ready', 'Published', 'Archived'];
    if (role === 'SuperAdmin' || role === 'ChiefEditor')
      return all.filter((item) => item !== current);
    if (role === 'ZhEditor' && current === 'NewEntry') return ['ZhReview'];
    if (role === 'ZhEditor' && current === 'ZhReview') return ['KaReview'];
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

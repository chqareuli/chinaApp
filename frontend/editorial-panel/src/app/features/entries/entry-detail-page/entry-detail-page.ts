import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { CommentSummary, ReferenceMaterialDetail } from '../../../core/models/collaboration.models';
import {
  ApiProblem,
  EntryDetail,
  EntryHistoryItem,
  Homonym,
  SaveEntryContentRequest,
} from '../../../core/models/entry.models';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { EntriesService } from '../../../core/services/entries.service';
import { StatusBadge } from '../../../shared/status-badge/status-badge';

type EntryTab = 'content' | 'comments' | 'reference' | 'history';

@Component({
  selector: 'app-entry-detail-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink, StatusBadge],
  templateUrl: './entry-detail-page.html',
  styleUrl: './entry-detail-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly entriesService = inject(EntriesService);
  private readonly collaboration = inject(CollaborationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  private entryId = 0;
  readonly entry = signal<EntryDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeTab = signal<EntryTab>('content');
  readonly saving = signal(false);
  readonly saveSuccess = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly concurrencyConflict = signal(false);
  readonly changingStatus = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);

  readonly comments = signal<CommentSummary[]>([]);
  readonly commentsLoading = signal(false);
  readonly commentText = signal('');
  readonly commentSaving = signal(false);
  readonly editingCommentId = signal<number | null>(null);
  readonly editingCommentText = signal('');
  readonly commentError = signal<string | null>(null);

  readonly referenceLoading = signal(false);
  readonly referenceSaving = signal(false);
  readonly referenceError = signal<string | null>(null);

  readonly history = signal<EntryHistoryItem[]>([]);
  readonly historyLoading = signal(false);
  readonly historyError = signal<string | null>(null);

  readonly entryForm = this.fb.group({
    lemma: ['', [Validators.required, Validators.maxLength(255)]],
    pinyin: [''],
    homonyms: this.fb.array<FormGroup>([]),
  });

  readonly referenceForm = this.fb.group({
    g1: [''],
    g2: [''],
    g3: [''],
    g4: [''],
    g5: [''],
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

  ngOnInit(): void {
    this.entryId = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(this.entryId) || this.entryId < 1) {
      this.error.set('სტატიის ნომერი არასწორია.');
      this.loading.set(false);
      return;
    }

    this.loadEntry();
    this.loadComments();
    this.loadReferenceMaterial();
    this.loadHistory();
  }

  setTab(tab: EntryTab): void {
    this.activeTab.set(tab);
  }

  sensesAt(homonymIndex: number): FormArray<FormGroup> {
    return this.homonyms.at(homonymIndex).get('senses') as FormArray<FormGroup>;
  }

  examplesAt(homonymIndex: number, senseIndex: number): FormArray<FormGroup> {
    return this.sensesAt(homonymIndex).at(senseIndex).get('examples') as FormArray<FormGroup>;
  }

  addHomonym(): void {
    this.homonyms.push(this.createHomonym());
    this.entryForm.markAsDirty();
  }

  removeHomonym(index: number): void {
    this.homonyms.removeAt(index);
    this.entryForm.markAsDirty();
  }

  addSense(homonymIndex: number): void {
    const senses = this.sensesAt(homonymIndex);
    senses.push(this.createSense(senses.length + 1));
    this.entryForm.markAsDirty();
  }

  removeSense(homonymIndex: number, senseIndex: number): void {
    const senses = this.sensesAt(homonymIndex);
    senses.removeAt(senseIndex);
    this.renumberSenses(senses);
    this.entryForm.markAsDirty();
  }

  addExample(homonymIndex: number, senseIndex: number): void {
    this.examplesAt(homonymIndex, senseIndex).push(this.createExample());
    this.entryForm.markAsDirty();
  }

  removeExample(homonymIndex: number, senseIndex: number, exampleIndex: number): void {
    this.examplesAt(homonymIndex, senseIndex).removeAt(exampleIndex);
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

  archiveComment(id: number): void {
    this.collaboration.archiveComment(id).subscribe({
      next: () => this.comments.update((comments) => comments.filter((item) => item.id !== id)),
      error: (error: HttpErrorResponse) =>
        this.commentError.set(this.problemMessage(error, 'კომენტარის არქივირება ვერ მოხერხდა.')),
    });
  }

  canEditComment(comment: CommentSummary): boolean {
    return comment.authorEditorId === this.auth.editor()?.id || this.canManageComments();
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

  private populateEntryForm(entry: EntryDetail): void {
    this.entryForm.controls.lemma.setValue(entry.lemma);
    this.entryForm.controls.pinyin.setValue(entry.pinyin ?? '');
    this.homonyms.clear();
    for (const homonym of entry.homonyms) this.homonyms.push(this.createHomonym(homonym));
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
      senses: this.fb.array<FormGroup>(
        (homonym?.senses ?? []).map((sense) =>
          this.fb.group({
            number: [sense.number],
            definition: [sense.definition],
            examples: this.fb.array<FormGroup>(
              sense.examples.map((example) =>
                this.fb.group({ zh: [example.zh], ka: [example.ka] }),
              ),
            ),
          }),
        ),
      ),
    });
  }

  private createSense(number: number): FormGroup {
    return this.fb.group({
      number: [number],
      definition: [''],
      examples: this.fb.array<FormGroup>([]),
    });
  }

  private createExample(): FormGroup {
    return this.fb.group({ zh: [''], ka: [''] });
  }

  private renumberSenses(senses: FormArray<FormGroup>): void {
    senses.controls.forEach((sense, index) => sense.get('number')?.setValue(index + 1));
  }

  private toSaveRequest(rowVersion: number): SaveEntryContentRequest {
    const raw = this.entryForm.getRawValue();
    const homonyms = (raw.homonyms ?? []) as Array<{
      senses?: Array<{
        definition?: string | null;
        examples?: Array<{ zh?: string | null; ka?: string | null }>;
      }>;
    }>;
    return {
      lemma: raw.lemma?.trim() ?? '',
      pinyin: raw.pinyin?.trim() || null,
      rowVersion,
      homonyms: homonyms.map((homonym) => ({
        senses: (homonym.senses ?? []).map((sense, index) => ({
          number: index + 1,
          definition: sense.definition?.trim() ?? '',
          examples: (sense.examples ?? []).map((example) => ({
            zh: example.zh?.trim() ?? '',
            ka: example.ka?.trim() ?? '',
          })),
        })),
      })),
    };
  }

  private publishValidationErrors(targetStatus: string): string[] {
    if (this.normalizeStatus(targetStatus) !== 'Published') return [];
    const content = this.toSaveRequest(this.entry()?.rowVersion ?? 0);
    const errors: string[] = [];
    if (!content.lemma) errors.push('გამოქვეყნებისთვის სათაური აუცილებელია.');
    if (!content.homonyms.length || content.homonyms.some((homonym) => !homonym.senses.length)) {
      errors.push('გამოქვეყნებისთვის საჭიროა მინიმუმ ერთი მნიშვნელობა.');
    }
    if (content.homonyms.some((homonym) => homonym.senses.some((sense) => !sense.definition))) {
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

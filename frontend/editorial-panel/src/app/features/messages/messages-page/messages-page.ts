import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { EditorListItem } from '../../../core/models/admin.models';
import { editorRoleLabels } from '../../../core/models/auth.models';
import { DirectMessageSummary } from '../../../core/models/collaboration.models';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { EditorsService } from '../../../core/services/editors.service';
import { UnreadActivityService } from '../../../core/services/unread-activity.service';
import { LinkedMessageBody } from '../../../shared/linked-message-body/linked-message-body';

@Component({
  selector: 'app-messages-page',
  imports: [DatePipe, LinkedMessageBody],
  templateUrl: './messages-page.html',
  styleUrl: './messages-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessagesPage implements OnInit {
  private readonly collaboration = inject(CollaborationService);
  private readonly editorsService = inject(EditorsService);
  private readonly auth = inject(AuthService);
  private readonly unreadActivity = inject(UnreadActivityService);
  readonly editors = signal<EditorListItem[]>([]);
  readonly messages = signal<DirectMessageSummary[]>([]);
  readonly selectedEditorId = signal<number | null>(null);
  readonly body = signal('');
  readonly newMessageOpen = signal(false);
  readonly newRecipientId = signal<number | null>(null);
  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentEditorId = computed(() => this.auth.editor()?.id ?? 0);
  readonly availableEditors = computed(() =>
    this.editors().filter((e) => e.id !== this.currentEditorId() && e.isActive),
  );
  readonly selectedEditor = computed(
    () => this.editors().find((e) => e.id === this.selectedEditorId()) ?? null,
  );
  readonly thread = computed(() => {
    const other = this.selectedEditorId(),
      me = this.currentEditorId();
    return this.messages()
      .filter(
        (m) =>
          (m.senderEditorId === me && m.recipientEditorId === other) ||
          (m.senderEditorId === other && m.recipientEditorId === me),
      )
      .sort((a, b) => Date.parse(a.sentAtUtc) - Date.parse(b.sentAtUtc));
  });
  ngOnInit(): void {
    forkJoin({ editors: this.editorsService.list(), messages: this.collaboration.getMessages() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ editors, messages }) => {
          this.editors.set(editors);
          this.messages.set(messages);
          const first =
            this.conversationEditorIds(messages)[0] ??
            editors.find((e) => e.id !== this.currentEditorId() && e.isActive)?.id;
          this.selectedEditorId.set(first ?? null);
          if (first) this.markThreadRead(first);
        },
        error: () => this.error.set('წერილების ჩატვირთვა ვერ მოხერხდა.'),
      });
  }
  selectEditor(id: number): void {
    this.selectedEditorId.set(id);
    this.markThreadRead(id);
  }
  openNewMessage(): void {
    this.newRecipientId.set(null);
    this.newMessageOpen.set(true);
  }
  updateNewRecipient(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    this.newRecipientId.set(value || null);
  }
  startNewMessage(): void {
    const recipientId = this.newRecipientId();
    if (!recipientId) return;
    this.selectEditor(recipientId);
    this.newMessageOpen.set(false);
  }
  roleLabel(editor: EditorListItem): string {
    return editorRoleLabels[editor.role];
  }
  updateBody(event: Event): void {
    this.body.set((event.target as HTMLTextAreaElement).value);
  }
  send(): void {
    const recipient = this.selectedEditorId(),
      body = this.body().trim();
    if (!recipient || !body || this.sending()) return;
    this.sending.set(true);
    this.collaboration
      .sendMessage(recipient, body)
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: (message) => {
          this.messages.update((items) => [...items, message]);
          this.body.set('');
        },
        error: () => this.error.set('წერილის გაგზავნა ვერ მოხერხდა.'),
      });
  }
  lastMessage(editorId: number): DirectMessageSummary | undefined {
    return [...this.messages()]
      .filter((m) => m.senderEditorId === editorId || m.recipientEditorId === editorId)
      .sort((a, b) => Date.parse(b.sentAtUtc) - Date.parse(a.sentAtUtc))[0];
  }
  unreadCount(editorId: number): number {
    return this.messages().filter(
      (m) =>
        m.senderEditorId === editorId &&
        m.recipientEditorId === this.currentEditorId() &&
        !m.readAtUtc,
    ).length;
  }
  private conversationEditorIds(messages: DirectMessageSummary[]): number[] {
    const me = this.currentEditorId();
    return [
      ...new Set(
        [...messages]
          .sort((a, b) => Date.parse(b.sentAtUtc) - Date.parse(a.sentAtUtc))
          .map((m) => (m.senderEditorId === me ? m.recipientEditorId : m.senderEditorId)),
      ),
    ];
  }
  private markThreadRead(editorId: number): void {
    this.messages()
      .filter(
        (m) =>
          m.senderEditorId === editorId &&
          m.recipientEditorId === this.currentEditorId() &&
          !m.readAtUtc,
      )
      .forEach((m) =>
        this.collaboration.markMessageRead(m.id).subscribe({
          next: () =>
            this.messages.update((items) => {
              this.unreadActivity.messageRead();
              return items.map((item) =>
                item.id === m.id ? { ...item, readAtUtc: new Date().toISOString() } : item,
              );
            }),
        }),
      );
  }
}

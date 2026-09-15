import { PanelFontControl } from '../../../shared/panel-font-control';
import { PanelSearchService } from '../../../core/services/panel-search.service';
import { editorRoleCodes } from '../../../core/models/auth.models';
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
import { DirectMessageSummary } from '../../../core/models/collaboration.models';
import { AuthService } from '../../../core/services/auth.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { EditorsService } from '../../../core/services/editors.service';
import { UnreadActivityService } from '../../../core/services/unread-activity.service';
import { LinkedMessageBody } from '../../../shared/linked-message-body/linked-message-body';

@Component({
  selector: 'app-messages-page',
  imports: [DatePipe, LinkedMessageBody, PanelFontControl],
  templateUrl: './messages-page.html',
  styleUrl: './messages-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessagesPage implements OnInit {
  readonly search = inject(PanelSearchService);
  private readonly collaboration = inject(CollaborationService);
  private readonly editorsService = inject(EditorsService);
  private readonly auth = inject(AuthService);
  private readonly unreadActivity = inject(UnreadActivityService);
  private readonly reading = new Set<number>();
  readonly roleCodes = editorRoleCodes;
  readonly unreadOnly = signal(false);
  readonly showGroup = computed(
    () =>
      (!this.unreadOnly() || this.unreadCount(0) > 0) &&
      this.search.matches(
        'All editors · 全体编辑',
        ...this.messages()
          .filter((m) => m.broadcastId)
          .map((m) => m.body),
      ),
  );
  senderRole(id: number): string {
    const editor = this.editors().find((e) => e.id === id);
    return editor ? this.roleCodes[editor.editorialRole || editor.role] : '';
  }
  readonly visibleEditors = computed(() =>
    this.editors()
      .filter(
        (e) =>
          e.id !== this.currentEditorId() &&
          (this.lastMessage(e.id) || e.id === this.selectedEditorId()),
      )
      .filter(
        (editor) =>
          (!this.unreadOnly() || this.unreadCount(editor.id) > 0) &&
          this.search.matches(
            editor.displayName,
            ...this.messages()
              .filter(
                (m) =>
                  !m.broadcastId &&
                  (m.senderEditorId === editor.id || m.recipientEditorId === editor.id),
              )
              .map((m) => m.body),
          ),
      )
      .sort(
        (a, b) =>
          Date.parse(this.lastMessage(b.id)?.sentAtUtc ?? '1970-01-01') -
          Date.parse(this.lastMessage(a.id)?.sentAtUtc ?? '1970-01-01'),
      ),
  );
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
  readonly selectedEditor = computed(() =>
    this.selectedEditorId() === 0
      ? ({ id: 0, displayName: 'All editors · 全体编辑' } as EditorListItem)
      : (this.editors().find((e) => e.id === this.selectedEditorId()) ?? null),
  );
  readonly thread = computed(() => {
    const other = this.selectedEditorId(),
      me = this.currentEditorId();
    return this.messages()
      .filter((m) =>
        other === 0
          ? !!m.broadcastId
          : !m.broadcastId &&
            ((m.senderEditorId === me && m.recipientEditorId === other) ||
              (m.senderEditorId === other && m.recipientEditorId === me)),
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
          if (first !== undefined) this.markThreadRead(first);
        },
        error: () => this.error.set('Could not load messages. · 无法加载消息。'),
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
    const raw = (event.target as HTMLSelectElement).value;
    this.newRecipientId.set(raw === '' ? null : Number(raw));
    this.startNewMessage();
  }
  startNewMessage(): void {
    const recipientId = this.newRecipientId();
    if (recipientId === null) return;
    this.selectEditor(recipientId);
    this.newMessageOpen.set(false);
  }
  updateBody(event: Event): void {
    this.body.set((event.target as HTMLTextAreaElement).value);
  }
  send(): void {
    const recipient = this.selectedEditorId(),
      body = this.body().trim();
    if (recipient === null || !body || this.sending()) return;
    this.sending.set(true);
    this.collaboration
      .sendMessage(recipient, body)
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: (message) => {
          this.messages.update((items) => [...items, message]);
          this.body.set('');
        },
        error: () => this.error.set('Could not send message. · 无法发送消息。'),
      });
  }
  lastMessage(editorId: number): DirectMessageSummary | undefined {
    return [...this.messages()]
      .filter((m) =>
        editorId === 0
          ? !!m.broadcastId
          : !m.broadcastId && (m.senderEditorId === editorId || m.recipientEditorId === editorId),
      )
      .sort((a, b) => Date.parse(b.sentAtUtc) - Date.parse(a.sentAtUtc))[0];
  }
  unreadCount(editorId: number): number {
    return this.messages().filter(
      (m) =>
        (editorId === 0 ? !!m.broadcastId : !m.broadcastId && m.senderEditorId === editorId) &&
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
          .map((m) =>
            m.broadcastId ? 0 : m.senderEditorId === me ? m.recipientEditorId : m.senderEditorId,
          ),
      ),
    ];
  }
  private markThreadRead(editorId: number): void {
    this.messages()
      .filter(
        (m) =>
          (editorId === 0 ? !!m.broadcastId : !m.broadcastId && m.senderEditorId === editorId) &&
          m.recipientEditorId === this.currentEditorId() &&
          !m.readAtUtc &&
          !this.reading.has(m.id),
      )
      .forEach((m) => {
        this.reading.add(m.id);
        this.collaboration
          .markMessageRead(m.id)
          .pipe(finalize(() => this.reading.delete(m.id)))
          .subscribe({
            next: () =>
              this.messages.update((items) => {
                this.unreadActivity.messageRead();
                return items.map((item) =>
                  item.id === m.id ? { ...item, readAtUtc: new Date().toISOString() } : item,
                );
              }),
            error: () =>
              this.error.set('Could not mark messages as read. · 无法将消息标记为已读。'),
          });
      });
  }
}

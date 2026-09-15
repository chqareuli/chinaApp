import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MessagesPage } from './messages-page';
import { AuthService } from '../../../core/services/auth.service';
import { EditorsService } from '../../../core/services/editors.service';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { UnreadActivityService } from '../../../core/services/unread-activity.service';

describe('Messages handoff behavior', () => {
  const base = {
    id: 1,
    senderEditorId: 2,
    senderDisplayName: 'Editor',
    recipientEditorId: 1,
    recipientDisplayName: 'Me',
    body: 'Private',
    sentAtUtc: '2026-09-15T10:00:00Z',
    readAtUtc: null,
  };
  let service: {
    getMessages: ReturnType<typeof vi.fn>;
    markMessageRead: ReturnType<typeof vi.fn>;
    sendMessage: ReturnType<typeof vi.fn>;
  };
  beforeEach(() => {
    service = {
      getMessages: vi.fn(() =>
        of([base, { ...base, id: 2, body: 'Shared', broadcastId: 'group' }]),
      ),
      markMessageRead: vi.fn(() => of(undefined)),
      sendMessage: vi.fn(() => of({ ...base, id: 3, senderEditorId: 1, broadcastId: 'sent' })),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { editor: signal({ id: 1 }) } },
        {
          provide: EditorsService,
          useValue: {
            list: () => of([{ id: 2, displayName: 'Editor', role: 'Editor', isActive: true }]),
          },
        },
        { provide: CollaborationService, useValue: service },
        { provide: UnreadActivityService, useValue: { messageRead: vi.fn() } },
      ],
    });
  });
  it('keeps shared messages outside private threads and private unread counts', () => {
    const page = TestBed.createComponent(MessagesPage).componentInstance;
    page.messages.set([base, { ...base, id: 2, broadcastId: 'group' }]);
    page.selectedEditorId.set(2);
    expect(page.thread().map((m) => m.id)).toEqual([1]);
    expect(page.unreadCount(2)).toBe(1);
    page.selectEditor(0);
    expect(page.thread().map((m) => m.id)).toEqual([2]);
    expect(service.markMessageRead).toHaveBeenCalledWith(2);
    expect(page.unreadCount(2)).toBe(1);
  });
  it('opens the selected recipient immediately and sends group messages through the real service contract', () => {
    const page = TestBed.createComponent(MessagesPage).componentInstance;
    page.openNewMessage();
    page.updateNewRecipient({ target: { value: '0' } } as unknown as Event);
    expect(page.selectedEditorId()).toBe(0);
    expect(page.newMessageOpen()).toBe(false);
    page.body.set(' Hello group ');
    page.send();
    expect(service.sendMessage).toHaveBeenCalledWith(0, 'Hello group');
    expect(page.body()).toBe('');
  });
});

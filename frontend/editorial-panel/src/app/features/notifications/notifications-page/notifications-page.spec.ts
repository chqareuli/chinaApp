import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { NotificationsPage } from './notifications-page';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { UnreadActivityService } from '../../../core/services/unread-activity.service';
import { PanelSearchService } from '../../../core/services/panel-search.service';

describe('Notification quick view', () => {
  it('marks once, keeps the preview, filters unread and searches real details', () => {
    const read = vi.fn(() => of(undefined));
    TestBed.configureTestingModule({
      providers: [
        { provide: CollaborationService, useValue: { markNotificationRead: read } },
        { provide: UnreadActivityService, useValue: { notificationRead: vi.fn() } },
      ],
    });
    const page = TestBed.createComponent(NotificationsPage).componentInstance;
    const item = {
      id: 1,
      entryId: 2,
      entryLemma: '词',
      triggeredByEditorId: 3,
      triggeredByDisplayName: 'Editor',
      type: 'CommentAdded',
      detail: 'Review example',
      isRead: false,
      createdAtUtc: '2026-09-15T00:00:00Z',
    };
    page.notifications.set([item]);
    TestBed.inject(PanelSearchService).query.set('example');
    expect(page.visibleNotifications().length).toBe(1);
    page.unreadOnly.set(true);
    page.select(item);
    expect(page.notifications()).toEqual([]);
    expect(page.selected()?.isRead).toBe(true);
    page.select(page.selected()!);
    expect(read).toHaveBeenCalledTimes(1);
    expect(page.typeLabel('CommentAdded')).toBe('Comment added · 已添加评论');
    expect(page.detailLabel(item)).toBe('Review example');
  });
});

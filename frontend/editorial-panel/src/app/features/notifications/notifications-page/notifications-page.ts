import { PanelFontControl } from '../../../shared/panel-font-control';
import { editorRoleCodes } from '../../../core/models/auth.models';
import { getStatusMeta } from '../../../shared/status-badge/status-badge';
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
import { PanelSearchService } from '../../../core/services/panel-search.service';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { NotificationSummary } from '../../../core/models/collaboration.models';
import { CollaborationService } from '../../../core/services/collaboration.service';
import { UnreadActivityService } from '../../../core/services/unread-activity.service';

@Component({
  selector: 'app-notifications-page',
  imports: [DatePipe, RouterLink, PanelFontControl],
  templateUrl: './notifications-page.html',
  styleUrl: './notifications-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPage implements OnInit {
  private readonly search = inject(PanelSearchService);
  readonly visibleNotifications = computed(() =>
    this.notifications().filter((n) =>
      this.search.matches(n.entryLemma, n.triggeredByDisplayName, this.typeLabel(n.type), n.detail),
    ),
  );
  readonly roleCodes = editorRoleCodes;
  readonly statusMeta = getStatusMeta;
  detailLabel(item: NotificationSummary): string {
    return item.type === 'StatusChanged'
      ? (item.detail ?? '')
          .split(' → ')
          .map((s) => getStatusMeta(s).label)
          .join(' → ')
      : (item.detail ?? '');
  }
  stageClass(status = ''): string {
    return (
      (
        {
          ZhReview: 'cge-stage-cht',
          KaReview: 'cge-stage-gel',
          Ready: 'cge-stage-ready',
          Published: 'cge-stage-ready',
        } as Record<string, string>
      )[status] ?? ''
    );
  }
  private readonly collaboration = inject(CollaborationService);
  private readonly unreadActivity = inject(UnreadActivityService);
  readonly selected = signal<NotificationSummary | null>(null);
  private readonly reading = new Set<number>();
  private loadVersion = 0;
  select(notification: NotificationSummary): void {
    this.selected.set(notification);
    this.markRead(notification);
  }
  readonly notifications = signal<NotificationSummary[]>([]);
  readonly loading = signal(true);
  readonly unreadOnly = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  toggleUnread(): void {
    this.unreadOnly.update((value) => !value);
    this.load();
  }

  markRead(notification: NotificationSummary): void {
    if (notification.isRead || this.reading.has(notification.id)) return;
    this.reading.add(notification.id);
    this.collaboration
      .markNotificationRead(notification.id)
      .pipe(finalize(() => this.reading.delete(notification.id)))
      .subscribe({
        next: () => {
          this.notifications.update((items) =>
            this.unreadOnly()
              ? items.filter((item) => item.id !== notification.id)
              : items.map((item) =>
                  item.id === notification.id ? { ...item, isRead: true } : item,
                ),
          );
          this.unreadActivity.notificationRead();
          if (this.selected()?.id === notification.id)
            this.selected.set({ ...notification, isRead: true });
        },
        error: () =>
          this.error.set('Could not mark notification as read. · 无法将通知标记为已读。'),
      });
  }

  typeLabel(type: string): string {
    return (
      (
        {
          ContentEdited: 'Entry text updated · 词目内容已修改',
          StatusChanged: 'Status changed · 状态已更改',
          CommentAdded: 'Comment added · 已添加评论',
          ReferenceMaterialUpdated: 'Reference material updated · 参考资料已修改',
        } as Record<string, string>
      )[type] ?? 'Entry updated · 词目已更新'
    );
  }

  private load(): void {
    const version = ++this.loadVersion;
    this.loading.set(true);
    this.error.set(null);
    this.collaboration
      .getNotifications(this.unreadOnly())
      .pipe(
        finalize(() => {
          if (version === this.loadVersion) this.loading.set(false);
        }),
      )
      .subscribe({
        next: (items) => {
          if (version !== this.loadVersion) return;
          this.notifications.set(items);
          if (this.selected() && !items.some((i) => i.id === this.selected()?.id))
            this.selected.set(null);
        },
        error: (_: HttpErrorResponse) =>
          this.error.set('Could not load notifications. · 无法加载通知。'),
      });
  }
}

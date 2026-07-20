import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { NotificationSummary } from '../../../core/models/collaboration.models';
import { CollaborationService } from '../../../core/services/collaboration.service';

@Component({
  selector: 'app-notifications-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './notifications-page.html',
  styleUrl: './notifications-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsPage implements OnInit {
  private readonly collaboration = inject(CollaborationService);
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
    if (notification.isRead) return;
    this.collaboration.markNotificationRead(notification.id).subscribe({
      next: () =>
        this.notifications.update((items) =>
          items.map((item) => (item.id === notification.id ? { ...item, isRead: true } : item)),
        ),
    });
  }

  typeLabel(type: string): string {
    return (
      (
        {
          ContentEdited: 'სტატიის შინაარსი შეიცვალა',
          StatusChanged: 'სტატუსი შეიცვალა',
          CommentAdded: 'კომენტარი დაემატა',
        } as Record<string, string>
      )[type] ?? 'სტატიაზე ცვლილება მოხდა'
    );
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.collaboration
      .getNotifications(this.unreadOnly())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.notifications.set(items),
        error: (_: HttpErrorResponse) => this.error.set('ნოტიფიკაციების ჩატვირთვა ვერ მოხერხდა.'),
      });
  }
}

import { inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, Subscription, switchMap, timer } from 'rxjs';
import { CollaborationService } from './collaboration.service';

@Injectable({ providedIn: 'root' })
export class UnreadActivityService {
  private readonly collaboration = inject(CollaborationService);
  private polling?: Subscription;

  readonly notifications = signal(0);
  readonly messages = signal(0);

  startPolling(): void {
    if (this.polling) return;

    this.polling = timer(0, 15_000)
      .pipe(switchMap(() => this.collaboration.getUnreadActivity().pipe(catchError(() => EMPTY))))
      .subscribe((counts) => {
        this.notifications.set(counts.notifications);
        this.messages.set(counts.messages);
      });
  }

  stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = undefined;
  }

  refresh(): void {
    this.collaboration
      .getUnreadActivity()
      .pipe(catchError(() => EMPTY))
      .subscribe((counts) => {
        this.notifications.set(counts.notifications);
        this.messages.set(counts.messages);
      });
  }

  notificationRead(): void {
    this.notifications.update((count) => Math.max(0, count - 1));
  }

  messageRead(): void {
    this.messages.update((count) => Math.max(0, count - 1));
  }
}

import { inject, Injectable, signal } from '@angular/core';
import {
  catchError,
  EMPTY,
  Subscription,
  switchMap,
  timer,
  defer,
  filter,
  tap,
  ignoreElements,
} from 'rxjs';
import { CollaborationService } from './collaboration.service';

@Injectable({ providedIn: 'root' })
export class UnreadActivityService {
  private readonly collaboration = inject(CollaborationService);
  private polling?: Subscription;
  private revision = 0;
  private readCounts() {
    return defer(() => {
      const revision = ++this.revision;
      return this.collaboration.getUnreadActivity().pipe(
        filter(() => revision === this.revision),
        tap((counts) => {
          this.notifications.set(counts.notifications);
          this.messages.set(counts.messages);
        }),
        catchError(() => EMPTY),
        ignoreElements(),
      );
    });
  }

  readonly notifications = signal(0);
  readonly messages = signal(0);

  startPolling(): void {
    if (this.polling) return;

    this.polling = timer(0, 15_000)
      .pipe(switchMap(() => this.readCounts()))
      .subscribe();
  }

  stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = undefined;
  }

  refresh(): void {
    this.readCounts().subscribe();
  }

  notificationRead(): void {
    ++this.revision;
    this.notifications.update((count) => Math.max(0, count - 1));
    this.refresh();
  }

  messageRead(): void {
    ++this.revision;
    this.messages.update((count) => Math.max(0, count - 1));
    this.refresh();
  }
}

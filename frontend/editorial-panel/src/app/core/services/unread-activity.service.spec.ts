import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { UnreadActivityService } from './unread-activity.service';
import { CollaborationService } from './collaboration.service';
describe('UnreadActivityService request ordering', () => {
  it('does not restore a stale unread badge after marking a notification read', () => {
    const pending: Subject<{ notifications: number; messages: number }>[] = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: CollaborationService,
          useValue: {
            getUnreadActivity: () => {
              const request = new Subject<{ notifications: number; messages: number }>();
              pending.push(request);
              return request;
            },
          },
        },
      ],
    });
    const service = TestBed.inject(UnreadActivityService);
    service.notifications.set(1);
    service.refresh();
    service.notificationRead();
    pending[0].next({ notifications: 1, messages: 0 });
    expect(service.notifications()).toBe(0);
    pending[1].next({ notifications: 0, messages: 0 });
    expect(service.notifications()).toBe(0);
  });
});

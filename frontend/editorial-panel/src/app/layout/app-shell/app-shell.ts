import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  of,
  Subject,
  switchMap,
} from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PanelIcon } from '../../shared/panel-icon/panel-icon';
import { PanelAppearance } from '../../shared/panel-font-control';
import { EntriesService } from '../../core/services/entries.service';
import { EntrySummary } from '../../core/models/entry.models';
import { editorRoleCodes } from '../../core/models/auth.models';
import { editorRoleLabels } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { PanelSearchService } from '../../core/services/panel-search.service';
import { UnreadActivityService } from '../../core/services/unread-activity.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, PanelIcon],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShell implements OnInit, OnDestroy {
  private readonly panelSearch = inject(PanelSearchService);
  private isCollaborationPage(): boolean {
    return /\/adminpanel\/(messages|notifications)(?:\?|$)/.test(this.router.url);
  }
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly unreadActivity = inject(UnreadActivityService);

  readonly appearance = inject(PanelAppearance);
  private readonly entriesService = inject(EntriesService);
  private readonly searchQueries = new Subject<string>();
  readonly searchResults = signal<EntrySummary[]>([]);
  readonly roleCode = computed(() => editorRoleCodes[this.editor()?.role ?? 'Editor']);
  constructor() {
    this.searchQueries
      .pipe(
        debounceTime(240),
        distinctUntilChanged(),
        switchMap((query) =>
          /[\p{Script=Han}]/u.test(query) || query.length >= 2
            ? this.entriesService.search(query).pipe(catchError(() => of([])))
            : of([]),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.searchResults.set(items));
  }
  searchChanged(query: string): void {
    this.panelSearch.query.set(query);
    if (this.isCollaborationPage()) {
      this.searchResults.set([]);
      return;
    }
    this.searchQueries.next(query.trim());
  }
  search(event: Event, query: string): void {
    event.preventDefault();
    if (this.isCollaborationPage()) {
      this.panelSearch.query.set(query);
      this.searchResults.set([]);
      return;
    }
    this.searchResults.set([]);
    void this.router.navigate(['/adminpanel/entries'], { queryParams: { q: query.trim() } });
  }
  readonly editor = this.auth.editor;
  readonly menuOpen = signal(false);
  readonly loggingOut = signal(false);
  readonly roleLabel = computed(() => {
    const role = this.editor()?.role;
    return role ? editorRoleLabels[role] : '';
  });
  readonly initials = computed(() => {
    const name = this.editor()?.displayName ?? '';
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  });
  readonly unreadNotifications = this.unreadActivity.notifications;
  readonly unreadMessages = this.unreadActivity.messages;
  readonly canCreateEntry = computed(() => {
    const role = this.editor()?.role;
    return (
      role === 'SuperAdmin' ||
      role === 'ChiefEditor' ||
      role === 'DeputyChiefEditor' ||
      role === 'Editor' ||
      role === 'AssistantEditor'
    );
  });
  readonly isSuperAdmin = computed(() => this.editor()?.role === 'SuperAdmin');

  ngOnInit(): void {
    this.unreadActivity.startPolling();
  }

  ngOnDestroy(): void {
    this.unreadActivity.stopPolling();
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  logout(): void {
    this.loggingOut.set(true);
    this.auth
      .logout()
      .pipe(finalize(() => this.loggingOut.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/adminpanel']),
        error: () => void this.router.navigate(['/adminpanel']),
      });
  }
}

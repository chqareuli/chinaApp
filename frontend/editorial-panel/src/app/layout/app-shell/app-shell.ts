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
import { finalize } from 'rxjs';
import { editorRoleLabels } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { UnreadActivityService } from '../../core/services/unread-activity.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShell implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly unreadActivity = inject(UnreadActivityService);

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
    return role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'AssistantEditor';
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

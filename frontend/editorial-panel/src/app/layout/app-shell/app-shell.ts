import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { finalize } from 'rxjs';
import { editorRoleLabels } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { CollaborationService } from '../../core/services/collaboration.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppShell implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly collaboration = inject(CollaborationService);

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
  readonly unreadNotifications = signal(0);
  readonly canCreateEntry = computed(() => {
    const role = this.editor()?.role;
    return role === 'SuperAdmin' || role === 'ChiefEditor' || role === 'AssistantEditor';
  });
  readonly isSuperAdmin = computed(() => this.editor()?.role === 'SuperAdmin');

  ngOnInit(): void {
    this.collaboration.getNotifications(true).subscribe({
      next: (items) => this.unreadNotifications.set(items.length),
    });
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
        next: () => void this.router.navigate(['/login']),
        error: () => void this.router.navigate(['/login']),
      });
  }
}

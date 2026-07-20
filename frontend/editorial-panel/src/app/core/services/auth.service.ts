import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, map, Observable, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { AuthResponse, AuthSession, EditorSummary } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly storageKey = 'editorial-panel.session';
  private readonly sessionState = signal<AuthSession | null>(this.restoreSession());
  private refreshRequest: Observable<string> | null = null;

  readonly session = this.sessionState.asReadonly();
  readonly editor = computed(() => this.sessionState()?.editor ?? null);
  readonly accessToken = computed(() => this.sessionState()?.accessToken ?? null);
  readonly hasSession = computed(() => this.sessionState() !== null);
  readonly isAuthenticated = computed(() => {
    const session = this.sessionState();
    return session !== null && Date.parse(session.accessTokenExpiresAtUtc) > Date.now() + 15_000;
  });

  login(email: string, password: string): Observable<AuthSession> {
    return this.http
      .post<AuthResponse>(
        `${this.apiBaseUrl}/auth/login`,
        { email, password },
        { withCredentials: true },
      )
      .pipe(tap((session) => this.storeSession(session)));
  }

  refresh(): Observable<string> {
    if (this.refreshRequest) {
      return this.refreshRequest;
    }

    this.refreshRequest = this.http
      .post<AuthResponse>(`${this.apiBaseUrl}/auth/refresh`, {}, { withCredentials: true })
      .pipe(
        tap((session) => this.storeSession(session)),
        map((session) => session.accessToken),
        finalize(() => (this.refreshRequest = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.refreshRequest;
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${this.apiBaseUrl}/auth/logout`, {}, { withCredentials: true })
      .pipe(finalize(() => this.clearSession()));
  }

  clearSession(): void {
    this.sessionState.set(null);
    localStorage.removeItem(this.storageKey);
  }

  updateEditor(editor: EditorSummary): void {
    const session = this.sessionState();
    if (!session) return;
    this.storeSession({ ...session, editor });
  }

  private storeSession(session: AuthSession): void {
    this.sessionState.set(session);
    localStorage.setItem(this.storageKey, JSON.stringify(session));
  }

  private restoreSession(): AuthSession | null {
    const value = localStorage.getItem(this.storageKey);
    if (!value) {
      return null;
    }

    try {
      return JSON.parse(value) as AuthSession;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }
}

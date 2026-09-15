import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { AuthService } from './auth.service';

describe('Remember me', () => {
  const key = 'editorial-panel.session';
  const session = {
    accessToken: 'test-token',
    accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    editor: { id: 1, email: 'test@example.com', displayName: 'Test', role: 'Editor' },
  };
  let service: AuthService;
  let http: HttpTestingController;
  beforeEach(() => {
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    http.verify();
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
  });

  it('keeps unchecked sessions in the tab when the token is refreshed', () => {
    service.login('test@example.com', 'example', false).subscribe();
    http.expectOne('/api/auth/login').flush(session);
    service.refresh().subscribe();
    http.expectOne('/api/auth/refresh').flush({ ...session, accessToken: 'refreshed' });
    expect(localStorage.getItem(key)).toBeNull();
    expect(JSON.parse(sessionStorage.getItem(key)!).accessToken).toBe('refreshed');
  });
  it('preserves remembered sessions and clears both storage locations at logout', () => {
    service.login('test@example.com', 'example', true).subscribe();
    http.expectOne('/api/auth/login').flush(session);
    expect(localStorage.getItem(key)).not.toBeNull();
    expect(sessionStorage.getItem(key)).toBeNull();
    service.logout().subscribe();
    http.expectOne('/api/auth/logout').flush(null);
    expect(localStorage.getItem(key)).toBeNull();
    expect(sessionStorage.getItem(key)).toBeNull();
  });
});

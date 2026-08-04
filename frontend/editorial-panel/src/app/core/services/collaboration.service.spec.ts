import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { CollaborationService } from './collaboration.service';

describe('CollaborationService support materials', () => {
  let service: CollaborationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(CollaborationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads supporting materials as an anonymous list', () => {
    service.getSupportMaterials(17).subscribe();

    const request = http.expectOne('/api/entries/17/support-materials');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('adds a language and one comment field', () => {
    service
      .addSupportMaterial(17, { language: 'Russian', commentText: 'Справочный текст' })
      .subscribe();

    const request = http.expectOne('/api/entries/17/support-materials');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      language: 'Russian',
      commentText: 'Справочный текст',
    });
    request.flush({
      id: 1,
      entryId: 17,
      language: 'Russian',
      commentText: 'Справочный текст',
      createdAtUtc: '2026-08-04T00:00:00Z',
    });
  });
});

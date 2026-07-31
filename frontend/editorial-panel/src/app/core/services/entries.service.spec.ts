import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { EntriesService } from './entries.service';

describe('EntriesService', () => {
  let service: EntriesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(EntriesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the complete article list with filters', () => {
    service
      .getAllList({
        page: 2,
        pageSize: 25,
        query: ' 中国 ',
        status: 'NewEntry',
        editorId: 7,
        sort: 'LemmaAsc',
      })
      .subscribe();

    const request = http.expectOne(
      (candidate) => candidate.url === '/api/entries/all' && candidate.params.get('page') === '2',
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('q')).toBe('中国');
    expect(request.request.params.get('status')).toBe('NewEntry');
    expect(request.request.params.get('editorId')).toBe('7');
    expect(request.request.params.get('sort')).toBe('LemmaAsc');
    request.flush({ items: [], totalCount: 0, page: 2, pageSize: 25 });
  });

  it('omits empty optional filters', () => {
    service.getAllList({ page: 1, pageSize: 25, query: ' ', sort: 'UpdatedDesc' }).subscribe();

    const request = http.expectOne('/api/entries/all?page=1&pageSize=25&sort=UpdatedDesc');
    expect(request.request.params.has('q')).toBe(false);
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('editorId')).toBe(false);
    request.flush({ items: [], totalCount: 0, page: 1, pageSize: 25 });
  });

  it('loads a controlled vocabulary category', () => {
    service.getVocabularies('pos').subscribe();

    const request = http.expectOne('/api/vocabularies?category=pos');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 1, category: 'pos', code: '名', label: '名', sortOrder: 1 }]);
  });
});

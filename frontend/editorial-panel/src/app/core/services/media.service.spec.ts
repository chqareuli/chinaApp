import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api.config';
import { MediaService } from './media.service';

describe('MediaService', () => {
  let service: MediaService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(MediaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads media attached to an entry', () => {
    service.listForEntry(17).subscribe((items) => expect(items).toEqual([]));

    const request = http.expectOne('/api/entries/17/media');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('uploads the file as multipart form data', () => {
    const file = new File(['image'], '学习.png', { type: 'image/png' });
    service.upload(17, file, 'cover').subscribe();

    const request = http.expectOne('/api/entries/17/media');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    const uploadedFile = (request.request.body as FormData).get('file') as File;
    expect(uploadedFile.name).toBe(file.name);
    expect(uploadedFile.type).toBe(file.type);
    expect(uploadedFile.size).toBe(file.size);
    expect((request.request.body as FormData).get('placement')).toBe('cover');
    request.flush({});
  });

  it('downloads protected image content through the API base URL', () => {
    service.getContent('/media/files/safe.png').subscribe();

    const request = http.expectOne('/api/media/files/safe.png');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob());
  });

  it('deletes a media asset by id', () => {
    service.delete(8).subscribe();

    const request = http.expectOne('/api/media/8');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });
});

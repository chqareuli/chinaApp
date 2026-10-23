import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { EntriesListPage } from './entries-list-page';
import { EntriesService } from '../../../core/services/entries.service';
import { EditorsService } from '../../../core/services/editors.service';
import { AuthService } from '../../../core/services/auth.service';

it('excludes drafts only on unfiltered Home and includes them in explicit filters', () => {
  const getAllList = vi.fn().mockReturnValue(of({ items: [], totalCount: 0 }));
  const params = convertToParamMap({});
  TestBed.configureTestingModule({
    providers: [
      { provide: EntriesService, useValue: { getAllList } },
      { provide: EditorsService, useValue: { list: () => of([]) } },
      { provide: AuthService, useValue: { editor: () => ({ role: 'SuperAdmin' }) } },
      { provide: Router, useValue: { navigate: vi.fn() } },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: { data: { home: true }, queryParamMap: params },
          queryParamMap: of(params),
        },
      },
    ],
  });
  const page = TestBed.runInInjectionContext(() => new EntriesListPage());
  expect(getAllList.mock.lastCall![0].excludeDrafts).toBe(true);
  page.statusControl.setValue('NewEntry');
  expect(getAllList.mock.lastCall![0]).toMatchObject({ status: 'NewEntry', excludeDrafts: false });
  page.clearFilters();
  expect(getAllList.mock.lastCall![0].excludeDrafts).toBe(true);
  page.authorControl.setValue('7');
  expect(getAllList.mock.lastCall![0]).toMatchObject({ authorId: 7, excludeDrafts: false });
});

import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { PublicDictionaryEntry } from '../../../core/models/public-dictionary.models';
import { PublicDictionaryService } from '../../../core/services/public-dictionary.service';
import { PublicDictionaryEntryPage } from './public-dictionary-entry-page';

const article = (id: number, lemma: string): PublicDictionaryEntry => ({
  id,
  lemma,
  pinyin: null,
  pinyins: [],
  complexForms: [],
  updatedAtUtc: '2026-08-03T00:00:00Z',
  homonyms: [],
  gramGroups: [],
  directPoses: [],
  senses: [],
});

class PublicDictionaryServiceStub {
  readonly requestedIds: number[] = [];

  getEntry(id: number) {
    this.requestedIds.push(id);
    return of(article(id, id === 4 ? '中国' : 'source'));
  }
}

describe('PublicDictionaryEntryPage navigation', () => {
  it('reloads the article when an XR link changes the route id', () => {
    const params = new BehaviorSubject(convertToParamMap({ id: '14' }));
    const dictionary = new PublicDictionaryServiceStub();
    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: PublicDictionaryService, useValue: dictionary },
      ],
    });
    const component = TestBed.runInInjectionContext(() => new PublicDictionaryEntryPage());
    component.ngOnInit();

    expect(component.entry()?.lemma).toBe('source');
    params.next(convertToParamMap({ id: '4' }));

    expect(dictionary.requestedIds).toEqual([14, 4]);
    expect(component.entry()?.lemma).toBe('中国');
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { EntriesService } from '../../core/services/entries.service';
import { LinkedMessageBody } from './linked-message-body';

describe('comment and message entry links', () => {
  it('uses stored identities without resolving a possibly renamed headword again', () => {
    const search = vi.fn();
    TestBed.configureTestingModule({providers:[provideRouter([]),{provide:EntriesService,useValue:{search}}]});
    const fixture=TestBed.createComponent(LinkedMessageBody);
    fixture.componentRef.setInput('body','See 中国');
    fixture.componentRef.setInput('links',[{offset:4,text:'中国',entryId:17}]);fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/adminpanel/entries/17');
    expect(search).not.toHaveBeenCalled();
  });
  it('links only an unambiguous entry identity', () => {
    TestBed.configureTestingModule({providers:[provideRouter([]),{provide:EntriesService,useValue:{search:(token:string)=>of(token === '中国' ? [{id:1,lemma:token},{id:2,lemma:token}] : [{id:3,lemma:token}])}}]});
    const fixture=TestBed.createComponent(LinkedMessageBody);
    fixture.componentRef.setInput('body','中国 和'); fixture.detectChanges();
    const links=fixture.nativeElement.querySelectorAll('a');
    expect(links.length).toBe(1); expect(links[0].getAttribute('href')).toBe('/adminpanel/entries/3');
  });
  it('does not overwrite revised text with a stale lookup result', () => {
    const pending=new Subject<any[]>();
    TestBed.configureTestingModule({providers:[provideRouter([]),{provide:EntriesService,useValue:{search:()=>pending}}]});
    const fixture=TestBed.createComponent(LinkedMessageBody);
    fixture.componentRef.setInput('body','中国');fixture.detectChanges();
    fixture.componentRef.setInput('body','updated');fixture.detectChanges();
    pending.next([{id:1,lemma:'中国'}]);pending.complete();fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toBe('updated');
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { InlineEntryText } from './inline-entry-text';

describe('InlineEntryText', () => {
  it('renders multiple images at the start, middle and end without losing formatted text', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(InlineEntryText);
    fixture.componentRef.setInput('body', 'ab');
    fixture.componentRef.setInput('markers', [{ textField: 'definition', textOffset: 0, displayText: 'ab' }]);
    fixture.componentRef.setInput('images', [0, 1, 2].map(offset => ({ offset, url: 'blob:test', alt: 'image' + offset })));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toBe('ab');
    expect(Array.from(fixture.nativeElement.children).map((x: any) => x.tagName)).toEqual(['IMG', 'EM', 'IMG', 'EM', 'IMG']);
    expect(fixture.nativeElement.querySelectorAll('img').length).toBe(3);
  });
  it('renders abbreviations in italics and links at their position without injecting HTML', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(InlineEntryText);
    fixture.componentRef.setInput('body', 'ჩვეულ. იხ. 中国 <script>');
    fixture.componentRef.setInput('markers', [{ textField: 'definition', textOffset: 0, displayText: 'ჩვეულ.' }]);
    fixture.componentRef.setInput('references', [{ placement: 'Inline', textField: 'definition', textOffset: 7, displayText: 'იხ. 中国', targetEntryId: 2 }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('em').textContent).toBe('ჩვეულ.');
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/word/2');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.textContent).toBe('ჩვეულ. იხ. 中国 <script>');
  });
  it('does not guess which repeated token to link when its saved position no longer matches', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(InlineEntryText);
    fixture.componentRef.setInput('body', 'prefix 中国 中国');
    fixture.componentRef.setInput('references', [{ placement: 'Inline', textField: 'definition', textOffset: 0, displayText: '中国', targetEntryId: 2 }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a')).toBeNull();
    expect(fixture.nativeElement.textContent).toBe('prefix 中国 中国');
  });
});

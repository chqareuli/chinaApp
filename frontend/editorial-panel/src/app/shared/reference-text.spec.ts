import { TestBed } from '@angular/core/testing';
import { ReferenceText } from './reference-text';

describe('reference material rendering', () => {
  it('preserves Chinese text and legacy bullet depth, highlights search and excludes executable markup', () => {
    const fixture = TestBed.createComponent(ReferenceText);
    fixture.componentRef.setInput('body', '<span>都</span> dōu<br>●● all<script>alert(1)</script>');
    fixture.componentRef.setInput('query', 'all'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.han').textContent).toBe('都');
    expect(fixture.nativeElement.querySelector('.legacy-bullet').textContent).toBe('●●');
    expect(fixture.nativeElement.querySelector('.search-match').textContent).toBe('all');
    expect(fixture.nativeElement.textContent).not.toContain('alert');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
  });
});

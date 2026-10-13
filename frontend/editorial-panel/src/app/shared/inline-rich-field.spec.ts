import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { InlineRichField } from './inline-rich-field';

describe('formatted lexical text editing', () => {
  it('shows inline formatting while retaining the plain text form value and cursor bridge', async () => {
    TestBed.configureTestingModule({providers:[provideRouter([])]});
    const fixture=TestBed.createComponent(InlineRichField), control=new FormControl('ჩვეულ. ტექსტი'), bridge=document.createElement('textarea');
    bridge.value=control.value!;
    fixture.componentRef.setInput('control',control);fixture.componentRef.setInput('bridge',bridge);
    fixture.componentRef.setInput('markers',[{category:'Abbr',value:'usual',textField:'definition',textOffset:0,displayText:'ჩვეულ.'}]);
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('em').textContent).toBe('ჩვეულ.');
    control.setValue('ჩვეულ. ახალი ტექსტი'); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[contenteditable]').textContent).toBe('ჩვეულ. ახალი ტექსტი');
    const editor=fixture.nativeElement.querySelector('[contenteditable]');
    editor.textContent='შეცვლილი ტექსტი'; fixture.componentInstance.readInput();
    expect(control.value).toBe('შეცვლილი ტექსტი');expect(control.dirty).toBe(true);
    expect(bridge.value).toBe('შეცვლილი ტექსტი');
    fixture.componentInstance.keyDown(new KeyboardEvent('keydown',{key:'z',ctrlKey:true}));
    expect(control.value).toBe('ჩვეულ. ახალი ტექსტი');
    fixture.componentInstance.keyDown(new KeyboardEvent('keydown',{key:'y',ctrlKey:true}));
    expect(control.value).toBe('შეცვლილი ტექსტი');
  });
});

import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { InlineRichField } from './inline-rich-field';

describe('formatted lexical text editing', () => {
  it('keeps image names out of stored text and cursor offsets, and emits removal for the selected image', async () => {
    TestBed.configureTestingModule({providers:[provideRouter([])]});
    const fixture=TestBed.createComponent(InlineRichField), control=new FormControl('ab'), bridge=document.createElement('textarea');
    bridge.value='ab';
    fixture.componentRef.setInput('control',control);fixture.componentRef.setInput('bridge',bridge);
    fixture.componentRef.setInput('images',[{id:7,offset:1,url:'blob:test',alt:'photo',label:'სურათის სახელი'}]);
    fixture.detectChanges();await fixture.whenStable();
    const editor=fixture.nativeElement.querySelector('[contenteditable="true"]') as HTMLElement;
    expect(editor.querySelector('.inline-image')?.textContent).toContain('სურათის სახელი');
    const walker=document.createTreeWalker(editor,NodeFilter.SHOW_TEXT);let last:Node | null=null;let candidate:Node | null;while((candidate=walker.nextNode())){if(candidate.textContent==='b') last=candidate;}
    const range=document.createRange();range.setStart(last!,0);range.collapse(true);
    window.getSelection()!.removeAllRanges();window.getSelection()!.addRange(range);
    fixture.componentInstance.syncSelection();expect(bridge.selectionStart).toBe(1);
    fixture.componentInstance.readInput();expect(control.value).toBe('ab');
    const removed=vi.fn();fixture.componentInstance.imageRemove.subscribe(removed);
    (editor.querySelector('[data-media-remove]') as HTMLButtonElement).click();
    expect(removed).toHaveBeenCalledWith(7);
  });
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

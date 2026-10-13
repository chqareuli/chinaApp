import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, Injector, OnChanges, OnDestroy, ViewChild, afterNextRender, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup } from '@angular/forms';
import { Subscription } from 'rxjs';
import { InlineEntryText } from './inline-entry-text';
import { CrossReference } from '../core/models/entry.models';

@Component({
  selector: 'app-inline-rich-field',
  imports: [InlineEntryText],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #editor contenteditable="true" role="textbox" aria-multiline="true" [attr.aria-label]="label()" [attr.data-placeholder]="label()" [attr.data-empty]="!body() && !images().length" (click)="editableClick($event)" (input)="readInput()" (keyup)="syncSelection()" (mouseup)="syncSelection()" (blur)="syncSelection()" (paste)="paste($event)" (keydown)="keyDown($event)" (keydown.enter)="lineBreak($event)" (drop)="$event.preventDefault()"></div><div #formatted hidden aria-hidden="true"><app-inline-entry-text [body]="body()" [images]="images()" [removable]="true" [markers]="markers()" [references]="references()" [field]="field()" route="/adminpanel/entries" /></div><dialog #imageViewer class="image-viewer" aria-label="Image preview · 图片预览" (click)="$event.target === imageViewer && imageViewer.close()" (close)="expandedImage.set(null)">@if (expandedImage(); as image) { <button class="image-close" type="button" autofocus aria-label="Close · 关闭" (click)="imageViewer.close()">×</button><img [src]="image.url" [alt]="image.alt" /> }</dialog>`,
  styles: [`:host{display:block}div{min-height:54px;white-space:pre-wrap;overflow-wrap:anywhere;outline:none;line-height:1.5}div[data-empty="true"]:before{content:attr(data-placeholder);color:#929292;pointer-events:none;position:absolute}dialog.image-viewer{border:0;border-radius:14px;padding:40px 16px 16px;max-width:94vw;max-height:94vh}dialog.image-viewer::backdrop{background:#14202bc4}dialog.image-viewer img{display:block;max-width:88vw;max-height:80vh;object-fit:contain}.image-close{position:absolute;top:5px;right:8px;border:0;background:transparent;font-size:28px;cursor:pointer}div:focus-visible{outline:2px solid #86bde9;outline-offset:4px;border-radius:4px}`],
})
export class InlineRichField implements OnChanges, AfterViewInit, OnDestroy {
  readonly images = input<{offset: number; url: string; alt: string; label?: string; id?: number}[]>([]);
  readonly imageRemove = output<number>();
  readonly expandedImage = signal<{url: string; alt: string} | null>(null);
  @ViewChild('imageViewer') private imageViewer?: ElementRef<HTMLDialogElement>;
  readonly annotationArrays = input<FormArray<FormGroup>[]>([]);
  readonly control = input.required<AbstractControl>();
  readonly value = input<string | null>(null);
  readonly bridge = input.required<HTMLTextAreaElement>();
  readonly markers = input<{textField?: string | null; textOffset?: number | null; displayText?: string | null}[]>([]);
  readonly references = input<CrossReference[]>([]);
  readonly field = input('definition');
  readonly label = input('Definition · 释义');
  readonly body = signal('');
  @ViewChild('editor') private editor?: ElementRef<HTMLDivElement>;
  @ViewChild('formatted') private formatted?: ElementRef<HTMLDivElement>;
  private readonly injector = inject(Injector);
  private observed?: AbstractControl;
  private subscription?: Subscription;
  private undoHistory: {text:string;annotations:Record<string,unknown>[][]}[] = [];
  private redoHistory: {text:string;annotations:Record<string,unknown>[][]}[] = [];
  private lastState = {text:'',annotations:[] as Record<string,unknown>[][]};
  private applyingHistory = false;
  private renderPending = false;
  private scheduleRender(): void {
    if (this.renderPending) return; this.renderPending = true;
    afterNextRender(() => {
      this.renderPending = false;
      const editor = this.editor?.nativeElement, formatted = this.formatted?.nativeElement;
      if (!editor || !formatted) return;
      if (editor.innerHTML !== formatted.innerHTML) {
        editor.replaceChildren(...Array.from(formatted.childNodes).map(node => node.cloneNode(true)));
        this.restoreSelection();
      }
      this.lastState = this.captureState();
    }, {injector:this.injector});
  }
  ngOnChanges(): void {
    this.body.set(this.value() ?? String(this.control().value || ''));
    this.scheduleRender();
    if (this.observed === this.control()) return;
    this.subscription?.unsubscribe(); this.observed = this.control();
    this.body.set(String(this.control().value || ''));
    this.lastState = this.captureState();
    this.subscription = this.control().valueChanges.subscribe(value => {
      if (!this.applyingHistory && String(value || '') !== this.body()) {
        this.undoHistory.push(this.lastState); this.redoHistory = [];
        if (this.undoHistory.length > 100) this.undoHistory.shift();
      }
      this.body.set(String(value || ''));
      this.scheduleRender();
    });
  }
  ngAfterViewInit(): void { this.scheduleRender(); }
  ngOnDestroy(): void { this.subscription?.unsubscribe(); }
  keyDown(event: KeyboardEvent): void {
    if (!(event.ctrlKey || event.metaKey) || !['z','y'].includes(event.key.toLowerCase())) return;
    event.preventDefault();
    const redo = event.key.toLowerCase() === 'y' || event.shiftKey;
    const source = redo ? this.redoHistory : this.undoHistory, target = redo ? this.undoHistory : this.redoHistory;
    const value = source.pop(); if (value === undefined) return;
    target.push(this.captureState()); this.applyingHistory = true; this.control().setValue(value.text); this.applyingHistory = false;
    const fb = new FormBuilder();
    this.annotationArrays().forEach((array,index) => { array.clear(); for (const item of value.annotations[index] || []) array.push(fb.group(Object.fromEntries(Object.entries(item).map(([key,content]) => [key,[content]])))); });
    this.control().markAsDirty(); this.bridge().value = value.text; this.bridge().setSelectionRange(value.text.length,value.text.length);
    this.lastState = this.captureState();
  }
  private captureState() { return {text:String(this.control().value || ''),annotations:this.annotationArrays().map(array => structuredClone(array.getRawValue()))}; }
  syncSelection(): void {
    const editor = this.editor?.nativeElement, selection = window.getSelection();
    if (!editor || !selection?.rangeCount || !editor.contains(selection.anchorNode) || !editor.contains(selection.focusNode)) return;
    const range = selection.getRangeAt(0), prefix = range.cloneRange();
    prefix.selectNodeContents(editor); prefix.setEnd(range.startContainer, range.startOffset);
    const start = this.plainText(prefix.cloneContents()).length;
    this.bridge().setSelectionRange(start, start + this.plainText(range.cloneContents()).length);
  }
  editableClick(event: MouseEvent): void {
    const element = event.target as Element;
    const remove = element.closest<HTMLElement>('[data-media-remove]');
    if (remove) {event.preventDefault();this.imageRemove.emit(Number(remove.dataset['mediaRemove']));return;}
    const image = element.closest<HTMLElement>('[data-image-url]');
    if (image) {event.preventDefault();this.expandedImage.set({url:image.dataset['imageUrl']!,alt:image.dataset['imageAlt'] || ''});this.imageViewer?.nativeElement.showModal();return;}
    if (element.closest?.('a')) event.preventDefault();
  }
  private plainText(node: Node): string {
    const copy = node.cloneNode(true) as HTMLElement;
    copy.querySelectorAll?.('.inline-image,dialog').forEach(item => item.remove());
    return copy.textContent || '';
  }
  readInput(): void {
    const editor = this.editor?.nativeElement; if (!editor) return;
    const value = this.plainText(editor).replace(/\r\n/g, '\n');
    this.bridge().value = value; this.syncSelection();
    const start = this.bridge().selectionStart, end = this.bridge().selectionEnd;
    this.lastState = this.captureState(); this.control().setValue(value); this.control().markAsDirty();
    this.bridge().setSelectionRange(start,end);
    this.scheduleRender();
  }
  paste(event: ClipboardEvent): void {
    event.preventDefault();
    this.insertPlainText(event.clipboardData?.getData('text/plain') || '');
  }
  lineBreak(event: Event): void { event.preventDefault(); this.insertPlainText('\n'); }
  private insertPlainText(value: string): void {
    const selection = window.getSelection(), editor = this.editor?.nativeElement;
    if (!editor || !selection?.rangeCount || !editor.contains(selection.anchorNode)) return;
    const range = selection.getRangeAt(0), text = document.createTextNode(value);
    range.deleteContents(); range.insertNode(text); range.setStartAfter(text); range.collapse(true);
    selection.removeAllRanges(); selection.addRange(range); this.readInput();
  }
  private restoreSelection(): void {
    const editor = this.editor?.nativeElement; if (!editor?.isConnected) return;
    if (!this.control().dirty && document.activeElement !== this.bridge() && !editor.contains(document.activeElement)) return;
    const range = document.createRange();
    const point = (offset: number): [Node, number] => {
      const walker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT, {acceptNode: node => node.parentElement?.closest('.inline-image,dialog') ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT}); let node: Node | null;
      while ((node = walker.nextNode())) {
        const length = node.textContent?.length || 0;
        if (offset <= length) return [node, offset];
        offset -= length;
      }
      return [editor, editor.childNodes.length];
    };
    range.setStart(...point(this.bridge().selectionStart));
    range.setEnd(...point(this.bridge().selectionEnd));
    editor.focus(); const selection = window.getSelection(); selection?.removeAllRanges(); selection?.addRange(range);
  }
}

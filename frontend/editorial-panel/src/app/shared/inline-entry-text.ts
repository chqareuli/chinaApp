import { EntrySearchHighlight, highlightRuns } from './entry-search-highlight';
import { Component, ChangeDetectionStrategy, computed, input, signal, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

type Reference = {
  placement: string;
  textField?: string | null;
  textOffset?: number | null;
  displayText: string | null;
  targetEntryId: number | null;
};
type Marker = {
  textField?: string | null;
  textOffset?: number | null;
  displayText?: string | null;
};
@Component({
  selector: 'app-inline-entry-text',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@for (part of parts(); track $index) {
      @if (part.image) {
        <span class="inline-image" contenteditable="false"
          ><button
            [attr.data-image-url]="part.image.url"
            [attr.data-image-alt]="part.image.alt"
            class="image-thumbnail"
            type="button"
            aria-haspopup="dialog"
            [title]="part.image.alt"
            [attr.aria-label]="'Enlarge image · 放大图片: ' + part.image.alt"
            (click)="expandedImage.set(part.image); viewer.showModal()"
          >
            @if (part.image.label) {
              <span>{{ part.image.label }}</span>
            }
            <svg
              width="19"
              height="19"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              aria-hidden="true"
            >
              <rect x="3" y="3" width="18" height="18" rx="3" />
              <circle cx="8" cy="8" r="1.5" />
              <path d="m3 17 5-5 4 4 4-5 5 6" />
            </svg>
          </button>
          @if (removable() && part.image.id) {
            <button
              type="button"
              class="image-remove"
              [attr.data-media-remove]="part.image.id"
              [attr.aria-label]="'Remove image · 删除图片: ' + part.image.alt"
            >
              ×
            </button>
          }
        </span>
      } @else if (part.target) {
        <a [routerLink]="[route(), part.target]">
          @if (part.highlighted) {
            <mark>{{ part.text }}</mark>
          } @else {<ng-container>{{ part.text }}</ng-container>}
        </a>
      } @else if (part.italic) {
        <em>
          @if (part.highlighted) {
            <mark>{{ part.text }}</mark>
          } @else {<ng-container>{{ part.text }}</ng-container>}
        </em>
      } @else {
        <span [class.definition-number]="part.numbered">
          @if (part.highlighted) {
            <mark>{{ part.text }}</mark>
          } @else {<ng-container>{{ part.text }}</ng-container>}
        </span>
      }
    }
    <dialog
      #viewer
      class="image-viewer"
      aria-label="Image preview · 图片预览"
      (click)="$event.target === viewer && viewer.close()"
      (close)="expandedImage.set(null)"
    >
      @if (expandedImage(); as image) {
        <button
          class="image-close"
          type="button"
          autofocus
          aria-label="Close · 关闭"
          (click)="viewer.close()"
        >
          ×
        </button>
        <img [src]="image.url" [alt]="image.alt" />
      }
    </dialog>`,
  styles: [
    ':host{white-space:pre-wrap}a{color:#286fac;text-decoration:underline}em{font-style:italic;font-weight:700;color:#5a6373}.definition-number{color:#5b59eb;font-style:normal;font-weight:500}.image-thumbnail{display:inline-flex;vertical-align:middle;align-items:center;justify-content:center;padding:2px;margin:0 4px;border:0;border-radius:4px;background:transparent;color:#3685b5;cursor:zoom-in;line-height:inherit;gap:4px;font:inherit;text-decoration:underline}.inline-image{display:inline-flex;align-items:center}.image-remove{border:0;background:transparent;color:#a24b4b;cursor:pointer;padding:0 3px;font-size:16px}.image-thumbnail:hover{border-color:#86bde9;box-shadow:0 2px 8px #00000015}.image-thumbnail img{display:block;width:auto;height:auto;max-width:min(160px,100%);max-height:120px;object-fit:contain}.image-viewer{padding:40px 16px 16px;border:0;border-radius:14px;max-width:94vw;max-height:94vh;background:#fff;box-shadow:0 12px 50px #0004}.image-viewer::backdrop{background:#14202bc4}.image-viewer img{display:block;max-width:88vw;max-height:80vh;object-fit:contain}.image-close{position:absolute;top:5px;right:8px;border:0;background:transparent;font-size:28px;cursor:pointer;line-height:1}',
  ],
})
export class InlineEntryText {
  readonly searchHighlight = inject(EntrySearchHighlight);
  readonly expandedImage = signal<{ url: string; alt: string; label?: string; id?: number } | null>(
    null,
  );
  readonly removable = input(false);
  readonly body = input('');
  readonly references = input<Reference[]>([]);
  readonly markers = input<Marker[]>([]);
  readonly images = input<
    { offset: number; url: string; alt: string; label?: string; id?: number }[]
  >([]);
  readonly field = input('definition');
  readonly route = input('/word');
  readonly parts = computed(() => {
    const body = this.body();
    const annotations = [
      ...this.references().map((item) => ({ ...item, italic: false })),
      ...this.markers().map((item) => ({
        ...item,
        placement: 'Inline',
        targetEntryId: null,
        italic: true,
      })),
    ];
    const matches = annotations
      .flatMap((reference) => {
        if (
          reference.placement !== 'Inline' ||
          reference.textField !== this.field() ||
          !reference.displayText ||
          (!reference.targetEntryId && !reference.italic)
        )
          return [];
        let offset = reference.textOffset ?? -1;
        if (body.slice(offset, offset + reference.displayText.length) !== reference.displayText) {
          offset = body.indexOf(reference.displayText);
          if (offset < 0 || body.indexOf(reference.displayText, offset + 1) >= 0) return [];
        }
        return [
          {
            offset,
            text: reference.displayText,
            target: reference.targetEntryId,
            italic: reference.italic,
          },
        ];
      })
      .sort((a, b) => a.offset - b.offset);
    const parts: {
      text: string;
      target: number | null;
      italic?: boolean;
      numbered?: boolean;
      image?: { url: string; alt: string; label?: string; id?: number };
    }[] = [];
    let cursor = 0;
    for (const match of matches) {
      if (match.offset < cursor) continue;
      parts.push({ text: body.slice(cursor, match.offset), target: null }, match);
      cursor = match.offset + match.text.length;
    }
    parts.push({ text: body.slice(cursor), target: null });
    // Split text runs at image positions without changing the saved text or losing formatting.
    const images = this.images()
      .filter((i) => Number.isInteger(i.offset) && i.offset >= 0 && i.offset <= body.length)
      .sort((a, b) => a.offset - b.offset);
    const result: typeof parts = [];
    let position = 0;
    let imageIndex = 0;
    for (const part of parts) {
      let local = 0;
      while (
        imageIndex < images.length &&
        images[imageIndex].offset <= position + part.text.length
      ) {
        const image = images[imageIndex++];
        const split = Math.max(local, image.offset - position);
        if (split > local) result.push({ ...part, text: part.text.slice(local, split) });
        result.push({ text: '', target: null, image });
        local = split;
      }
      if (local < part.text.length) result.push({ ...part, text: part.text.slice(local) });
      position += part.text.length;
    }
    if (this.field() !== 'definition') return highlightRuns(result, this.searchHighlight.query());
    return highlightRuns(
      result.flatMap((part) =>
        part.image || part.target || part.italic
          ? [part]
          : part.text
              .split(/([①-⑳])/u)
              .filter(Boolean)
              .map((text) => ({ ...part, text, numbered: /^[①-⑳]$/u.test(text) })),
      ),
      this.searchHighlight.query(),
    );
  });
}

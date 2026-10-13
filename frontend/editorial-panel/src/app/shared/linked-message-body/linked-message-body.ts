import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnChanges,
  OnDestroy,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, Subscription } from 'rxjs';
import { EntriesService } from '../../core/services/entries.service';

interface MessagePart {
  text: string;
  entryId?: number;
}

@Component({
  selector: 'app-linked-message-body',
  imports: [RouterLink],
  template: `@for (part of parts(); track $index) {
    @if (part.entryId) {
      <a [routerLink]="['/adminpanel/entries', part.entryId]" lang="zh">{{ part.text }}</a>
    } @else {
      <span>{{ part.text }}</span>
    }
  }`,
  styles: `
    :host {
      white-space: pre-wrap;
      line-height: 1.6;
    }
    a {
      color: #17649a;
      font-weight: 800;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LinkedMessageBody implements OnChanges, OnDestroy {
  private lookup?: Subscription;
  private readonly entries = inject(EntriesService);
  readonly body = input.required<string>();
  readonly links = input<{offset:number;text:string;entryId:number}[] | null>(null);
  readonly parts = signal<MessagePart[]>([]);

  ngOnChanges(): void {
    this.lookup?.unsubscribe();
    const body = this.body();
    if (this.links() !== null) {
      const parts: MessagePart[] = []; let cursor = 0;
      for (const link of [...this.links()!].sort((a,b) => a.offset-b.offset)) {
        if (link.offset < cursor || body.slice(link.offset, link.offset+link.text.length) !== link.text) continue;
        parts.push({text:body.slice(cursor,link.offset)},{text:link.text,entryId:link.entryId});
        cursor = link.offset+link.text.length;
      }
      parts.push({text:body.slice(cursor)}); this.parts.set(parts); return;
    }
    const tokens = [...new Set(body.match(/[\u3400-\u9fff]+/g) ?? [])].slice(0, 8);
    if (!tokens.length) {
      this.parts.set([{ text: body }]);
      return;
    }
    this.lookup = forkJoin(
      tokens.map((token) =>
        this.entries.search(token, 10).pipe(
          map((items) => { const exact = items.filter(item => item.lemma === token); return { token, entryId: exact.length === 1 ? exact[0].id : undefined }; }),
          catchError(() => of({ token, entryId: undefined })),
        ),
      ),
    ).subscribe((resolved) => {
      const links = new Map(
        resolved.filter((item) => item.entryId).map((item) => [item.token, item.entryId] as const),
      );
      this.parts.set(
        body
          .split(/([\u3400-\u9fff]+)/g)
          .filter(Boolean)
          .map((text) => ({ text, entryId: links.get(text) })),
      );
    });
  }
  ngOnDestroy(): void { this.lookup?.unsubscribe(); }
}

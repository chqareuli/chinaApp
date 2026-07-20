import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnChanges,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
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
export class LinkedMessageBody implements OnChanges {
  private readonly entries = inject(EntriesService);
  readonly body = input.required<string>();
  readonly parts = signal<MessagePart[]>([]);

  ngOnChanges(): void {
    const body = this.body();
    const tokens = [...new Set(body.match(/[\u3400-\u9fff]+/g) ?? [])].slice(0, 8);
    if (!tokens.length) {
      this.parts.set([{ text: body }]);
      return;
    }
    forkJoin(
      tokens.map((token) =>
        this.entries.search(token, 10).pipe(
          map((items) => ({ token, entryId: items.find((item) => item.lemma === token)?.id })),
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
}

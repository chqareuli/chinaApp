import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-reference-text',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@for (part of parts(); track $index) { <span [class.han]="part.han" [class.legacy-bullet]="part.bullet" [class.search-match]="part.match">{{ part.text }}</span> }`,
  styles: [':host{white-space:pre-wrap;line-height:1.65}.han{color:#111}.legacy-bullet{background:#d9effb;color:#287db0;border-radius:3px}.search-match{background:#ffe78a;color:#222}'],
})
export class ReferenceText {
  readonly body = input('');
  readonly query = input('');
  readonly parts = computed(() => {
    let body = this.body();
    if (/<\/?(?:span|br|p|div|ul|li|script|style)\b/i.test(body)) {
      const parsed = new DOMParser().parseFromString(body, 'text/html');
      parsed.querySelectorAll('script,style,iframe,object').forEach(node => node.remove());
      parsed.querySelectorAll('br').forEach(node => node.replaceWith('\n'));
      parsed.querySelectorAll('p,div,li').forEach(node => node.append('\n'));
      body = parsed.body.textContent || '';
    }
    const query = this.query().trim().toLocaleLowerCase();
    const parts: {text:string;han?:boolean;bullet?:boolean;match?:boolean}[] = [];
    for (const run of body.split(/([\u3400-\u9fff]+|[●•]+)/u).filter(Boolean)) {
      const style = {han:/^[\u3400-\u9fff]+$/u.test(run),bullet:/^[●•]+$/.test(run)};
      let start = 0;
      let index = query ? run.toLocaleLowerCase().indexOf(query) : -1;
      while (index >= 0) {
        if (index > start) parts.push({text:run.slice(start,index),...style});
        parts.push({text:run.slice(index,index+query.length),...style,match:true});
        start = index + query.length; index = run.toLocaleLowerCase().indexOf(query,start);
      }
      if (start < run.length) parts.push({text:run.slice(start),...style});
    }
    return parts;
  });
}

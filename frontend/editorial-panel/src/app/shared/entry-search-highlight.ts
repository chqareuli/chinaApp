import { Injectable, signal } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class EntrySearchHighlight {
  readonly query = signal('');
}
export function highlightRuns<T extends { text: string; image?: unknown }>(
  parts: T[],
  query: string,
): (T & { highlighted?: boolean })[] {
  const needle = query.trim().toLocaleLowerCase();
  if (!needle) return parts;
  return parts.flatMap((part) => {
    if (part.image) return [part];
    const result: (T & { highlighted?: boolean })[] = [];
    const text = part.text.toLocaleLowerCase();
    let start = 0,
      index = 0;
    while ((index = text.indexOf(needle, start)) >= 0) {
      if (index > start) result.push({ ...part, text: part.text.slice(start, index) });
      result.push({
        ...part,
        text: part.text.slice(index, index + needle.length),
        highlighted: true,
      });
      start = index + needle.length;
    }
    if (start < part.text.length) result.push({ ...part, text: part.text.slice(start) });
    return result;
  });
}

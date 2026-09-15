import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PanelSearchService {
  readonly query = signal('');
  matches(...values: unknown[]): boolean {
    const query = this.query().trim().toLocaleLowerCase();
    return (
      !query ||
      values.some((v) =>
        String(v ?? '')
          .toLocaleLowerCase()
          .includes(query),
      )
    );
  }
}

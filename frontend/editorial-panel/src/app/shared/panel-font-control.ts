import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class PanelAppearance {
  readonly fontSize = signal(15);
  resize(delta: number): void {
    this.fontSize.update((size) => Math.min(22, Math.max(12, size + delta)));
  }
}
@Component({
  selector: 'app-panel-font-control',
  template: `<aside aria-label="Font size · 字号">
    <span>Font size · 字号</span
    ><button
      class="btn btn-ghost"
      type="button"
      aria-label="Increase font · 放大字体"
      (click)="appearance.resize(1)"
      [disabled]="appearance.fontSize() >= 22"
    >
      +</button
    ><button
      class="btn btn-ghost"
      type="button"
      aria-label="Decrease font · 缩小字体"
      (click)="appearance.resize(-1)"
      [disabled]="appearance.fontSize() <= 12"
    >
      −
    </button>
  </aside>`,
  styles: [
    'aside{display:flex;flex-direction:column;align-items:center;gap:5px}span{writing-mode:vertical-rl;font-size:12px;color:var(--muted-foreground)}',
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PanelFontControl {
  readonly appearance = inject(PanelAppearance);
}

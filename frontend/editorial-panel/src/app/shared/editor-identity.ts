import { Component, Injectable, computed, inject, input, output, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { EditorsService } from '../core/services/editors.service';
import { EditorListItem } from '../core/models/admin.models';
import { editorRoleCodes } from '../core/models/auth.models';

@Injectable({ providedIn: 'root' })
export class EditorDirectory {
  readonly editors = signal<EditorListItem[]>([]);
  private loaded = false;
  private readonly service = inject(EditorsService);
  load(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.service
      .list()
      .pipe(catchError(() => of([])))
      .subscribe((items) => this.editors.set(items));
  }
  update(editor: EditorListItem): void {
    this.editors.update((items) => [...items.filter((item) => item.id !== editor.id), editor]);
  }
}
@Component({
  selector: 'app-editor-identity',
  template: `<span class="person" [class.team]="team()"
    ><span class="roles">
      @for (code of codes(); track code) {
        <span class="viz-badge">{{ code }}</span>
      }
    </span>
    @if (clickable()) {
      <button type="button" (click)="activate.emit()">
        <strong>{{ display() }}</strong>
        @if (team() && person()?.chineseName) {
          <strong lang="zh">{{ person()?.chineseName }}</strong>
        }
      </button>
    } @else {
      <span class="names"
        ><strong>{{ display() }}</strong>
        @if (team() && person()?.chineseName) {
          <strong lang="zh">{{ person()?.chineseName }}</strong>
        }
      </span>
    }
  </span>`,
  styles: [
    `
      :host {
        display: inline-block;
        font-size: inherit;
      }
      .person {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        font-size: inherit;
      }
      .roles,
      .names,
      button {
        display: inline-flex;
        flex-direction: column;
        align-items: flex-start;
        justify-content: center;
        gap: 2px;
      }
      .roles .viz-badge {
        margin: 0 !important;
      }
      .names strong,
      button strong {
        font: inherit;
        font-weight: 700;
        line-height: 1.35;
      }
      .team {
        color: #4b4da7;
      }
      button {
        font: inherit;
        border: 0;
        padding: 0;
        background: transparent;
        color: inherit;
        cursor: pointer;
        text-align: left;
      }
    `,
  ],
})
export class EditorIdentity {
  readonly editor = input<EditorListItem | null>(null);
  readonly id = input<number | null | undefined>(null);
  readonly name = input<string | null | undefined>('');
  readonly role = input('');
  readonly team = input(false);
  readonly clickable = input(false);
  readonly activate = output<void>();
  private readonly directory = inject(EditorDirectory);
  constructor() {
    this.directory.load();
  }
  readonly person = computed(
    () => this.editor() ?? this.directory.editors().find((e) => e.id === this.id()) ?? null,
  );
  readonly display = computed(() => this.person()?.displayName || this.name() || '—');
  readonly codes = computed(() => {
    const e = this.person();
    const role = e?.editorialRole || e?.role || this.role();
    const code = (editorRoleCodes as Record<string, string>)[role] || role;
    return [
      ...new Set(
        [code, ...(e?.isSupervisor || e?.role === 'SuperAdmin' ? ['SV'] : [])].filter(Boolean),
      ),
    ];
  });
}

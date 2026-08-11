import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { PublicDictionaryEntry } from '../../core/models/public-dictionary.models';
import { PublicEntryArticle } from '../public-entry-article/public-entry-article';

@Component({
  selector: 'app-entry-preview-modal',
  imports: [PublicEntryArticle],
  templateUrl: './entry-preview-modal.html',
  styleUrl: './entry-preview-modal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryPreviewModal {
  readonly entry = input.required<PublicDictionaryEntry>();
  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  close(): void {
    this.closed.emit();
  }

  closeFromBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }
}

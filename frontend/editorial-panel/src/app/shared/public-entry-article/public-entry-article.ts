import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PublicDictionaryEntry } from '../../core/models/public-dictionary.models';

@Component({
  selector: 'app-public-entry-article',
  imports: [DatePipe, NgTemplateOutlet, RouterLink],
  templateUrl: './public-entry-article.html',
  styleUrl: './public-entry-article.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicEntryArticle {
  readonly entry = input.required<PublicDictionaryEntry>();
}

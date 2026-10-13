import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InlineEntryText } from '../inline-entry-text';
import { PublicDictionaryEntry } from '../../core/models/public-dictionary.models';

@Component({
  selector: 'app-public-entry-article',
  imports: [DatePipe, NgTemplateOutlet, RouterLink, InlineEntryText],
  templateUrl: './public-entry-article.html',
  styleUrl: './public-entry-article.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicEntryArticle {
  readonly entry = input.required<PublicDictionaryEntry>();

  toRoman(value: number): string {
    if (!Number.isInteger(value) || value < 1) return '';

    const numerals: ReadonlyArray<readonly [number, string]> = [
      [1000, 'M'], [900, 'CM'], [500, 'D'], [400, 'CD'],
      [100, 'C'], [90, 'XC'], [50, 'L'], [40, 'XL'],
      [10, 'X'], [9, 'IX'], [5, 'V'], [4, 'IV'], [1, 'I'],
    ];
    let remainder = value;
    let result = '';

    for (const [amount, numeral] of numerals) {
      while (remainder >= amount) {
        result += numeral;
        remainder -= amount;
      }
    }

    return result;
  }
}

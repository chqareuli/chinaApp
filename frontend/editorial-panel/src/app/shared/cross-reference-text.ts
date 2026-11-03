import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { InlineEntryText } from './inline-entry-text';

@Component({
  selector: 'app-cross-reference-text',
  imports: [InlineEntryText],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-inline-entry-text
    [body]="body()"
    [references]="references()"
    field="reference"
    [route]="route()"
  />`,
})
export class CrossReferenceText {
  readonly reference = input.required<{
    label: string;
    target: string;
    displayText: string | null;
    targetEntryId: number | null;
    targets?: { displayText: string; textOffset: number; targetEntryId: number | null }[];
  }>();
  readonly route = input('/word');
  readonly body = computed(
    () => this.reference().displayText || `${this.reference().label} ${this.reference().target}`,
  );
  readonly references = computed(() => [
    {
      ...this.reference(),
      placement: 'Inline',
      textField: 'reference',
      textOffset: 0,
      displayText: this.body(),
    },
  ]);
}

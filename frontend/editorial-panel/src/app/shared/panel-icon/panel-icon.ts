import { ChangeDetectionStrategy, Component, input } from '@angular/core';
@Component({
  selector: 'app-panel-icon',
  template: `<svg
    width="27"
    height="27"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.7"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    @switch (name()) {
      @case ('home') {
        <path d="m3 10 9-7 9 7v10h-6v-6H9v6H3z"></path>
      }
      @case ('messages') {
        <rect x="3" y="5" width="18" height="14" rx="2"></rect>
        <path d="m3 7 9 7 9-7"></path>
      }
      @case ('notifications') {
        <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 20a2 2 0 0 0 4 0"></path>
      }
      @case ('new') {
        <path d="M13 3H5v18h14V9l-6-6v6h6M12 12v6M9 15h6"></path>
      }
      @case ('media') {
        <rect x="3" y="4" width="18" height="16" rx="2"></rect>
        <circle cx="8" cy="9" r="1.4"></circle>
        <path d="m21 15-5-5L5 20"></path>
      }
      @case ('statistics') {
        <rect x="4" y="2" width="16" height="20" rx="2"></rect>
        <rect x="7" y="5" width="10" height="4" rx="0.7"></rect>
        <path d="M8 13h.01M12 13h.01M16 13h.01M8 17h.01M12 17h.01M16 17h.01"></path>
      }
      @case ('team') {
        <circle cx="9" cy="8" r="3"></circle>
        <path d="M3 20v-2a6 6 0 0 1 12 0v2M17 5.3a3 3 0 0 1 0 5.4M18 14a5 5 0 0 1 3 4.6V20"></path>
      }
      @case ('logout') {
        <path d="M10 17v4H3V3h7v4M8 12h13m-5-5 5 5-5 5" />
      }
      @case ('edit') {
        <path d="m15 5 4 4M4 20l4-1L20 7a2 2 0 0 0-4-4L4 15z" />
      }
    }
  </svg>`,
  styles: [':host {display:inline-flex;align-items:center}'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PanelIcon {
  readonly name = input.required<string>();
}

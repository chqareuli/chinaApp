import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

interface StatusMeta {
  label: string;
  cssClass: string;
}

const fallback: StatusMeta = { label: 'უცნობი', cssClass: 'status-unknown' };
const statuses: Record<string, StatusMeta> = {
  newentry: { label: 'ახალი სტატია', cssClass: 'status-new' },
  zhreview: { label: 'ჩინური შემოწმება', cssClass: 'status-zh' },
  kareview: { label: 'ქართული შემოწმება', cssClass: 'status-ka' },
  ready: { label: 'მზადაა', cssClass: 'status-ready' },
  published: { label: 'გამოქვეყნებული', cssClass: 'status-published' },
  archived: { label: 'არქივი', cssClass: 'status-archived' },
};

export function getStatusMeta(status: string): StatusMeta {
  const key = status.replaceAll('_', '').toLowerCase();
  return statuses[key] ?? { ...fallback, label: status || fallback.label };
}

@Component({
  selector: 'app-status-badge',
  template: `
    <span class="status-badge" [class]="'status-badge ' + meta().cssClass">
      <span class="status-dot" aria-hidden="true"></span>
      {{ meta().label }}
    </span>
  `,
  styleUrl: './status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<string>();
  readonly meta = computed(() => getStatusMeta(this.status()));
}

import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

interface StatusMeta {
  label: string;
  cssClass: string;
}

const fallback: StatusMeta = { label: 'უცნობი', cssClass: 'status-unknown' };
const statuses: Record<string, StatusMeta> = {
  newentry: { label: 'New Entry · 新建', cssClass: 'status-new' },
  zhreview: { label: 'CHT-review · 中文审核', cssClass: 'status-zh' },
  kareview: { label: 'GEL-review · 格文审核', cssClass: 'status-ka' },
  ready: { label: 'Ready · 待发布', cssClass: 'status-ready' },
  published: { label: 'Published · 已发布', cssClass: 'status-published' },
  archived: { label: 'Archived · 已归档', cssClass: 'status-archived' },
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

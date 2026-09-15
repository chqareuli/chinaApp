import { getStatusMeta } from './status-badge';

describe('getStatusMeta', () => {
  it.each([
    ['NewEntry', 'New Entry · 新建'],
    ['zh_review', 'CHT-review · 中文审核'],
    ['KaReview', 'GEL-review · 格文审核'],
    ['ready', 'Ready · 待发布'],
    ['Published', 'Published · 已发布'],
  ])('maps %s to the approved bilingual label', (status, label) => {
    expect(getStatusMeta(status).label).toBe(label);
  });

  it('preserves an unknown status as a safe fallback label', () => {
    expect(getStatusMeta('NeedsRepair').label).toBe('NeedsRepair');
  });

  it.each([
    ['NewEntry', 'status-new'],
    ['ZhReview', 'status-zh'],
    ['KaReview', 'status-ka'],
    ['Ready', 'status-ready'],
    ['Published', 'status-published'],
  ])('maps %s to its document color class', (status, cssClass) => {
    expect(getStatusMeta(status).cssClass).toBe(cssClass);
  });
});

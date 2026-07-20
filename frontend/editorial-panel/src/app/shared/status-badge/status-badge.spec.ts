import { getStatusMeta } from './status-badge';

describe('getStatusMeta', () => {
  it.each([
    ['NewEntry', 'ახალი სტატია'],
    ['zh_review', 'ჩინური შემოწმება'],
    ['KaReview', 'ქართული შემოწმება'],
    ['ready', 'მზადაა'],
    ['Published', 'გამოქვეყნებული'],
  ])('maps %s to its Georgian label', (status, label) => {
    expect(getStatusMeta(status).label).toBe(label);
  });

  it('preserves an unknown status as a safe fallback label', () => {
    expect(getStatusMeta('NeedsRepair').label).toBe('NeedsRepair');
  });
});

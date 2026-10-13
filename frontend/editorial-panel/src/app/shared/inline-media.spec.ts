import { captureMediaPlacement, readMediaPlacement, resolveMediaOffset } from './inline-media';

describe('inline media positions', () => {
  it('persists and resolves a position after text has been inserted before it', () => {
    const body = 'ა'.repeat(50) + 'მარცხენა მარჯვენა';
    const placement = readMediaPlacement(captureMediaPlacement('sense', 9, 'definition', body, 59))!;
    expect(JSON.parse(JSON.stringify(placement))).toEqual(placement);
    expect(resolveMediaOffset(placement, 'ახალი ' + body)).toBe(65);
  });
  it('supports endpoints, rejects invalid placements and leaves legacy media separate', () => {
    expect(readMediaPlacement('sense:9')).toBeNull();
    expect(readMediaPlacement('inline:{}')).toBeNull();
    const p = readMediaPlacement(captureMediaPlacement('example', 2, 'ka', 'ტექსტი', 100))!;
    expect(p.offset).toBe(6);
    expect(resolveMediaOffset(p, '')).toBe(0);
  });
  it('keeps the anchor when text immediately before it changes', () => {
    const p = readMediaPlacement(captureMediaPlacement('sense', 1, 'definition', 'before after', 7))!;
    expect(resolveMediaOffset(p, 'replacement after')).toBe(12);
  });
});

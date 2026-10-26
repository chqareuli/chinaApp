import { highlightRuns } from './entry-search-highlight';
describe('Entry search context', () => {
  it('highlights every match while preserving links, text, and image positions', () => {
    const parts = highlightRuns(
      [
        { text: '河深流靜', target: 2 },
        { text: '', target: null, image: { id: 3 } },
        { text: '靜', target: null },
      ],
      '靜',
    );
    expect(parts.filter((part) => part.highlighted).map((part) => part.text)).toEqual(['靜', '靜']);
    expect(parts.map((part) => part.text).join('')).toBe('河深流靜靜');
    expect(parts.find((part) => part.highlighted)?.target).toBe(2);
    expect(parts.filter((part) => part.image)).toHaveLength(1);
  });
});

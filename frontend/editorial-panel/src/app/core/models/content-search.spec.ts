import { canSearchContent } from './content-search';
describe('Entry content search threshold', () => {
  it.each(['中', '测试', 'ab', 'ქა', 'a-b'])('accepts %s', (value) =>
    expect(canSearchContent(value)).toBe(true),
  );
  it.each(['', 'a', 'ქ', '12', 'a1', '!?'])('does not search for %s', (value) =>
    expect(canSearchContent(value)).toBe(false),
  );
});

export function canSearchContent(query: string): boolean {
  return /\p{Script=Han}/u.test(query) || (query.match(/\p{L}/gu)?.length ?? 0) >= 2;
}

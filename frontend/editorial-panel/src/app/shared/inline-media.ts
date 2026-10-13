export interface InlineMediaPlacement {
  kind: 'sense' | 'example';
  nodeId: number;
  field: 'definition' | 'zh' | 'ka';
  offset: number;
  before: string;
  after: string;
}

export function captureMediaPlacement(kind: InlineMediaPlacement['kind'], nodeId: number, field: InlineMediaPlacement['field'], body: string, offset: number): string {
  offset = Math.max(0, Math.min(body.length, offset));
  return 'inline:' + JSON.stringify({ kind, nodeId, field, offset, before: body.slice(Math.max(0, offset - 32), offset), after: body.slice(offset, offset + 32) });
}

export function readMediaPlacement(value: string | null | undefined): InlineMediaPlacement | null {
  if (!value?.startsWith('inline:')) return null;
  try {
    const p = JSON.parse(value.slice(7));
    return ['sense', 'example'].includes(p.kind) && ['definition', 'zh', 'ka'].includes(p.field)
      && Number.isInteger(p.nodeId) && p.nodeId > 0 && Number.isInteger(p.offset) && p.offset >= 0
      && typeof p.before === 'string' && typeof p.after === 'string' ? p : null;
  } catch { return null; }
}

export function resolveMediaOffset(p: InlineMediaPlacement, body: string): number {
  const context = p.before + p.after;
  if (context) {
    const index = body.indexOf(context);
    if (index >= 0 && body.indexOf(context, index + 1) < 0) return index + p.before.length;
  }
  // Edits immediately before/after the image may change one half of its context.
  if (p.after) {
    const index = body.indexOf(p.after);
    if (index >= 0 && body.indexOf(p.after, index + 1) < 0) return index;
  }
  if (p.before) {
    const index = body.indexOf(p.before);
    if (index >= 0 && body.indexOf(p.before, index + 1) < 0) return index + p.before.length;
  }
  return Math.min(body.length, p.offset);
}

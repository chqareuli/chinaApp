export interface EntrySummary {
  id: number;
  lemma: string;
  pinyin: string | null;
  status: string;
  mainAuthorDisplayName: string | null;
  lastEditorDisplayName: string;
  updatedAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type EntryListSort = 'UpdatedDesc' | 'UpdatedAsc' | 'LemmaAsc' | 'LemmaDesc' | 'Status';

export interface EntryListQuery {
  page: number;
  pageSize: number;
  query?: string;
  status?: string;
  editorId?: number;
  sort: EntryListSort;
}

export interface Example {
  id: number | null;
  zhSegmentId: number | null;
  kaSegmentId: number | null;
  zh: string;
  ka: string;
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
}

export interface Sense {
  id: number | null;
  definitionId: number | null;
  number: number;
  definition: string;
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  examples: Example[];
}

export interface GramGroup {
  id: number | null;
  posSegmentId: number | null;
  number: number;
  pos: string | null;
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  senses: Sense[];
}

export interface DirectPos {
  id: number | null;
  pos: string;
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  senses: Sense[];
}

export interface Homonym {
  id: number | null;
  text: string;
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  gramGroups: GramGroup[];
}

export interface PinyinItem {
  id: number | null;
  value: string;
  order: number;
}

export interface ComplexFormItem {
  id: number | null;
  value: string;
  order: number;
}

export interface EditorialMarker {
  id: number | null;
  category: 'Style' | 'Domain' | 'Abbr' | 'Lang';
  value: string;
}

export interface CrossReference {
  id: number | null;
  type: 'see' | 'seeAlso' | 'equivalent' | 'antonym' | 'illustration';
  label: string;
  target: string;
  targetEntryId: number | null;
  targetEntryLemma: string | null;
  displayText: string | null;
  placement: 'Inline' | 'Attached' | 'Standalone';
}

export interface VocabularyItem {
  id: number;
  category: 'pos' | 'style' | 'domain' | 'abbr' | 'lang';
  code: string;
  label: string;
  sortOrder: number;
}

export interface EntryDetail extends EntrySummary {
  createdAtUtc: string;
  rowVersion: number;
  pinyins: PinyinItem[];
  complexForms: ComplexFormItem[];
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  homonyms: Homonym[];
  gramGroups: GramGroup[];
  directPoses: DirectPos[];
  senses: Sense[];
  capabilities?: EntryCapabilities;
}

export interface EntryCapabilities {
  canEditContent: boolean;
  allowedStatusTransitions: string[];
}

export interface EntryHistoryItem {
  id: number;
  action: string;
  performedByEditorId: number;
  performedByDisplayName: string;
  performedAtUtc: string;
  oldValue: string | null;
  newValue: string | null;
  notes: string | null;
}

export interface MediaAssetItem {
  id: number;
  entryId: number;
  originalFilename: string;
  mimeType: string;
  storageUrl: string;
  thumbnailUrl: string | null;
  placement: string | null;
  uploadedByEditorId: number;
  uploadedByDisplayName: string;
  uploadedAtUtc: string;
}

export interface SaveEntryContentRequest {
  lemma: string;
  pinyins: PinyinItem[];
  complexForms: ComplexFormItem[];
  markers: EditorialMarker[];
  crossReferences: CrossReference[];
  homonyms: Homonym[];
  gramGroups: GramGroup[];
  directPoses: DirectPos[];
  senses: Sense[];
  rowVersion: number;
}

export interface CreateEntryRequest {
  lemma: string;
  pinyins: string[];
}

export interface EntryStatusTotals {
  countsByStatus: Record<string, number>;
  total: number;
}

export interface ApiProblem {
  code?: string;
  detail?: string;
  message?: string;
  title?: string;
}

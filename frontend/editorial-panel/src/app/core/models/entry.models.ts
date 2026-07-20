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

export interface Example {
  zh: string;
  ka: string;
}

export interface Sense {
  number: number;
  definition: string;
  examples: Example[];
}

export interface Homonym {
  senses: Sense[];
}

export interface EntryDetail extends EntrySummary {
  createdAtUtc: string;
  rowVersion: number;
  homonyms: Homonym[];
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

export interface SaveEntryContentRequest {
  lemma: string;
  pinyin: string | null;
  homonyms: Homonym[];
  rowVersion: number;
}

export interface CreateEntryRequest {
  lemma: string;
  pinyin: string | null;
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

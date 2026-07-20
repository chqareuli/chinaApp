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
}

export interface CreateEntryRequest {
  lemma: string;
  pinyin: string | null;
}

export interface EntryStatusTotals {
  countsByStatus: Record<string, number>;
  total: number;
}

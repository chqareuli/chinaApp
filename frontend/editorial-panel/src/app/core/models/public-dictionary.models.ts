export type PublicSearchMode = 'word' | 'text';

export interface PublicDictionarySearchItem {
  id: number;
  lemma: string;
  pinyin: string | null;
  snippet: string;
}

export interface PublicDictionaryExample {
  zh: string;
  ka: string;
}

export interface PublicDictionarySense {
  number: number;
  definition: string;
  examples: PublicDictionaryExample[];
}

export interface PublicDictionaryHomonym {
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryEntry {
  id: number;
  lemma: string;
  pinyin: string | null;
  updatedAtUtc: string;
  homonyms: PublicDictionaryHomonym[];
}

export interface PublicDictionaryStats {
  publishedEntries: number;
}

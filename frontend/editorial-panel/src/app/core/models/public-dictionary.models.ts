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
  gramGroups: PublicDictionaryGramGroup[];
}

export interface PublicDictionaryGramGroup {
  number: number;
  pos: string | null;
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryDirectPos {
  pos: string;
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryEntry {
  id: number;
  lemma: string;
  pinyin: string | null;
  pinyins: string[];
  updatedAtUtc: string;
  homonyms: PublicDictionaryHomonym[];
  gramGroups: PublicDictionaryGramGroup[];
  directPoses: PublicDictionaryDirectPos[];
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryStats {
  publishedEntries: number;
}

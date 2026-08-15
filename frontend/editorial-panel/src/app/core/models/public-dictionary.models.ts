export type PublicSearchMode = 'word' | 'text';

export interface PublicDictionarySearchItem {
  id: number;
  lemma: string;
  pinyin: string | null;
  snippet: string;
}

export interface PublicDictionaryCrossReference {
  type: string;
  label: string;
  target: string;
  displayText: string | null;
  placement: 'Inline' | 'Attached' | 'Standalone';
  targetEntryId: number | null;
  targetEntryLemma: string | null;
}

export interface PublicDictionaryMarker {
  category: 'Style' | 'Domain' | 'Abbr' | 'Lang';
  value: string;
  label: string;
}

export interface PublicDictionaryExample {
  zh: string;
  ka: string;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
}

export interface PublicDictionarySense {
  number: number;
  definition: string;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
  examples: PublicDictionaryExample[];
}

export interface PublicDictionaryHomonym {
  text: string | null;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
  gramGroups: PublicDictionaryGramGroup[];
}

export interface PublicDictionaryGramGroup {
  number: number;
  pos: string | null;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryDirectPos {
  pos: string;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryEntry {
  id: number;
  lemma: string;
  pinyin: string | null;
  pinyins: string[];
  complexForms: string[];
  updatedAtUtc: string;
  homonyms: PublicDictionaryHomonym[];
  gramGroups: PublicDictionaryGramGroup[];
  directPoses: PublicDictionaryDirectPos[];
  senses: PublicDictionarySense[];
}

export interface PublicDictionaryStats {
  publishedEntries: number;
}

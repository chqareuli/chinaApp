export type PublicSearchMode = 'word' | 'text';

export interface PublicDictionarySearchItem {
  id: number;
  lemma: string;
  pinyin: string | null;
  snippet: string;
}

export interface PublicDictionaryCrossReference {
  textOffset?: number | null;
  textField?: string | null;
  type: string;
  label: string;
  target: string;
  displayText: string | null;
  placement: 'Inline' | 'Attached' | 'Standalone';
  targetEntryId: number | null;
  targetEntryLemma: string | null;
}

export interface PublicDictionaryMarker {
  textOffset?: number | null;
  textField?: string | null;
  displayText?: string | null;
  category: 'Style' | 'Domain' | 'Abbr' | 'Lang';
  value: string;
  label: string;
}

export interface PublicDictionaryExample {
  inlineImages?: { offset: number; url: string; alt: string }[];
  zh: string;
  ka: string;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
}

export interface PublicDictionarySense {
  inlineImages?: { offset: number; url: string; alt: string }[];
  number: number;
  definition: string;
  markers: PublicDictionaryMarker[];
  crossReferences: PublicDictionaryCrossReference[];
  examples: PublicDictionaryExample[];
}

export interface PublicDictionaryHomonym {
  directSenses?: PublicDictionarySense[];
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

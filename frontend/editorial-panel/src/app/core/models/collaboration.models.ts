export interface CommentSummary {
  id: number;
  entryId: number;
  authorEditorId: number;
  authorDisplayName: string;
  commentText: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ReferenceMaterialDetail {
  entryId: number;
  g1: string;
  g2: string;
  g3: string;
  g4: string;
  g5: string;
  originalRawReferenceMaterial: string;
  updatedAtUtc: string;
}

export type SaveReferenceMaterialRequest = Pick<
  ReferenceMaterialDetail,
  'g1' | 'g2' | 'g3' | 'g4' | 'g5'
>;

export type SupportMaterialLanguage = 'English' | 'Chinese' | 'Russian';

export interface SupportMaterialSummary {
  id: number;
  entryId: number;
  language: SupportMaterialLanguage;
  commentText: string;
  createdAtUtc: string;
  canDelete: boolean;
}

export interface CreateSupportMaterialRequest {
  language: SupportMaterialLanguage;
  commentText: string;
}

export interface NotificationSummary {
  id: number;
  entryId: number;
  entryLemma: string;
  triggeredByEditorId: number;
  triggeredByDisplayName: string;
  type: string;
  isRead: boolean;
  createdAtUtc: string;
}

export interface DirectMessageSummary {
  id: number;
  senderEditorId: number;
  senderDisplayName: string;
  recipientEditorId: number;
  recipientDisplayName: string;
  body: string;
  sentAtUtc: string;
  readAtUtc: string | null;
}

export interface UnreadActivitySummary {
  notifications: number;
  messages: number;
}

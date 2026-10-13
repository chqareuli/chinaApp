import { EditorRole } from './auth.models';

export interface EditorListItem {
  firstName?: string | null;
  lastName?: string | null;
  chineseName?: string | null;
  weChatAccount?: string | null;
  editorialRole?: EditorRole | null;
  isSupervisor?: boolean;
  isCancelled?: boolean;
  id: number;
  email: string;
  displayName: string;
  role: EditorRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateEditorRequest {
  firstName?: string;
  lastName?: string;
  chineseName?: string;
  weChatAccount?: string;
  isSupervisor?: boolean;
  email: string;
  displayName: string;
  role: EditorRole;
  initialPassword: string;
}

export interface UpdateEditorRequest {
  isSupervisor?: boolean;
  isCancelled?: boolean;
  email: string;
  displayName: string;
  role: EditorRole;
  isActive: boolean;
  newPassword: string | null;
}

export interface EditorScoreSummary {
  totalEntries: number;
  editorId: number;
  displayName: string;
  role: EditorRole;
  mainScoreTotal: number;
  additionalScoreTotal: number;
  kaEditorScoreTotal: number;
}

export interface ScoreCountsByStatus {
  main: Record<string, number>;
  additional: Record<string, number>;
  kaEditor: Record<string, number>;
}

export interface ScoreTypeStatistics {
  total: number;
  lastAwardedAtUtc: string | null;
}

export interface PersonalScoreStatistics {
  totalEntries: number;
  totalPoints: number;
  main: ScoreTypeStatistics;
  additional: ScoreTypeStatistics;
  kaEditor: ScoreTypeStatistics;
}

export interface EditorAccountingDetail {
  entriesByStatus?: Record<string, number>;
  editorId: number;
  displayName: string;
  email: string;
  role: EditorRole;
  createdAtUtc: string;
  scoresByStatus: ScoreCountsByStatus;
  personalStatistics: PersonalScoreStatistics;
}

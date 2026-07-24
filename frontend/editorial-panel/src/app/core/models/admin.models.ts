import { EditorRole } from './auth.models';

export interface EditorListItem {
  id: number;
  email: string;
  displayName: string;
  role: EditorRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateEditorRequest {
  email: string;
  displayName: string;
  role: EditorRole;
  initialPassword: string;
}

export interface UpdateEditorRequest {
  email: string;
  displayName: string;
  role: EditorRole;
  isActive: boolean;
  newPassword: string | null;
}

export interface EditorScoreSummary {
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

export interface EditorAccountingDetail {
  editorId: number;
  displayName: string;
  email: string;
  role: EditorRole;
  createdAtUtc: string;
  scoresByStatus: ScoreCountsByStatus;
}

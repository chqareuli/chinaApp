export type EditorRole =
  | 'SuperAdmin'
  | 'ChiefEditor'
  | 'ZhEditor'
  | 'KaEditor'
  | 'AssistantEditor';

export interface EditorSummary {
  id: number;
  email: string;
  displayName: string;
  role: EditorRole;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  editor: EditorSummary;
}

export interface AuthSession extends AuthResponse {}

export const editorRoleLabels: Record<EditorRole, string> = {
  SuperAdmin: 'სუპერ ადმინი',
  ChiefEditor: 'მთავარი რედაქტორი',
  ZhEditor: 'ჩინურის რედაქტორი',
  KaEditor: 'ქართულის რედაქტორი',
  AssistantEditor: 'დამხმარე რედაქტორი',
};

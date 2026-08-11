export type EditorRole =
  | 'SuperAdmin'
  | 'ChiefEditor'
  | 'DeputyChiefEditor'
  | 'Editor'
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
  SuperAdmin: 'სუპერვიზორი',
  ChiefEditor: 'მთავარი რედაქტორი',
  DeputyChiefEditor: 'მთავარი რედაქტორის მოადგილე',
  Editor: 'რედაქტორი',
  KaEditor: 'ქართული ენის რედაქტორი',
  AssistantEditor: 'დამხმარე რედაქტორი',
};

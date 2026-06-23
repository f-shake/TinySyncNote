// 用户信息
export interface UserInfo {
  id: string
  username: string
}

// 认证响应
export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: UserInfo
}

// 登录请求
export interface LoginRequest {
  username: string
  password: string
}

// 注册请求
export interface RegisterRequest {
  username: string
  password: string
}

// 笔记本
export interface Notebook {
  id: string
  userId: string
  name: string
  description?: string
  sortOrder: number
  createdAt: string
  updatedAt: string
}

// 目录（含子目录和笔记数）
export interface Category {
  id: string
  notebookId: string
  parentCategoryId?: string
  name: string
  sortOrder: number
  createdAt: string
  updatedAt: string
  children: Category[]
  noteCount: number
}

// 笔记详情（含正文）
export interface NoteDetailResponse {
  id: string
  categoryId: string
  title: string
  content: string
  version: number
  createdAt: string
  updatedAt: string
  notebookId: string
  notebookName: string
}

// 笔记列表项（不含正文）
export interface NoteListItem {
  id: string
  categoryId: string
  title: string
  version: number
  updatedAt: string
}

// 快照
export interface NoteSnapshot {
  id: string
  noteId: string
  title: string
  content?: string
  version: number
  snapshotType: 'Manual' | 'Automatic'
  snapshotAt: string
  contentLength?: number
}

// 冲突详情
export interface ConflictResponse {
  id: string
  noteId: string
  noteTitle: string
  localVersion: number
  remoteVersion: number
  localContent: string
  remoteContent: string
  createdAt: string
  resolvedAt?: string
  resolutionStrategy?: string
}

// 分享
export interface NoteShare {
  id: string
  noteId: string
  ownerUserId: string
  ownerUsername: string
  sharedWithUserId: string
  sharedNoteCopyId: string
  sharedAt: string
}

export interface PublicShareLink {
  id: string
  noteId: string
  token: string
  shareUrl: string
  createdAt: string
  expiresAt: string | null
  isActive: boolean
}

export interface UserSearchItem {
  id: string
  username: string
}

export interface SharedNoteView {
  title: string
  htmlContent: string
  createdAt: string
  updatedAt: string
}

// 冲突列表项
export interface ConflictListItem {
  id: string
  noteId: string
  noteTitle: string
  createdAt: string
}

// AI 设置
export interface AISettings {
  ai_url: string
  ai_key: string
  ai_model: string
}

// AI 对话消息
export interface AIChatMessage {
  role: 'system' | 'user' | 'assistant' | 'tool'
  content: string
  tool_call_id?: string
  tool_calls?: AIToolCall[]
}

export interface AIToolCall {
  id: string
  type: 'function'
  function: {
    name: string
    arguments: string
  }
}

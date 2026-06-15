// 用户信息
export interface UserInfo {
  id: string
  username: string
  email: string
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
  email: string
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

// 冲突列表项
export interface ConflictListItem {
  id: string
  noteId: string
  noteTitle: string
  createdAt: string
}

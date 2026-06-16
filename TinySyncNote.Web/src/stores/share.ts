import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../utils/http'
import type { NoteShare, PublicShareLink, UserSearchItem } from '../types'
import { ElMessage } from 'element-plus'

export const useShareStore = defineStore('share', () => {
  const publicLinks = ref<PublicShareLink[]>([])
  const shares = ref<NoteShare[]>([])
  const searchResults = ref<UserSearchItem[]>([])
  const loading = ref(false)

  // ── 导出 Markdown ──
  async function exportAsMarkdown(noteId: string): Promise<{ data: Blob; filename: string }> {
    const res = await http.get(`/api/export/note/${noteId}/markdown`, {
      responseType: 'blob'
    })
    return { data: res.data, filename: getFilename(res) || `note-${noteId}.md` }
  }

  // ── 导出 HTML（支持亮/暗主题） ──
  async function exportAsHtml(noteId: string, theme = 'light'): Promise<{ data: Blob; filename: string }> {
    const res = await http.get(`/api/export/note/${noteId}/html`, {
      params: { theme },
      responseType: 'blob'
    })
    return { data: res.data, filename: getFilename(res) || `note-${noteId}.html` }
  }

  function getFilename(res: any): string | null {
    const header = res.headers?.['content-disposition']
    if (!header) return null
    const match = header.match(/filename\*?=(?:UTF-8'')?([^;\s]+)/i)
    return match ? decodeURIComponent(match[1]) : null
  }

  // ── 用户搜索 ──
  async function searchUsers(query: string) {
    if (query.length < 2) { searchResults.value = []; return }
    try {
      const res = await http.get<UserSearchItem[]>('/api/users/search', {
        params: { q: query }
      })
      searchResults.value = res.data
    } catch {
      searchResults.value = []
    }
  }

  // ── 分享给用户 ──
  async function shareNote(noteId: string, sharedWithUserId: string) {
    try {
      const res = await http.post<NoteShare>(`/api/share/note/${noteId}`, {
        sharedWithUserId
      })
      ElMessage.success('分享成功')
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '分享失败')
      throw err
    }
  }

  // ── 公开链接 ──
  async function fetchPublicLinks(noteId: string) {
    try {
      const res = await http.get<PublicShareLink[]>(`/api/share/note/${noteId}/public`)
      publicLinks.value = res.data
    } catch {
      publicLinks.value = []
    }
  }

  async function createPublicLink(noteId: string, expiresAt?: string) {
    try {
      const res = await http.post<PublicShareLink>(`/api/share/note/${noteId}/public`, {
        expiresAt: expiresAt || null
      })
      publicLinks.value.unshift(res.data)
      ElMessage.success('分享链接已创建')
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '创建链接失败')
      throw err
    }
  }

  async function revokePublicLink(shareId: string) {
    try {
      await http.delete(`/api/share/public/${shareId}`)
      publicLinks.value = publicLinks.value.filter(l => l.id !== shareId)
      ElMessage.success('链接已撤销')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '撤销失败')
      throw err
    }
  }

  return {
    publicLinks, shares, searchResults, loading,
    exportAsMarkdown, exportAsHtml,
    searchUsers, shareNote,
    fetchPublicLinks, createPublicLink, revokePublicLink
  }
})

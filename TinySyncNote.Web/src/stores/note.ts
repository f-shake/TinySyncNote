import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../utils/http'
import type { NoteListItem, NoteDetailResponse } from '../types'
import { ElMessage } from 'element-plus'

export const useNoteStore = defineStore('note', () => {
  const notes = ref<NoteListItem[]>([])
  const currentNote = ref<NoteDetailResponse | null>(null)
  const loading = ref(false)
  const selectedCategoryId = ref<string | null>(null)

  async function fetchByCategory(categoryId: string) {
    loading.value = true
    try {
      const res = await http.get<NoteListItem[]>(`/api/notes/by-category/${categoryId}`)
      notes.value = res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '获取笔记列表失败')
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id: string): Promise<NoteDetailResponse> {
    loading.value = true
    try {
      const res = await http.get<NoteDetailResponse>(`/api/notes/${id}`)
      currentNote.value = res.data
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '获取笔记失败')
      throw err
    } finally {
      loading.value = false
    }
  }

  async function create(categoryId: string, title: string, content = '') {
    try {
      const res = await http.post<NoteDetailResponse>('/api/notes', {
        categoryId,
        title,
        content
      })
      notes.value.unshift({
        id: res.data.id,
        categoryId: res.data.categoryId,
        title: res.data.title,
        version: res.data.version,
        updatedAt: res.data.updatedAt
      })
      ElMessage.success('笔记已创建')
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '创建笔记失败')
      throw err
    }
  }

  async function update(id: string, title: string, content: string, version: number) {
    try {
      const res = await http.put<NoteDetailResponse>(`/api/notes/${id}`, {
        title,
        content,
        version
      })
      currentNote.value = res.data
      // 同步更新列表中的标题和版本
      const idx = notes.value.findIndex(n => n.id === id)
      if (idx !== -1) {
        notes.value[idx].title = res.data.title
        notes.value[idx].version = res.data.version
        notes.value[idx].updatedAt = res.data.updatedAt
      }
      return res.data
    } catch (err: any) {
      if (err.response?.status === 409) {
        // 冲突 — 抛出给编辑器处理
        throw err
      }
      ElMessage.error(err.response?.data?.message || '保存失败')
      throw err
    }
  }

  async function remove(id: string) {
    try {
      await http.delete(`/api/notes/${id}`)
      notes.value = notes.value.filter(n => n.id !== id)
      if (currentNote.value?.id === id) currentNote.value = null
      ElMessage.success('笔记已删除')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '删除失败')
      throw err
    }
  }

  return { notes, currentNote, loading, selectedCategoryId, fetchByCategory, fetchById, create, update, remove }
})

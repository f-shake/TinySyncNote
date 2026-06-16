import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../utils/http'
import type { NoteSnapshot } from '../types'
import { ElMessage } from 'element-plus'

export const useSnapshotStore = defineStore('snapshot', () => {
  const snapshots = ref<NoteSnapshot[]>([])
  const loading = ref(false)

  async function fetchByNote(noteId: string) {
    loading.value = true
    try {
      const res = await http.get<NoteSnapshot[]>(`/api/notes/${noteId}/snapshots`)
      snapshots.value = res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '获取历史版本失败')
    } finally {
      loading.value = false
    }
  }

  async function fetchById(noteId: string, snapshotId: string): Promise<NoteSnapshot | null> {
    try {
      const res = await http.get<NoteSnapshot>(`/api/notes/${noteId}/snapshots/${snapshotId}`)
      return res.data
    } catch {
      return null
    }
  }

  async function create(noteId: string) {
    try {
      const res = await http.post<NoteSnapshot>(`/api/notes/${noteId}/snapshots`)
      snapshots.value.unshift(res.data)
      ElMessage.success('快照已创建')
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '创建快照失败')
      throw err
    }
  }

  async function restore(noteId: string, snapshotId: string) {
    try {
      const res = await http.post<NoteSnapshot>(`/api/notes/${noteId}/snapshots/${snapshotId}/restore`)
      ElMessage.success('已恢复到历史版本')
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '恢复失败')
      throw err
    }
  }

  async function remove(noteId: string, snapshotId: string) {
    try {
      await http.delete(`/api/notes/${noteId}/snapshots/${snapshotId}`)
      snapshots.value = snapshots.value.filter(s => s.id !== snapshotId)
      ElMessage.success('快照已删除')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '删除快照失败')
      throw err
    }
  }

  return { snapshots, loading, fetchByNote, fetchById, create, restore, remove }
})

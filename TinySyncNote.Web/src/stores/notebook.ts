import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../utils/http'
import type { Notebook } from '../types'
import { ElMessage, type MessageParams } from 'element-plus'

export const useNotebookStore = defineStore('notebook', () => {
  const notebooks = ref<Notebook[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll() {
    loading.value = true
    error.value = null
    try {
      const res = await http.get<Notebook[]>('/api/notebooks')
      notebooks.value = res.data
    } catch (err: any) {
      error.value = err.response?.data?.message || '获取笔记本列表失败'
      ElMessage.error(error.value || '获取笔记本列表失败')
    } finally {
      loading.value = false
    }
  }

  async function create(name: string, description?: string) {
    try {
      const res = await http.post<Notebook>('/api/notebooks', { name, description })
      notebooks.value.unshift(res.data)
      ElMessage.success('笔记本创建成功' as MessageParams)
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '创建失败')
      throw err
    }
  }

  async function update(id: string, name: string, description?: string) {
    try {
      const res = await http.put<Notebook>(`/api/notebooks/${id}`, {
        name,
        description: description || null,
        sortOrder: 0
      })
      const idx = notebooks.value.findIndex(n => n.id === id)
      if (idx !== -1) notebooks.value[idx] = res.data
      ElMessage.success('更新成功' as MessageParams)
      return res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '更新失败')
      throw err
    }
  }

  async function remove(id: string) {
    try {
      await http.delete(`/api/notebooks/${id}`)
      notebooks.value = notebooks.value.filter(n => n.id !== id)
      ElMessage.success('笔记本已删除' as MessageParams)
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '删除失败')
      throw err
    }
  }

  return { notebooks, loading, error, fetchAll, create, update, remove }
})

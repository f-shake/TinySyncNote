import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../utils/http'
import type { Category } from '../types'
import { ElMessage } from 'element-plus'

export const useCategoryStore = defineStore('category', () => {
  const tree = ref<Category[]>([])
  const loading = ref(false)

  async function fetchTree(notebookId: string) {
    loading.value = true
    try {
      const res = await http.get<Category[]>(`/api/categories/tree/${notebookId}`)
      tree.value = res.data
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '获取目录失败')
    } finally {
      loading.value = false
    }
  }

  async function create(notebookId: string, name: string, parentCategoryId?: string) {
    try {
      await http.post('/api/categories', { notebookId, name, parentCategoryId })
      await fetchTree(notebookId)
      ElMessage.success('目录已创建')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '创建目录失败')
      throw err
    }
  }

  async function rename(id: string, name: string, notebookId: string) {
    try {
      await http.put(`/api/categories/${id}`, { name, sortOrder: 0, parentCategoryId: null })
      await fetchTree(notebookId)
      ElMessage.success('目录已重命名')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '重命名失败')
      throw err
    }
  }

  async function remove(id: string, notebookId: string) {
    try {
      await http.delete(`/api/categories/${id}`)
      await fetchTree(notebookId)
      ElMessage.success('目录已删除')
    } catch (err: any) {
      ElMessage.error(err.response?.data?.message || '删除失败')
      throw err
    }
  }

  return { tree, loading, fetchTree, create, rename, remove }
})

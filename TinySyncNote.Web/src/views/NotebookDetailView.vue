<script setup lang="ts">
import { onMounted, watch, ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useNoteStore } from '../stores/note'
import { useNotebookStore } from '../stores/notebook'
import http from '../utils/http'
import {
  Plus, Document, Download, Upload
} from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'

const route = useRoute()
const router = useRouter()
const noteStore = useNoteStore()
const notebookStore = useNotebookStore()

const notebookId = computed(() => route.params.id as string)
const currentNotebookName = computed(() => {
  const nb = notebookStore.notebooks.find(n => n.id === notebookId.value)
  return nb?.name || '加载中...'
})

// 导入对话框
const showImportDialog = ref(false)
const importFile = ref<File | null>(null)
const importing = ref(false)

onMounted(() => {
  if (noteStore.selectedCategoryId) {
    noteStore.fetchByCategory(noteStore.selectedCategoryId)
  }
})

watch(notebookId, () => {
  noteStore.selectedCategoryId = null
  noteStore.notes = []
})

function createNote() {
  if (!noteStore.selectedCategoryId) {
    ElMessage.warning('请先在左侧选择一个目录')
    return
  }
  noteStore.create(noteStore.selectedCategoryId, '无标题笔记')
    .then(note => {
      router.push(`/note/${note.id}?nb=${notebookId.value}&nbn=${encodeURIComponent(currentNotebookName.value)}`)
    })
}

function openNote(noteId: string) {
  router.push(`/note/${noteId}?nb=${notebookId.value}&nbn=${encodeURIComponent(currentNotebookName.value)}`)
}

function exportNotebook() {
  window.open(`/api/export/notebook/${notebookId.value}`, '_blank')
}

function onImportFileChange(file: File) {
  importFile.value = file
}

async function handleImport() {
  if (!importFile.value) return
  importing.value = true
  try {
    const formData = new FormData()
    formData.append('file', importFile.value)
    const url = `/api/import/markdown?categoryId=${noteStore.selectedCategoryId || ''}`
    await http.post(url, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    ElMessage.success('导入成功')
    showImportDialog.value = false
    if (noteStore.selectedCategoryId) {
      noteStore.fetchByCategory(noteStore.selectedCategoryId)
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '导入失败')
  } finally {
    importing.value = false
    importFile.value = null
  }
}
</script>

<template>
  <div class="notebook-detail-view">
    <!-- 头部 -->
    <div class="page-header">
      <el-breadcrumb>
        <el-breadcrumb-item :to="{ path: '/notebooks' }">笔记本</el-breadcrumb-item>
        <el-breadcrumb-item>{{ currentNotebookName }}</el-breadcrumb-item>
      </el-breadcrumb>

      <div class="header-actions">
        <el-button type="primary" :icon="Plus" @click="createNote">新建笔记</el-button>
        <el-button :icon="Download" @click="exportNotebook">导出</el-button>
        <el-button :icon="Upload" @click="showImportDialog = true">导入</el-button>
      </div>
    </div>

    <!-- 笔记列表 -->
    <div class="note-list">
      <div v-if="!noteStore.selectedCategoryId" class="empty-hint">
        <el-empty description="请在左侧选择一个目录" :image-size="120" />
      </div>

      <div v-else-if="noteStore.notes.length === 0 && !noteStore.loading" class="empty-hint">
        <el-empty description="该目录下还没有笔记" :image-size="120">
          <el-button type="primary" :icon="Plus" @click="createNote">新建笔记</el-button>
        </el-empty>
      </div>

      <div v-else class="note-scroll">
        <div
          v-for="note in noteStore.notes"
          :key="note.id"
          class="note-item"
          :class="{ active: note.id === route.params.id }"
          @click="openNote(note.id)"
        >
          <el-icon :size="16" color="#409eff"><Document /></el-icon>
          <div class="note-info">
            <div class="note-title">{{ note.title }}</div>
            <div class="note-time">{{ new Date(note.updatedAt).toLocaleString() }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ 导入对话框 ═══ -->
    <el-dialog
      v-model="showImportDialog"
      title="导入笔记"
      width="420px"
      @closed="importFile = null"
    >
      <div class="import-body">
        <p class="import-tip">支持 .md 文件和 .zip 压缩包（ZIP 会还原目录结构）</p>
        <el-upload
          drag
          :auto-upload="false"
          :show-file-list="true"
          :limit="1"
          :on-change="(u: any) => onImportFileChange(u.raw!)"
          accept=".md,.zip"
        >
          <el-icon :size="40" color="#c0c4cc"><Upload /></el-icon>
          <div class="el-upload__text">将文件拖到此处，或<em>点击选择</em></div>
        </el-upload>
      </div>
      <template #footer>
        <el-button @click="showImportDialog = false">取消</el-button>
        <el-button
          type="primary"
          :loading="importing"
          :disabled="!importFile"
          @click="handleImport"
        >
          导入
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.notebook-detail-view {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-shrink: 0;
}

.header-actions {
  display: flex;
  gap: 8px;
}

/* ── 笔记列表 ── */
.note-list {
  flex: 1;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.empty-hint {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
}

.note-scroll {
  flex: 1;
  overflow-y: auto;
}

.note-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
  cursor: pointer;
  transition: background 0.15s;
}

.note-item:hover {
  background: var(--el-fill-color-light);
}

.note-item.active {
  background: var(--el-color-primary-light-9);
}

.note-info {
  flex: 1;
  min-width: 0;
}

.note-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.note-time {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 2px;
}

/* ── 导入对话框 ── */
.import-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.import-tip {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin: 0;
}

@media (max-width: 720px) {
  .notebook-detail-view { padding: 12px; }
  .page-header { flex-direction: column; align-items: stretch; gap: 8px; }
  .header-actions { justify-content: flex-end; }
}
</style>

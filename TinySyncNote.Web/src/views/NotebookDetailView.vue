<script setup lang="ts">
import { onMounted, ref, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCategoryStore } from '../stores/category'
import { useNotebookStore } from '../stores/notebook'
import { useNoteStore } from '../stores/note'
import http from '../utils/http'
import {
  FolderOpened, Plus, Edit, Delete, Document,
  ArrowRight, ArrowDown, Download, Upload
} from '@element-plus/icons-vue'
import { ElMessageBox, ElMessage } from 'element-plus'
import type { Category } from '../types'

const route = useRoute()
const router = useRouter()
const catStore = useCategoryStore()
const notebookStore = useNotebookStore()
const noteStore = useNoteStore()

const notebookId = computed(() => route.params.id as string)
const selectedCategoryId = ref<string | null>(null)

// 创建/重命名对话框
const showCreateCatDialog = ref(false)
const showRenameCatDialog = ref(false)
const newCatName = ref('')
const editingCat = ref<Category | null>(null)
const createParentId = ref<string | undefined>(undefined)

onMounted(async () => {
  await catStore.fetchTree(notebookId.value)
  // 展开一级
  catStore.tree.forEach(c => expandedKeys.value.push(c.id))
  // 默认选中第一个有笔记的目录
  const firstCat = findFirstCategoryWithNotes(catStore.tree)
  if (firstCat) selectCategory(firstCat.id)
})

// 监视笔记本 ID 变化
watch(notebookId, (id) => {
  catStore.fetchTree(id)
  selectedCategoryId.value = null
  noteStore.notes = []
})

// ── 选择目录 ──
function selectCategory(id: string) {
  selectedCategoryId.value = id
  noteStore.fetchByCategory(id)
}

// ── 查找第一个有笔记的目录（递归） ──
function findFirstCategoryWithNotes(cats: Category[]): Category | null {
  for (const c of cats) {
    if (c.noteCount > 0) return c
    const found = findFirstCategoryWithNotes(c.children || [])
    if (found) return found
  }
  return cats.length > 0 ? cats[0] : null
}

// ── 获取当前笔记本名称 ──
const currentNotebookName = computed(() => {
  const nb = notebookStore.notebooks.find(n => n.id === notebookId.value)
  return nb?.name || '笔记本'
})

// ── 新建目录 ──
function openCreateCategory(parentId?: string) {
  createParentId.value = parentId
  newCatName.value = ''
  showCreateCatDialog.value = true
}

async function handleCreateCategory() {
  if (!newCatName.value.trim()) return
  try {
    await catStore.create(notebookId.value, newCatName.value.trim(), createParentId.value)
    showCreateCatDialog.value = false
    newCatName.value = ''
  } catch { /* handled */ }
}

// ── 重命名目录 ──
function openRenameCategory(cat: Category) {
  editingCat.value = cat
  newCatName.value = cat.name
  showRenameCatDialog.value = true
}

async function handleRenameCategory() {
  if (!editingCat.value || !newCatName.value.trim()) return
  try {
    await catStore.rename(editingCat.value.id, newCatName.value.trim(), notebookId.value)
    showRenameCatDialog.value = false
    editingCat.value = null
  } catch { /* handled */ }
}

// ── 删除目录 ──
async function handleDeleteCategory(cat: Category) {
  try {
    await ElMessageBox.confirm(
      `确定删除目录「${cat.name}」？目录下的笔记将一并删除。`,
      '删除确认',
      { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }
    )
    await catStore.remove(cat.id, notebookId.value)
    if (selectedCategoryId.value === cat.id) {
      selectedCategoryId.value = null
      noteStore.notes = []
    }
  } catch { /* cancelled or handled */ }
}

// ── 新建笔记 ──
async function createNote() {
  if (!selectedCategoryId.value) {
    ElMessageBox.alert('请先选择一个目录', '提示')
    return
  }
  try {
    const note = await noteStore.create(selectedCategoryId.value!, '无标题笔记')
    router.push(`/note/${note.id}`)
  } catch { /* handled */ }
}

// ── 导入导出 ──
const showImportDialog = ref(false)
const importFile = ref<File | null>(null)
const importing = ref(false)

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

    const isZip = importFile.value.name.endsWith('.zip')
    const url = isZip
      ? `/api/export/import/zip?notebookId=${notebookId.value}`
      : `/api/export/import/markdown?categoryId=${selectedCategoryId.value || ''}`

    await http.post(url, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })

    ElMessage.success('导入成功')
    showImportDialog.value = false
    importFile.value = null

    // 刷新目录树和笔记列表
    await catStore.fetchTree(notebookId.value)
    if (selectedCategoryId.value) {
      await noteStore.fetchByCategory(selectedCategoryId.value)
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '导入失败')
  } finally {
    importing.value = false
  }
}

// ── 展开/折叠目录树节点 ──
const expandedKeys = ref<string[]>([])

function toggleExpand(cat: Category) {
  const idx = expandedKeys.value.indexOf(cat.id)
  if (idx >= 0) expandedKeys.value.splice(idx, 1)
  else expandedKeys.value.push(cat.id)
}

function isExpanded(cat: Category) {
  return expandedKeys.value.includes(cat.id)
}

// ── flatten tree for rendering ──
interface FlatItem {
  id: string
  name: string
  depth: number
  noteCount: number
  data: Category
  hasChildren: boolean
}

function flattenTree(cats: Category[], depth = 0): FlatItem[] {
  const result: FlatItem[] = []
  for (const c of cats) {
    result.push({
      id: c.id,
      name: c.name,
      depth,
      noteCount: c.noteCount,
      data: c,
      hasChildren: (c.children?.length || 0) > 0
    })
    if (isExpanded(c) && c.children?.length) {
      result.push(...flattenTree(c.children, depth + 1))
    }
  }
  return result
}

const flatTree = computed(() => flattenTree(catStore.tree))
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
        <el-button :icon="Plus" @click="openCreateCategory()">新建目录</el-button>
        <el-button type="primary" :icon="Plus" @click="createNote">新建笔记</el-button>
        <el-button :icon="Download" @click="exportNotebook">导出</el-button>
        <el-button :icon="Upload" @click="showImportDialog = true">导入</el-button>
      </div>
    </div>

    <div class="content-area">
      <!-- 左侧：目录树 -->
      <div class="category-tree">
        <div class="panel-header">
          <span>目录</span>
          <el-button text :icon="Plus" size="small" @click="openCreateCategory()" />
        </div>

        <div v-if="catStore.loading" class="tree-loading">
          <el-skeleton :rows="3" animated />
        </div>

        <div v-else-if="catStore.tree.length === 0" class="tree-empty">
          <el-empty description="暂无目录" :image-size="80" />
        </div>

        <div v-else class="tree-scroll">
          <div
            v-for="item in flatTree"
            :key="item.id"
            class="tree-node"
            :class="{ selected: selectedCategoryId === item.id }"
            :style="{ paddingLeft: 12 + item.depth * 20 + 'px' }"
            @click="selectCategory(item.id)"
          >
            <el-icon
              v-if="item.hasChildren"
              :size="14"
              class="expand-icon"
              @click.stop="toggleExpand(item.data)"
            >
              <ArrowRight v-if="!isExpanded(item.data)" />
              <ArrowDown v-else />
            </el-icon>
            <span v-else class="expand-placeholder" />

            <el-icon :size="16" color="#e6a23c"><FolderOpened /></el-icon>
            <span class="node-name">{{ item.name }}</span>
            <span class="note-badge">{{ item.noteCount }}</span>

            <span class="node-actions" @click.stop>
              <el-button text :icon="Edit" size="small" @click="openRenameCategory(item.data)" />
              <el-button text :icon="Delete" size="small" type="danger" @click="handleDeleteCategory(item.data)" />
            </span>
          </div>
        </div>
      </div>

      <!-- 右侧：笔记列表 -->
      <div class="note-list">
        <div class="panel-header">笔记</div>

        <div v-if="!selectedCategoryId" class="empty-hint">
          <el-empty description="请先选择一个目录" :image-size="120" />
        </div>

        <div v-else-if="noteStore.loading" class="note-loading">
          <el-skeleton :rows="5" animated />
        </div>

        <div v-else-if="noteStore.notes.length === 0" class="empty-hint">
          <el-empty description="该目录下还没有笔记" :image-size="120">
            <el-button type="primary" :icon="Plus" @click="createNote">新建笔记</el-button>
          </el-empty>
        </div>

        <div v-else class="note-scroll">
          <div
            v-for="note in noteStore.notes"
            :key="note.id"
            class="note-item"
            :class="{ active: false }"
            @click="router.push(`/note/${note.id}`)"
          >
            <el-icon :size="16" color="#409eff"><Document /></el-icon>
            <div class="note-info">
              <div class="note-title">{{ note.title }}</div>
              <div class="note-meta">{{ new Date(note.updatedAt).toLocaleString() }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Category Dialog -->
    <el-dialog
      v-model="showCreateCatDialog"
      title="新建目录"
      width="380px"
      @closed="newCatName = ''"
    >
      <el-input
        v-model="newCatName"
        placeholder="请输入目录名称"
        maxlength="200"
        @keyup.enter="handleCreateCategory"
      />
      <template #footer>
        <el-button @click="showCreateCatDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreateCategory">创建</el-button>
      </template>
    </el-dialog>

    <!-- Rename Category Dialog -->
    <el-dialog
      v-model="showRenameCatDialog"
      title="重命名目录"
      width="380px"
      @closed="editingCat = null; newCatName = ''"
    >
      <el-input
        v-model="newCatName"
        placeholder="请输入新名称"
        maxlength="200"
        @keyup.enter="handleRenameCategory"
      />
      <template #footer>
        <el-button @click="showRenameCatDialog = false">取消</el-button>
        <el-button type="primary" @click="handleRenameCategory">确认</el-button>
      </template>
    </el-dialog>

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
}

.header-actions {
  display: flex;
  gap: 8px;
}

.content-area {
  flex: 1;
  display: flex;
  gap: 16px;
  overflow: hidden;
}

/* ── 左侧目录树 ── */
.category-tree {
  width: 280px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.panel-header {
  padding: 12px 16px;
  font-weight: 600;
  border-bottom: 1px solid var(--el-border-color-light);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.tree-loading,
.tree-empty,
.tree-scroll {
  flex: 1;
  overflow-y: auto;
}

.tree-loading {
  padding: 16px;
}

.tree-empty {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 20px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 12px;
  cursor: pointer;
  transition: background 0.15s;
  font-size: 13px;
  position: relative;
}

.tree-node:hover {
  background: var(--el-fill-color-light);
}

.tree-node.selected {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.expand-icon {
  cursor: pointer;
  flex-shrink: 0;
  transition: transform 0.2s;
}

.expand-placeholder {
  width: 14px;
  flex-shrink: 0;
}

.node-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.note-badge {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color);
  padding: 0 6px;
  border-radius: 8px;
  margin-right: 4px;
}

.node-actions {
  display: none;
  gap: 2px;
  flex-shrink: 0;
}

.tree-node:hover .node-actions {
  display: flex;
}

/* ── 右侧笔记列表 ── */
.note-list {
  flex: 1;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.empty-hint,
.note-loading {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
}

.note-loading {
  padding: 24px;
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

.note-meta {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 2px;
}

/* ── 导入 ── */
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

/* ════════════════════════════════════════
   笔记本详情 — 移动端自适应
   ════════════════════════════════════════ */
@media (max-width: 768px) {
  .notebook-detail-view {
    padding: 12px;
  }

  .content-area {
    flex-direction: column;
  }

  .category-tree {
    width: 100%;
    max-height: 200px;
    flex-shrink: 0;
  }

  .page-header h2 {
    font-size: 16px;
  }
}
</style>

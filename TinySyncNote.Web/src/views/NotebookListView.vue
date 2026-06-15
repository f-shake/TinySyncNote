<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useNotebookStore } from '../stores/notebook'
import { Folder, Plus, Edit, Delete } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import type { Notebook } from '../types'

const router = useRouter()
const store = useNotebookStore()

// 对话框状态
const showCreateDialog = ref(false)
const showRenameDialog = ref(false)
const newNotebookName = ref('')
const editingNotebook = ref<Notebook | null>(null)

onMounted(() => {
  store.fetchAll()
})

// ── 新建 ──
async function handleCreate() {
  if (!newNotebookName.value.trim()) return
  try {
    const nb = await store.create(newNotebookName.value.trim())
    showCreateDialog.value = false
    newNotebookName.value = ''
    router.push(`/notebook/${nb.id}`)
  } catch {
    // store 已处理错误
  }
}

// ── 重命名 ──
function openRename(notebook: Notebook) {
  editingNotebook.value = notebook
  newNotebookName.value = notebook.name
  showRenameDialog.value = true
}

async function handleRename() {
  if (!editingNotebook.value || !newNotebookName.value.trim()) return
  try {
    await store.update(editingNotebook.value.id, newNotebookName.value.trim())
    showRenameDialog.value = false
    editingNotebook.value = null
    newNotebookName.value = ''
  } catch {
    // store 已处理错误
  }
}

// ── 删除 ──
async function handleDelete(notebook: Notebook) {
  try {
    await ElMessageBox.confirm(
      `确定删除笔记本「${notebook.name}」？笔记本内的笔记和目录将一并删除。`,
      '删除确认',
      { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }
    )
    await store.remove(notebook.id)
  } catch {
    // 取消删除或已处理
  }
}

// ── 点击进入 ──
function openNotebook(id: string) {
  router.push(`/notebook/${id}`)
}
</script>

<template>
  <div class="notebook-list-view">
    <div class="page-header">
      <h2>我的笔记本</h2>
      <el-button type="primary" :icon="Plus" @click="showCreateDialog = true">
        新建笔记本
      </el-button>
    </div>

    <!-- Error -->
    <div v-if="store.error && store.notebooks.length === 0" class="error-state">
      <el-result
        icon="error"
        title="加载失败"
        :sub-title="store.error"
      >
        <template #extra>
          <el-button type="primary" @click="store.fetchAll()">重试</el-button>
        </template>
      </el-result>
    </div>

    <!-- Empty -->
    <div v-else-if="store.notebooks.length === 0" class="empty-state">
      <el-empty description="还没有笔记本，点击上方按钮创建">
        <template #image>
          <el-icon :size="80" color="#c0c4cc"><Folder /></el-icon>
        </template>
      </el-empty>
    </div>

    <!-- Notebook Grid -->
    <div v-else class="notebook-grid">
      <div
        v-for="nb in store.notebooks"
        :key="nb.id"
        class="notebook-card"
        @click="openNotebook(nb.id)"
      >
        <div class="card-icon">
          <el-icon :size="28" color="#409eff"><Folder /></el-icon>
        </div>
        <div class="card-body">
          <div class="card-title">{{ nb.name }}</div>
          <div class="card-desc">{{ nb.description || '暂无描述' }}</div>
        </div>
        <div class="card-actions" @click.stop>
          <el-button text :icon="Edit" @click="openRename(nb)" />
          <el-button text :icon="Delete" type="danger" @click="handleDelete(nb)" />
        </div>
      </div>
    </div>

    <!-- Create Dialog -->
    <el-dialog
      v-model="showCreateDialog"
      title="新建笔记本"
      width="400px"
      :close-on-click-modal="false"
      @closed="newNotebookName = ''"
    >
      <el-input
        v-model="newNotebookName"
        placeholder="请输入笔记本名称"
        maxlength="200"
        show-word-limit
        @keyup.enter="handleCreate"
      />
      <template #footer>
        <el-button @click="showCreateDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreate">创建</el-button>
      </template>
    </el-dialog>

    <!-- Rename Dialog -->
    <el-dialog
      v-model="showRenameDialog"
      title="重命名笔记本"
      width="400px"
      @closed="editingNotebook = null; newNotebookName = ''"
    >
      <el-input
        v-model="newNotebookName"
        placeholder="请输入新名称"
        maxlength="200"
        show-word-limit
        @keyup.enter="handleRename"
      />
      <template #footer>
        <el-button @click="showRenameDialog = false">取消</el-button>
        <el-button type="primary" @click="handleRename">确认</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.notebook-list-view {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h2 {
  margin: 0;
  font-size: 22px;
  color: var(--el-text-color-primary);
}

.loading-state,
.error-state,
.empty-state {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
}

.notebook-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.notebook-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--el-bg-color);
}

.notebook-card:hover {
  border-color: var(--el-color-primary-light-5);
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
}

.card-icon {
  flex-shrink: 0;
}

.card-body {
  flex: 1;
  min-width: 0;
}

.card-title {
  font-weight: 600;
  font-size: 15px;
  color: var(--el-text-color-primary);
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-actions {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
  align-self: center;
  width: 28px;
}

.card-actions :deep(.el-button) {
  width: 100%;
  padding: 5px 0;
  margin-left: 0;
  margin-right: 0;
}

.notebook-card .card-actions {
  opacity: 0;
  transition: opacity 0.2s;
}

.notebook-card:hover .card-actions {
  opacity: 1;
}
</style>

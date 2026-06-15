<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useNotebookStore } from '../stores/notebook'
import { useCategoryStore } from '../stores/category'
import { useNoteStore } from '../stores/note'
import { useRoute, useRouter } from 'vue-router'
import {
  WarningFilled, Setting, SwitchButton, Moon, Sunny, MoreFilled,
  FolderOpened, ArrowRight, ArrowDown, Plus, Edit, Delete
} from '@element-plus/icons-vue'
import type { Category } from '../types'
import { ElMessageBox } from 'element-plus'

interface FlatItem {
  id: string
  name: string
  depth: number
  noteCount: number
  hasChildren: boolean
  data: Category
}

const authStore = useAuthStore()
const notebookStore = useNotebookStore()
const categoryStore = useCategoryStore()
const noteStore = useNoteStore()
const route = useRoute()
const router = useRouter()

const isDark = ref(document.documentElement.classList.contains('dark'))
function toggleDark() {
  isDark.value = !isDark.value
  isDark.value
    ? (document.documentElement.classList.add('dark'), localStorage.setItem('theme', 'dark'))
    : (document.documentElement.classList.remove('dark'), localStorage.setItem('theme', 'light'))
}
function handleLogout() { authStore.logout() }

// ── 路由判断 ──
const isNotebookDetail = computed(() => route.name === 'NotebookDetail')
const isEditor = computed(() => route.name === 'NoteEditor')

// ── 笔记本数据 ──
onMounted(() => notebookStore.fetchAll())

const notebookId = computed(() => route.params.id as string)

// 进入笔记本时加载目录树，默认选中第一个
watch(notebookId, async (id) => {
  if (!id || !isNotebookDetail.value) return
  noteStore.selectedCategoryId = null
  noteStore.notes = []
  await categoryStore.fetchTree(id)
  autoSelectFirstCategory()
}, { immediate: true })

// 默认选中第一个有笔记的目录，否则选中第一个
function autoSelectFirstCategory() {
  const first = findFirstCategoryWithNotes(categoryStore.tree) || categoryStore.tree[0]
  if (first) selectCategory(first.id)
}
function findFirstCategoryWithNotes(cats: Category[]): Category | null {
  for (const c of cats) {
    if (c.noteCount && c.noteCount > 0) return c
    if (c.children?.length) {
      const found = findFirstCategoryWithNotes(c.children)
      if (found) return found
    }
  }
  return null
}

// ── 目录树 ──
const expandedKeys = ref<string[]>([])
function toggleExpand(cat: Category) {
  const idx = expandedKeys.value.indexOf(cat.id)
  idx >= 0 ? expandedKeys.value.splice(idx, 1) : expandedKeys.value.push(cat.id)
}
function isExpanded(cat: Category) { return expandedKeys.value.includes(cat.id) }

function flattenTree(cats: Category[], depth = 0): FlatItem[] {
  const result: FlatItem[] = []
  for (const c of cats) {
    result.push({ id: c.id, name: c.name, depth, noteCount: c.noteCount || 0, hasChildren: !!(c.children?.length), data: c })
    if (isExpanded(c) && c.children?.length) result.push(...flattenTree(c.children, depth + 1))
  }
  return result
}
const flatTree = computed(() => flattenTree(categoryStore.tree))

function selectCategory(id: string) {
  noteStore.selectedCategoryId = id
  noteStore.fetchByCategory(id)
}

function openCreateCategory(parentId?: string) {
  ElMessageBox.prompt('请输入目录名称', '新建目录', {
    confirmButtonText: '创建', cancelButtonText: '取消', inputPattern: /\S/, inputErrorMessage: '名称不能为空'
  }).then(async ({ value }) => {
    if (!notebookId.value) return
    const id = await categoryStore.create(notebookId.value, value, parentId)
    if (id) selectCategory(id)
  }).catch(() => {})
}
function openRenameCategory(cat: Category) {
  ElMessageBox.prompt('请输入新名称', '重命名目录', {
    confirmButtonText: '确认', cancelButtonText: '取消', inputValue: cat.name, inputPattern: /\S/, inputErrorMessage: '名称不能为空'
  }).then(({ value }) => {
    if (notebookId.value) categoryStore.rename(cat.id, value, notebookId.value)
  }).catch(() => {})
}
function handleDeleteCategory(cat: Category) {
  ElMessageBox.confirm(`确定删除目录「${cat.name}」？`, '删除确认', {
    confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning'
  }).then(() => {
    if (notebookId.value) {
      categoryStore.remove(cat.id, notebookId.value)
      if (noteStore.selectedCategoryId === cat.id) noteStore.selectedCategoryId = null
    }
  }).catch(() => {})
}

function handleMoreCommand(cmd: string) {
  cmd === 'toggle-dark' ? toggleDark() : router.push(cmd)
}

const editorNotebookId = computed(() => isEditor.value ? (route.query.nb as string) : null)

const currentNotebookName = computed(() => {
  const id = editorNotebookId.value || notebookId.value
  if (!id) return ''
  const nb = notebookStore.notebooks.find(n => n.id === id)
  if (nb?.name) return nb.name
  return (route.query.nbn as string) || ''
})

// 编辑器模式下加载目录树
watch(() => route.query.nb, (nb) => {
  if (nb && isEditor.value) {
    categoryStore.fetchTree(nb as string)
  }
}, { immediate: true })
</script>

<template>
  <div class="main-layout">
    <!-- ═══ 顶栏 ═══ -->
    <header class="top-bar">
      <div class="top-bar-left">
        <h2 class="app-title" :class="{ dim: isEditor }" @click="router.push('/notebooks')">TinySyncNote</h2>
        <span v-if="isEditor" class="top-nb-name">{{ currentNotebookName }}</span>
      </div>
      <div class="top-bar-right">
        <el-dropdown trigger="click" @command="handleMoreCommand">
          <el-button text :icon="MoreFilled" class="top-btn" />
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="toggle-dark">
                <el-icon><component :is="isDark ? Sunny : Moon" /></el-icon>
                {{ isDark ? '浅色模式' : '深色模式' }}
              </el-dropdown-item>
              <el-dropdown-item command="/conflicts" divided>
                <el-icon><WarningFilled /></el-icon>冲突列表
              </el-dropdown-item>
              <el-dropdown-item command="/settings">
                <el-icon><Setting /></el-icon>设置
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
        <span class="user-name">{{ authStore.user?.username }}</span>
        <el-button text :icon="SwitchButton" @click="handleLogout" class="top-btn" />
      </div>
    </header>

    <!-- ═══ 内容 ═══ -->
    <div class="workspace">
      <!-- ── 浏览笔记本：目录 | 笔记 ── -->
      <template v-if="isNotebookDetail">
        <div class="browse-panels">
        <div class="col col-cat">
          <div class="col-header">
            目录
            <el-button text :icon="Plus" size="small" @click="openCreateCategory()" />
          </div>
          <div v-if="categoryStore.tree.length === 0" class="col-empty">
            <el-empty description="暂无目录" :image-size="40" />
          </div>
          <div v-else class="col-body">
            <div
              v-for="item in flatTree" :key="item.id"
              class="tree-node"
              :class="{ selected: noteStore.selectedCategoryId === item.id }"
              :style="{ paddingLeft: 6 + item.depth * 14 + 'px' }"
              @click="selectCategory(item.id)"
            >
              <el-icon v-if="item.hasChildren" :size="11" class="expand-icon" @click.stop="toggleExpand(item.data)">
                <ArrowRight v-if="!isExpanded(item.data)" /><ArrowDown v-else />
              </el-icon>
              <span v-else class="expand-ph" />
              <el-icon :size="13" color="#e6a23c"><FolderOpened /></el-icon>
              <span class="node-name">{{ item.name }}</span>
              <span class="nb">{{ item.noteCount }}</span>
              <span class="node-actions" @click.stop>
                <el-button text :icon="Edit" size="small" @click="openRenameCategory(item.data)" />
                <el-button text :icon="Delete" size="small" type="danger" @click="handleDeleteCategory(item.data)" />
              </span>
            </div>
          </div>
        </div>

        <div class="col col-notes">
          <div class="col-header">笔记</div>
          <router-view />
        </div>
        </div>
      </template>

      <!-- ── 编辑笔记：导航 1/3 + 编辑器 2/3 ── -->
      <template v-else-if="isEditor">
        <div class="nav-compact">
          <div class="nav-compact-body">
          <div class="col col-cat">
            <div class="col-header">目录</div>
            <div v-if="categoryStore.tree.length === 0" class="col-empty">
              <el-empty description="暂无目录" :image-size="40" />
            </div>
            <div v-else class="col-body">
              <div
                v-for="item in flatTree" :key="item.id"
                class="tree-node"
                :class="{ selected: noteStore.selectedCategoryId === item.id }"
                :style="{ paddingLeft: 6 + item.depth * 14 + 'px' }"
                @click="selectCategory(item.id)"
              >
                <el-icon v-if="item.hasChildren" :size="11" class="expand-icon" @click.stop="toggleExpand(item.data)">
                  <ArrowRight v-if="!isExpanded(item.data)" /><ArrowDown v-else />
                </el-icon>
                <span v-else class="expand-ph" />
                <el-icon :size="13" color="#e6a23c"><FolderOpened /></el-icon>
                <span class="node-name">{{ item.name }}</span>
                <span class="nb">{{ item.noteCount }}</span>
              </div>
            </div>
          </div>
          <div class="col col-notes">
            <div class="col-header">笔记</div>
            <div class="col-body">
              <div
                v-for="n in noteStore.notes" :key="n.id"
                class="note-item"
                :class="{ active: n.id === route.params.id }"
                @click="router.push(`/note/${n.id}?nb=${editorNotebookId}`)"
              >
                <span class="note-title">{{ n.title }}</span>
              </div>
            </div>
          </div>
          </div>
        </div>
        <main class="editor-area">
          <router-view />
        </main>
      </template>

      <!-- ── 其他路由：全宽 ── -->
      <main v-else class="main-full">
        <router-view />
      </main>
    </div>
  </div>
</template>

<style scoped>
.main-layout { display: flex; flex-direction: column; height: 100vh; overflow: hidden; }

.top-bar {
  display: flex; align-items: center; justify-content: space-between;
  height: 44px; padding: 0 16px; background: var(--el-bg-color);
  flex-shrink: 0; z-index: 100;
  box-shadow: 0 1px 3px rgba(0,0,0,0.05);
}
.app-title { font-size: 16px; color: var(--el-color-primary); margin: 0; cursor: pointer; user-select: none; white-space: nowrap; }
.app-title.dim { color: var(--el-text-color-secondary); opacity: 0.6; }
.top-bar-left { display: flex; align-items: center; gap: 10px; min-width: 0; overflow: hidden; }
.top-nb-name { font-size: 14px; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; background: var(--el-color-primary-light-9); color: var(--el-color-primary); padding: 2px 10px; border-radius: 4px; line-height: 1.4; }
.top-bar-right { display: flex; align-items: center; gap: 4px; flex-shrink: 0; }
.top-btn { padding: 4px 8px; font-size: 13px; }
.user-name { font-size: 13px; color: var(--el-text-color-secondary); white-space: nowrap; margin: 0 4px; }

.workspace { flex: 1; display: flex; overflow: hidden; }

/* ── 列 ── */
.col { display: flex; flex-direction: column; overflow: hidden; background: var(--el-bg-color); }
.col-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 10px 12px; font-size: 11px; font-weight: 600;
  color: var(--el-text-color-secondary); text-transform: uppercase;
  letter-spacing: 0.5px; border-bottom: 1px solid var(--el-border-color-light); flex-shrink: 0;
}
.col-body { flex: 1; overflow-y: auto; padding: 4px 0; }
.col-empty { flex: 1; display: flex; justify-content: center; align-items: center; padding: 20px; }

.col-cat { flex: 0 0 220px; border-right: 1px solid var(--el-border-color-light); }
.col-notes { flex: 1; border-right: 1px solid var(--el-border-color-light); }
.col-notes:last-child { border-right: none; }

.nav-compact .col-cat,
.nav-compact .col-notes { flex: 1; min-width: 0; }

/* ── 目录树 ── */
.tree-node { display: flex; align-items: center; gap: 3px; padding: 5px 10px; cursor: pointer; font-size: 12px; transition: background 0.12s; }
.tree-node:hover { background: var(--el-fill-color-light); }
.tree-node.selected { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.expand-icon { flex-shrink: 0; cursor: pointer; color: var(--el-text-color-secondary); }
.expand-ph { width: 11px; flex-shrink: 0; }
.node-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.nb { font-size: 10px; color: var(--el-text-color-secondary); background: var(--el-fill-color); padding: 0 4px; border-radius: 5px; flex-shrink: 0; }
.node-actions { display: none; gap: 2px; flex-shrink: 0; }
.tree-node:hover .node-actions { display: flex; }

/* ── 笔记列表 ── */
.note-item { display: flex; align-items: center; padding: 8px 12px; cursor: pointer; font-size: 13px; transition: background 0.12s; }
.note-item:hover { background: var(--el-fill-color-light); }
.note-item.active { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.note-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* ── 编辑模式 ── */
.nav-compact {
  display: flex;
  flex-direction: column;
  flex: 0 0 33.33%;
  min-width: 320px;
  border-right: 1px solid var(--el-border-color-light);
}

.nav-compact-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.browse-panels {
  display: flex;
  flex: 1;
  overflow: hidden;
}
.editor-area { flex: 1; overflow: hidden; display: flex; flex-direction: column; }

/* ── 全宽 ── */
.main-full { flex: 1; overflow-y: auto; background: var(--el-bg-color-page); }

@media (max-width: 720px) {
  .nav-compact { display: none; }
  .browse-panels { flex-direction: column; }
  .col-cat { flex: 0 0 auto; max-height: 40vh; }
  .col-notes { flex: 1; }
}
</style>

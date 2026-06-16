<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useCategoryStore } from '../stores/category'
import { useNoteStore } from '../stores/note'
import {
  FolderOpened, Plus, Edit, Delete, ArrowRight, ArrowDown
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

const categoryStore = useCategoryStore()
const noteStore = useNoteStore()
const route = useRoute()

const notebookId = computed(() => route.params.id as string)

// 进入笔记本时加载目录树，默认选中第一个
watch(notebookId, async (id) => {
  if (!id) return
  noteStore.selectedCategoryId = null
  noteStore.notes = []
  await categoryStore.fetchTree(id)
  autoSelectFirstCategory()
}, { immediate: true })

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

// ── 目录树展平 ──
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

// ── 目录操作 ──
function openCreateCategory(parentId?: string) {
  ElMessageBox.prompt('请输入目录名称', '新建目录', {
    confirmButtonText: '创建', cancelButtonText: '取消', inputPattern: /\S/, inputErrorMessage: '名称不能为空'
  }).then(async ({ value }) => {
    const nbId = notebookId.value
    if (!nbId) return
    const id = await categoryStore.create(nbId, value, parentId)
    if (id) selectCategory(id)
  }).catch(() => {})
}
function openRenameCategory(cat: Category) {
  ElMessageBox.prompt('请输入新名称', '重命名目录', {
    confirmButtonText: '确认', cancelButtonText: '取消', inputValue: cat.name, inputPattern: /\S/, inputErrorMessage: '名称不能为空'
  }).then(({ value }) => {
    const nbId = notebookId.value
    if (nbId) categoryStore.rename(cat.id, value, nbId)
  }).catch(() => {})
}
function handleDeleteCategory(cat: Category) {
  ElMessageBox.confirm(`确定删除目录「${cat.name}」？`, '删除确认', {
    confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning'
  }).then(() => {
    const nbId = notebookId.value
    if (nbId) {
      categoryStore.remove(cat.id, nbId)
      if (noteStore.selectedCategoryId === cat.id) noteStore.selectedCategoryId = null
    }
  }).catch(() => {})
}
</script>

<template>
  <div class="col col-cat">
    <div class="col-header">
      <span class="col-header-label">
        <el-icon :size="14"><FolderOpened /></el-icon>
        目录
      </span>
      <el-button text :icon="Plus" size="small" @click="openCreateCategory()" />
    </div>
    <div v-if="categoryStore.tree.length === 0" class="col-empty">
      <el-empty description="暂无目录" :image-size="40" />
    </div>
    <div v-else class="col-body">
      <div v-for="item in flatTree" :key="item.id" class="tree-node"
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
        <span class="node-actions" @click.stop>
          <el-button text :icon="Edit" size="small" @click="openRenameCategory(item.data)" />
          <el-button text :icon="Delete" size="small" type="danger" @click="handleDeleteCategory(item.data)" />
        </span>
        <span class="nb">{{ item.noteCount }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.col { display: flex; flex-direction: column; overflow: hidden; background: var(--el-bg-color); }
.col-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 10px 14px; font-size: 12px; font-weight: 600;
  color: var(--el-text-color-primary); letter-spacing: 0.3px;
  border-bottom: 1px solid var(--el-border-color-light); flex-shrink: 0;
  background: var(--el-fill-color-lighter);
  user-select: none;
}
.col-header-label { display: flex; align-items: center; gap: 6px; }
.col-body { flex: 1; overflow-y: auto; padding: 4px 0; }
.col-empty { flex: 1; display: flex; justify-content: center; align-items: center; padding: 20px; }
.col-cat { flex: 0 0 220px; border-right: 1px solid var(--el-border-color-light); }

.tree-node { display: flex; align-items: center; gap: 8px; padding: 5px 10px; cursor: pointer; font-size: 12px; transition: background 0.12s; }
.tree-node:hover { background: var(--el-fill-color-light); }
.tree-node.selected { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.expand-icon { flex-shrink: 0; cursor: pointer; color: var(--el-text-color-secondary); }
.expand-ph { width: 11px; flex-shrink: 0; }
.node-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.nb { font-size: 10px; color: var(--el-text-color-secondary); background: var(--el-fill-color); padding: 0 4px; border-radius: 5px; flex-shrink: 0; margin-left: auto; }
.node-actions { display: flex; gap: 2px; flex-shrink: 0; }
@media (hover: hover) { .node-actions { visibility: hidden; } .tree-node:hover .node-actions { visibility: visible; } }
@media (hover: none) { .node-actions { opacity: 0.5; } }
</style>

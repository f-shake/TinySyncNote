<script setup lang="ts">
import { computed, ref, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCategoryStore } from '../stores/category'
import { useNoteStore } from '../stores/note'
import { ArrowLeft, FolderOpened, Document, Plus, Delete } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import type { Category } from '../types'

interface NoteTreeData {
  id: string
  label: string
  type: 'cat' | 'note'
  children?: NoteTreeData[]
  noteCount?: number
}

const route = useRoute()
const router = useRouter()
const categoryStore = useCategoryStore()
const noteStore = useNoteStore()

const treeRef = ref<any>(null)
const treeBackRef = ref<HTMLDivElement | null>(null)

const notebookId = computed(() => route.query.nb as string)

// 编辑器加载/切换笔记本时获取目录树
watch(() => route.query.nb, (nb) => {
  if (nb) {
    categoryStore.fetchTree(nb as string)
  }
}, { immediate: true })

// ── 构建 el-tree 数据 ──
// 分类形成树形结构，选中分类的笔记作为叶子节点注入
const treeData = computed((): NoteTreeData[] => {
  function build(cats: Category[]): NoteTreeData[] {
    return cats.map(c => {
      const children: NoteTreeData[] = []

      // 子分类
      if (c.children?.length) {
        children.push(...build(c.children))
      }

      // 选中的分类 → 笔记作为叶子节点
      if (c.id === noteStore.selectedCategoryId && noteStore.notes.length > 0) {
        children.push(...noteStore.notes.map(n => ({
          id: `note:${n.id}`,
          label: n.title,
          type: 'note' as const
        })))
      }

      return {
        id: `cat:${c.id}`,
        label: c.name,
        type: 'cat',
        noteCount: c.noteCount || 0,
        children
      }
    })
  }
  return build(categoryStore.tree)
})

const defaultProps = { children: 'children', label: 'label' }

// ── 默认展开所有分类 ──
const defaultExpandedKeys = computed(() => {
  const ids: string[] = []
  function collect(cats: Category[]) {
    for (const c of cats) {
      ids.push(`cat:${c.id}`)
      if (c.children?.length) collect(c.children)
    }
  }
  collect(categoryStore.tree)
  return ids
})

// ── 高亮当前节点 ──
const currentNodeKey = computed(() => {
  if (route.name === 'NoteEditor' && route.params.id) {
    return `note:${route.params.id}`
  }
  if (noteStore.selectedCategoryId) {
    return `cat:${noteStore.selectedCategoryId}`
  }
  return undefined
})

watch(currentNodeKey, (key) => {
  if (key && treeRef.value) {
    treeRef.value.setCurrentKey(key)
  }
}, { immediate: true })

// 树数据加载/刷新后重新应用高亮（解决异步时序：树还没加载完时 setCurrentKey 找不到节点）
watch(treeData, () => {
  if (currentNodeKey.value && treeRef.value) {
    nextTick(() => {
      treeRef.value.setCurrentKey(currentNodeKey.value)
    })
  }
})

// 点击节点
function handleNodeClick(data: NoteTreeData) {
  if (data.type === 'cat') {
    const catId = data.id.replace('cat:', '')
    noteStore.selectedCategoryId = catId
    noteStore.fetchByCategory(catId)
  } else {
    const noteId = data.id.replace('note:', '')
    router.push(`/note/${noteId}?nb=${notebookId.value}`)
  }
}

// ── 选中分类时自动展开 ──
watch(() => noteStore.selectedCategoryId, (newId) => {
  if (!newId || !treeRef.value) return
  // el-tree 没有公开的 expand 方法，通过内部 store 展开节点
  try {
    const node = (treeRef.value as any).store?.nodesMap?.[`cat:${newId}`]
    if (node) node.expanded = true
  } catch { /* tree 尚未就绪 */ }
})

// ── 返回按钮高度同步 ──
let heightSyncObs: ResizeObserver | null = null
onMounted(() => {
  nextTick(() => {
    const toolbar = document.querySelector('.editor-toolbar') as HTMLElement | null
    const backEl = treeBackRef.value
    if (toolbar && backEl) {
      heightSyncObs = new ResizeObserver(() => { backEl.style.height = toolbar.offsetHeight + 'px' })
      heightSyncObs.observe(toolbar)
    }
  })
})
onUnmounted(() => {
  heightSyncObs?.disconnect()
})

function goBackFromEditor() {
  const nbId = notebookId.value
  if (nbId) router.push(`/notebook/${nbId}`)
  else router.push('/notebooks')
}

// ── 在分类下新建笔记 ──
async function createNoteInCategory(catId: string) {
  if (!catId) return
  try {
    const note = await noteStore.create(catId, '无标题笔记')
    if (note) {
      noteStore.selectedCategoryId = catId
      router.push(`/note/${note.id}?nb=${notebookId.value}`)
    }
  } catch { /* handled by store */ }
}

// ── 新建目录 ──
function openCreateCategory(parentId?: string) {
  ElMessageBox.prompt('请输入目录名称', '新建目录', {
    confirmButtonText: '创建', cancelButtonText: '取消', inputPattern: /\S/, inputErrorMessage: '名称不能为空'
  }).then(async ({ value }) => {
    const nbId = notebookId.value
    if (!nbId) return
    const id = await categoryStore.create(nbId, value, parentId)
    if (id) {
      noteStore.selectedCategoryId = id
      noteStore.fetchByCategory(id)
    }
  }).catch(() => {})
}

// ── 删除笔记 ──
async function handleDeleteNote(e: Event, noteId: string, noteTitle: string) {
  e.stopPropagation()
  try {
    await ElMessageBox.confirm(`确定删除「${noteTitle}」？删除后不可恢复。`, '删除笔记', {
      confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning'
    })
    await noteStore.remove(noteId)
    if (route.params.id === noteId) router.push('/notebooks')
  } catch { /* cancelled */ }
}
</script>

<template>
  <aside class="tree-panel compact">
    <div ref="treeBackRef" class="tree-back" @click="goBackFromEditor">
      <el-icon :size="14"><ArrowLeft /></el-icon><span>返回</span>
    </div>
    <div class="tree-scroll">
      <el-tree
        ref="treeRef"
        :data="treeData"
        :props="defaultProps"
        node-key="id"
        :default-expanded-keys="defaultExpandedKeys"
        highlight-current
        :expand-on-click-node="false"
        @node-click="handleNodeClick"
      >
        <template #default="{ data }">
          <div class="tree-slot" :class="data.type">
            <template v-if="data.type === 'cat'">
              <el-icon :size="13" color="#e6a23c"><FolderOpened /></el-icon>
              <span class="tree-label">{{ data.label }}</span>
              <span class="tree-actions">
                <el-button text :icon="Plus" size="small" @click.stop="createNoteInCategory(data.id.replace('cat:', ''))" />
              </span>
              <span class="nb">{{ data.noteCount }}</span>
            </template>
            <template v-else>
              <el-icon :size="12" color="#409eff"><Document /></el-icon>
              <span class="tree-label">{{ data.label }}</span>
              <el-button text :icon="Delete" type="danger" size="small" class="note-del"
                @click.stop="(e: MouseEvent) => handleDeleteNote(e, data.id.replace('note:', ''), data.label)" />
            </template>
          </div>
        </template>
      </el-tree>
      <div class="tree-action" @click="openCreateCategory()">
        <el-icon :size="13"><Plus /></el-icon>
        <span class="tree-label dim">新建目录</span>
      </div>
    </div>
  </aside>
</template>

<style scoped>
.tree-panel {
  width: 260px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  overflow: hidden;
}
.tree-panel.compact { flex: 0 0 33.33%; min-width: 260px; max-width: 340px; }

.tree-back {
  display: flex; align-items: center; gap: 6px; padding: 10px 14px; cursor: pointer;
  font-size: 14px; color: var(--el-text-color-secondary);
  flex-shrink: 0; user-select: none;
}
.tree-back:hover { color: var(--el-color-primary); }

.tree-scroll { flex: 1; overflow-y: auto; padding: 6px 0; display: flex; flex-direction: column; }

/* el-tree 行高加宽 */
:deep(.el-tree-node__content) { min-height: 34px; }

/* el-tree custom slot */
.tree-slot { display: inline-flex; align-items: center; gap: 8px; width: 100%; min-width: 0; font-size: 14px; }
.tree-slot.note { font-size: 13px; }
.tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.tree-label.dim { color: var(--el-text-color-secondary); }
.nb { font-size: 11px; color: var(--el-text-color-secondary); background: var(--el-fill-color); padding: 0 6px; border-radius: 5px; flex-shrink: 0; margin-left: auto; margin-right: 6px; }
.tree-actions { display: flex; gap: 4px; flex-shrink: 0; }
@media (hover: hover) { .tree-actions { visibility: hidden; } .tree-slot:hover .tree-actions { visibility: visible; } }
@media (hover: none) { .tree-actions { opacity: 0.5; } }

.note-del { flex-shrink: 0; }

/* 新建目录 */
.tree-action {
  display: flex; align-items: center; gap: 8px; padding: 10px 12px; cursor: pointer;
  font-size: 14px; transition: background 0.1s;
  border-top: 1px solid var(--el-border-color-light); margin-top: auto;
}
.tree-action:hover { background: var(--el-fill-color-light); }
</style>

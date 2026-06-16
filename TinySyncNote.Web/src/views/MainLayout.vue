<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useNotebookStore } from '../stores/notebook'
import { useRoute, useRouter } from 'vue-router'
import {
  WarningFilled, Setting, SwitchButton, Moon, Sunny, MoreFilled, Document
} from '@element-plus/icons-vue'
import NotebookSidebar from '../components/NotebookSidebar.vue'
import NoteEditorSidebar from '../components/NoteEditorSidebar.vue'

const authStore = useAuthStore()
const notebookStore = useNotebookStore()
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

// ── 笔记本 ──
onMounted(() => notebookStore.fetchAll())

const notebookId = computed(() => route.params.id as string)
const editorNotebookId = computed(() => isEditor.value ? (route.query.nb as string) : null)

const currentNotebookName = computed(() => {
  const id = editorNotebookId.value || notebookId.value
  if (!id) return ''
  const nb = notebookStore.notebooks.find(n => n.id === id)
  if (nb?.name) return nb.name
  return (route.query.nbn as string) || ''
})

function handleMoreCommand(cmd: string) {
  cmd === 'toggle-dark' ? toggleDark() : router.push(cmd)
}
</script>

<template>
  <div class="main-layout">
    <!-- ═══ 顶栏 ═══ -->
    <header class="top-bar">
      <div class="top-bar-left">
        <h2 class="app-title" :class="{ dim: isEditor || isNotebookDetail }" @click="router.push('/notebooks')">TinySyncNote</h2>
        <span v-if="isEditor || isNotebookDetail" class="top-nb-name">{{ currentNotebookName }}</span>
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
      <!-- ── 浏览笔记本：两列（目录 | 笔记） ── -->
      <template v-if="isNotebookDetail">
        <NotebookSidebar />
        <div class="col col-notes">
          <div class="col-header">
            <span class="col-header-label">
              <el-icon :size="14"><Document /></el-icon>
              笔记
            </span>
          </div>
          <router-view />
        </div>
      </template>

      <!-- ── 编辑模式：树状导航（1/3）+ 编辑器（2/3） ── -->
      <template v-else-if="isEditor">
        <div class="editor-sidebar-wrap">
          <NoteEditorSidebar />
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

/* ── 两列布局（浏览模式） ── */
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
.col-notes { flex: 1; overflow: hidden; display: flex; flex-direction: column; }

.editor-area { flex: 1; overflow: hidden; display: flex; flex-direction: column; }

/* ── 全宽 ── */
.main-full { flex: 1; overflow-y: auto; background: var(--el-bg-color-page); }

@media (max-width: 720px) {
  .col-notes { flex: 1; }
  .editor-sidebar-wrap { display: none; }
}
</style>

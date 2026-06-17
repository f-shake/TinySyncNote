<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, computed, watch, nextTick } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import { useNoteStore } from '../stores/note'
import { useSnapshotStore } from '../stores/snapshot'
import type { NoteSnapshot } from '../types'
import { ElMessage, ElNotification } from 'element-plus'
import { Delete, Clock, Share, Promotion, ArrowLeft } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import Vditor from 'vditor'
import 'vditor/dist/index.css'
import { useSync } from '../composables/useSync'
import ShareDialog from '../components/ShareDialog.vue'
import AIChatPanel from '../components/AIChatPanel.vue'
import http from '../utils/http'
import type { AISettings } from '../types'

const route = useRoute()
const router = useRouter()
const noteStore = useNoteStore()
const snapshotStore = useSnapshotStore()

const noteId = ref('')
const title = ref('')
const editorRef = ref<HTMLDivElement | null>(null)
const saving = ref(false)
const loaded = ref(false)
const remoteUpdateBanner = ref(false)
const showSnapshotDrawer = ref(false)
const showShareDialog = ref(false)
const SHOW_AI_KEY = 'tsn_show_ai'
const showAIChat = ref(localStorage.getItem(SHOW_AI_KEY) === 'true')
watch(showAIChat, (v) => localStorage.setItem(SHOW_AI_KEY, String(v)))
const aiSettings = ref<AISettings | null>(null)
const previewSnapshot = ref<NoteSnapshot | null>(null)
const dirty = ref(false)   // 是否有未保存修改
const lastSelectedText = ref('') // 追踪编辑器选中文本，供 AI 使用

// ── AI 面板拖拽调整宽度 ──
const AI_PANEL_WIDTH_KEY = 'tsn_ai_panel_width'
const aiPanelWidth = ref(parseInt(localStorage.getItem(AI_PANEL_WIDTH_KEY) || '360', 10))
const aiResizing = ref(false)

function startAiResize(e: MouseEvent) {
  e.preventDefault()
  aiResizing.value = true
  const startX = e.clientX
  const startW = aiPanelWidth.value

  function onMove(ev: MouseEvent) {
    const w = startW - (ev.clientX - startX) // 向右拖 → AI 面板变窄
    aiPanelWidth.value = Math.max(200, Math.min(600, w))
  }
  function onUp() {
    aiResizing.value = false
    localStorage.setItem(AI_PANEL_WIDTH_KEY, String(aiPanelWidth.value))
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
}

const AUTO_SAVE_KEY = 'tsn_autosave_interval'

function getAutoSaveInterval(): number {
  const saved = localStorage.getItem(AUTO_SAVE_KEY)
  if (saved) {
    const n = parseInt(saved, 10)
    if (n >= 2 && n <= 300) return n * 1000
  }
  return 5000
}

let vditor: Vditor | null = null
let saveTimer: ReturnType<typeof setTimeout> | null = null
let modeCheckTimer: ReturnType<typeof setInterval> | null = null
let lastSavedVersion = 0
let pendingSaveVersion = 0  // 正在保存的版本号，用于过滤自己的 SignalR 通知

// ── 离开页面自动静默保存 ──
onBeforeRouteLeave(async (_to, _from, next) => {
  if (dirty.value) await handleSave()
  next()
})

// ── Ctrl+S 快捷键 ──
function onKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === 's') {
    e.preventDefault()
    handleSave()
  }
}
onMounted(() => document.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))

// ── SignalR 实时同步 ──
const {
  connected: syncConnected,
  onNoteUpdated,
  onNoteDeleted
} = useSync()

onNoteUpdated((evt) => {
  if (evt.noteId !== noteId.value) return

  // 情况 A：SignalR 通知的版本号与 lastSavedVersion 一致 → 自己刚保存成功（HTTP 先到），忽略
  if (evt.newVersion === lastSavedVersion) return

  // 情况 B：自己有正在进行的保存，且 SignalR 通知的版本号正好是保存版本 +1
  // → 这是自己的保存经由 SignalR 回传（WebSocket 快于 HTTP 响应），忽略
  if (pendingSaveVersion > 0 && evt.newVersion === pendingSaveVersion + 1) return

  // 情况 C：真正的跨设备修改
  if (dirty.value) {
    // 本设备有未保存的修改 → 提示用户手动处理
    remoteUpdateBanner.value = true
    ElNotification({
      title: '笔记已更新',
      message: '其他设备修改了此笔记',
      type: 'info',
      duration: 4000
    })
  } else {
    // 本设备无修改 → 静默更新到云端版本
    reloadNote()
  }
})

onNoteDeleted((evt) => {
  if (evt.noteId === noteId.value) {
    // 自己发起的删除不重复提示
    if (noteStore.deletingNoteId === evt.noteId) return
    ElNotification({
      title: '笔记已删除',
      message: '此笔记已被其他设备删除',
      type: 'warning',
      duration: 0
    })
    setTimeout(() => router.push('/notebooks'), 2000)
  }
})

onMounted(async () => {
  // 等待路由初始导航完成，确保 route.params 已正确解析
  // （否则刷新页面时惰性加载的组件可能拿到 undefined 的 params）
  await router.isReady()

  noteId.value = route.params.id as string
  if (!noteId.value) {
    ElMessage.error('笔记ID无效')
    router.push('/notebooks')
    return
  }

  await loadNote()
  loadAISettings()
})

onBeforeUnmount(() => {
  if (saveTimer) clearTimeout(saveTimer)
  if (modeCheckTimer) clearInterval(modeCheckTimer)
  vditor?.destroy()
})

// 仅在 onMounted 之后的 params 变化才触发 — 避免与首次挂载的 loadNote 并发
watch(() => route.params.id, async (id) => {
  if (id && typeof id === 'string' && noteId.value && id !== noteId.value) {
    // 切换笔记前自动保存当前笔记
    if (dirty.value) await handleSave()
    if (saveTimer) clearTimeout(saveTimer)
    noteId.value = id
    await loadNote()
  }
})

async function loadNote() {
  if (!noteId.value) return
  try {
    const note = await noteStore.fetchById(noteId.value)
    if (!note) { router.push('/notebooks'); return }
    title.value = note.title
    lastSavedVersion = note.version

    if (noteStore.selectedCategoryId !== note.categoryId) {
      noteStore.selectedCategoryId = note.categoryId
      noteStore.fetchByCategory(note.categoryId)
    }

    if (vditor) {
      // 已有编辑器 → 静默更新内容，不销毁重建
      vditor.setValue(note.content)
    } else {
      // 首次加载 → 创建编辑器
      loaded.value = true
      await nextTick()
      initEditor(note.content)
    }
    dirty.value = false
  } catch (err: any) {
    const detail = err?.response?.status
      ? `HTTP ${err.response.status}`
      : err?.message || '未知错误'
    ElMessage.error(`加载笔记失败 (${detail})`)
    router.push('/notebooks')
  }
}

function initEditor(content: string) {
  if (!editorRef.value) return
  if (vditor) return // 防止并发时重复初始化

  const isDark = document.documentElement.classList.contains('dark')
  const savedMode = (localStorage.getItem('vditorMode') as 'sv' | 'ir' | 'wysiwyg') || 'ir'

  vditor = new Vditor(editorRef.value, {
    value: content || '',
    height: '100%',
    minHeight: window.innerWidth <= 720 ? 200 : 400,
    mode: savedMode,
    placeholder: '开始书写...',
    cache: { enable: false },
    theme: isDark ? 'dark' : 'classic',
    icon: 'material',
    customWysiwygToolbar: () => {},
    preview: {
      theme: {
        current: isDark ? 'dark' : 'light',
      },
      actions: [],  // 隐藏 Desktop / Tablet / Mobile 设备切换按钮
    },
    toolbar: [
      'headings', 'bold', 'italic', 'strike', '|',
      'list', 'ordered-list', 'check', '|',
      'code', 'inline-code', '|',
      'table', 'link', 'quote', '|',
      'edit-mode', 'fullscreen', 'outline', '|',
      'undo', 'redo'
    ],
    upload: {
      url: '/api/upload/image',
      accept: 'image/*',
      multiple: true,
      fieldName: 'file',
      linkToImgUrl: '',
      extraData: { noteId: noteId.value },
      setHeaders: () => ({ 'Authorization': `Bearer ${localStorage.getItem('tsn_access_token')}` })
    },
    input: () => {
      dirty.value = true
      // 追踪选中文本
      if (vditor) lastSelectedText.value = vditor.getSelection() || ''
      // 防抖自动保存
      if (saveTimer) clearTimeout(saveTimer)
      saveTimer = setTimeout(autoSave, getAutoSaveInterval())
    },
    after: () => {
      // 监听 html 标签 class 变化，同步 Vditor 主题
      const observer = new MutationObserver(() => {
        if (!vditor) return
        const dark = document.documentElement.classList.contains('dark')
        vditor.setTheme(dark ? 'dark' : 'classic', dark ? 'dark' : 'light')
      })
      observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })

      // 轮询检测编辑模式切换（Vditor 未提供模式切换事件）
      modeCheckTimer = setInterval(() => {
        if (!vditor) return
        const mode = vditor.getCurrentMode()
        if (mode && mode !== localStorage.getItem('vditorMode')) {
          localStorage.setItem('vditorMode', mode)
        }
      }, 1000)

      // 追踪编辑器选中文本，供 AI 使用（点击 AI 面板后选区会丢失）
      function trackSelection() {
        if (!vditor) return
        const sel = vditor.getSelection() || window.getSelection()?.toString() || ''
        // 只保存非空选区，防止失去焦点时覆盖为空白
        if (sel) lastSelectedText.value = sel
      }
      // 在编辑器区域内鼠标松手时保存选中文本（发生在 blur 之前）
      document.addEventListener('mouseup', (e) => {
        if (vditor && editorRef.value?.contains(e.target as Node)) trackSelection()
      })
      // 键盘选中（Shift+方向键等）
      document.addEventListener('keyup', (e) => {
        if (vditor && editorRef.value?.contains(e.target as Node)) trackSelection()
      })
    }
  })
}


async function autoSave() {
  if (!vditor || !noteStore.currentNote) return
  const content = vditor.getValue()
  if (!content && !title.value) return

  saving.value = true
  const sendVersion = noteStore.currentNote.version
  pendingSaveVersion = sendVersion  // 记录发送时的版本号，用于过滤回传通知
  try {
    const result = await noteStore.update(
      noteId.value,
      title.value || '无标题笔记',
      content,
      sendVersion
    )
    lastSavedVersion = result.version
    dirty.value = false
    remoteUpdateBanner.value = false
  } catch (err: any) {
    if (err.response?.status === 409) {
      ElMessage.warning('内容冲突，请解决冲突后重试')
    }
  } finally {
    saving.value = false
    pendingSaveVersion = 0  // 无论成功失败都清除
  }
}

async function handleSave() {
  if (saveTimer) clearTimeout(saveTimer)
  await autoSave()
}

function handleSaveTagClick() {
  if (dirty.value && !saving.value) {
    handleSave()
  }
}


// 重新加载笔记（其他设备修改后）
async function reloadNote() {
  remoteUpdateBanner.value = false
  await loadNote()
}

// ── 快照 ──
async function handleCreateSnapshot() {
  await snapshotStore.create(noteId.value)
  await snapshotStore.fetchByNote(noteId.value)
}

function openSnapshotDrawer() {
  showSnapshotDrawer.value = true
  snapshotStore.fetchByNote(noteId.value)
}

async function handleRestoreSnapshot(snapshot: NoteSnapshot) {
  try {
    await ElMessageBox.confirm(
      `确定恢复到版本 v${snapshot.version}（${new Date(snapshot.snapshotAt).toLocaleString()}）？当前内容将被另存为快照。`,
      '恢复确认',
      { confirmButtonText: '恢复', cancelButtonText: '取消', type: 'info' }
    )
    await snapshotStore.restore(noteId.value, snapshot.id)
    // 刷新笔记内容
    vditor?.destroy()
    vditor = null
    loaded.value = false
    showSnapshotDrawer.value = false
    await loadNote()
  } catch { /* cancelled */ }
}

function previewSnapshotItem(snapshot: NoteSnapshot) {
  previewSnapshot.value = snapshot
  // 加载正文（列表接口不返回 content）
  if (!snapshot.content && snapshot.id) {
    snapshotStore.fetchById(noteId.value, snapshot.id).then(detail => {
      if (detail) previewSnapshot.value = detail
    })
  }
}

async function handleDeleteSnapshot(snapshot: NoteSnapshot) {
  try {
    await ElMessageBox.confirm(
      `确定删除 v${snapshot.version}（${new Date(snapshot.snapshotAt).toLocaleString()}）的快照？删除后不可恢复。`,
      '删除快照',
      { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }
    )
    await snapshotStore.remove(noteId.value, snapshot.id)
    if (previewSnapshot.value?.id === snapshot.id) {
      previewSnapshot.value = null
    }
  } catch { /* cancelled */ }
}

function formatCharCount(length: number | undefined): string {
  if (length === undefined || length === null) return ''
  return `${length} 字`
}

// ── 分享 ──
function openShareDialog() {
  showShareDialog.value = true
}

// ── AI ──
async function loadAISettings() {
  try {
    const res = await http.get<Record<string, string>>('/api/settings')
    const data = res.data
    aiSettings.value = {
      ai_url: data.ai_url || '',
      ai_key: data.ai_key || '',
      ai_model: data.ai_model || ''
    }
  } catch { /* ignore */ }
}

function openAIChat() {
  if (showAIChat.value) {
    showAIChat.value = false
    return
  }
  if (!aiSettings.value?.ai_key) {
    ElMessage.warning('请先在设置中配置 AI')
    return
  }
  showAIChat.value = true
}

// ── 移动端返回 ──
function goBack() {
  const nb = route.query.nb as string
  if (nb) router.push(`/notebook/${nb}`)
  else router.push('/notebooks')
}

const editorActions = computed(() => ({
  getNoteContent: () => vditor?.getValue() || '',
  replaceNoteContent: (content: string) => {
    vditor?.setValue(content)
    dirty.value = true
    startSaveTimer()
  },
  insertAtCursor: (text: string) => {
    vditor?.insertValue(text)
    dirty.value = true
    startSaveTimer()
  },
  getSelectedText: () => lastSelectedText.value || vditor?.getSelection() || '',
  setTitle: (t: string) => { title.value = t; dirty.value = true; startSaveTimer() }
}))

function startSaveTimer() {
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(autoSave, getAutoSaveInterval())
}

// 触发防抖保存（标题变化）
function onTitleChange() {
  dirty.value = true
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(autoSave, getAutoSaveInterval())
}

</script>

<template>
  <div class="note-editor-view" v-if="loaded">
    <!-- 顶栏 -->
    <div class="editor-toolbar">
      <el-button
        class="mobile-back-btn"
        text
        :icon="ArrowLeft"
        @click="goBack"
      />
      <div class="title-area">
        <el-input
          v-model="title"
          placeholder="笔记标题"
          class="title-input"
          maxlength="500"
          @input="onTitleChange"
        />
      </div>

      <div class="toolbar-actions">
        <!-- 同步状态 -->
        <el-tooltip content="实时同步状态" placement="bottom">
          <el-tag
            :type="syncConnected ? 'success' : 'danger'"
            size="small"
            effect="plain"
          >
            {{ syncConnected ? '已连接' : '离线' }}
          </el-tag>
        </el-tooltip>

        <el-tag
          :type="saving ? 'info' : dirty ? 'warning' : 'success'"
          size="small"
          effect="plain"
          :style="{ cursor: dirty && !saving ? 'pointer' : 'default' }"
          @click="handleSaveTagClick"
        >
          {{ saving ? '保存中' : dirty ? '未保存' : '已保存' }}
        </el-tag>

        <el-button text :icon="Clock" @click="openSnapshotDrawer">历史版本</el-button>
        <el-button text :icon="Share" @click="openShareDialog">分享</el-button>
        <el-button text :icon="Promotion" @click="openAIChat">AI</el-button>
      </div>
    </div>

    <!-- 远程更新提示 -->
    <el-alert
      v-if="remoteUpdateBanner"
      title="此笔记已被其他设备修改"
      type="info"
      show-icon
      :closable="true"
      @close="remoteUpdateBanner = false"
    >
      <template #default>
        <el-button size="small" @click="reloadNote">重新加载</el-button>
      </template>
    </el-alert>

    <!-- Vditor 容器 + AI 面板 -->
    <div class="editor-body">
      <div class="editor-container">
        <div ref="editorRef" class="vditor-wrap" />
      </div>

      <!-- AI 面板拖拽手柄 -->
      <div
        v-if="showAIChat"
        class="drag-handle-v"
        :class="{ active: aiResizing }"
        @mousedown="startAiResize"
      />

      <!-- ═══ AI 助手 ═══ -->
      <AIChatPanel
        v-show="showAIChat"
        :visible="showAIChat"
        :editor="editorActions"
        :settings="aiSettings"
        :style="{ width: aiPanelWidth + 'px', flexShrink: 0 }"
        @close="showAIChat = false"
      />
    </div>
  </div>

  <!-- ═══ 分享对话框 ═══ -->
  <ShareDialog
    v-if="showShareDialog"
    :note-id="noteId"
    :note-title="title"
    :on-save="handleSave"
    @close="showShareDialog = false"
  />



  <el-drawer
    v-model="showSnapshotDrawer"
    title="历史版本"
    size="380px"
    :close-on-click-modal="false"
  >
    <div class="snapshot-drawer-body">
      <el-button
        type="primary"
        :icon="Clock"
        :loading="snapshotStore.loading"
        @click="handleCreateSnapshot"
        class="create-snapshot-btn"
      >
        创建快照
      </el-button>

      <div v-if="snapshotStore.snapshots.length === 0" class="snapshot-empty">
        <el-empty description="暂无历史版本" :image-size="80" />
      </div>

      <div v-else class="snapshot-list">
        <div
          v-for="s in snapshotStore.snapshots"
          :key="s.id"
          class="snapshot-item"
          :class="{ active: previewSnapshot?.id === s.id }"
          @click="previewSnapshotItem(s)"
        >
          <div class="snapshot-header">
            <el-tag size="small" :type="s.snapshotType === 'Manual' ? 'primary' : 'info'">
              {{ s.snapshotType === 'Manual' ? '手动' : '自动' }}
            </el-tag>
            <span class="snapshot-version">v{{ s.version }}</span>
            <span class="snapshot-chars">{{ formatCharCount(s.contentLength) }}</span>
            <span class="snapshot-time">{{ new Date(s.snapshotAt).toLocaleString() }}</span>
            <el-button
              text
              size="small"
              type="danger"
              :icon="Delete"
              @click.stop="handleDeleteSnapshot(s)"
              class="snapshot-delete-btn"
            />
          </div>

          <div v-if="previewSnapshot?.id === s.id" class="snapshot-preview">
            <div class="preview-content">{{ previewSnapshot?.content || '（加载中...）' }}</div>
            <div class="snapshot-actions">
              <el-button
                type="warning"
                size="small"
                @click.stop="handleRestoreSnapshot(s)"
                class="action-btn"
              >
                恢复到此版本
              </el-button>
              <el-button
                type="danger"
                size="small"
                :icon="Delete"
                @click.stop="handleDeleteSnapshot(s)"
                class="action-btn"
              >
                删除
              </el-button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </el-drawer>
</template>

<style scoped>
.note-editor-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--el-bg-color);
  overflow: hidden;
}

.editor-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  z-index: 10;
}

.title-area {
  flex: 1;
  min-width: 0;
}

.title-input :deep(.el-input__wrapper) {
  border: none;
  box-shadow: none !important;
  font-size: 18px;
  font-weight: 600;
}

.title-input :deep(.el-input__inner) {
  border: none;
  font-size: 18px;
  font-weight: 600;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.editor-container {
  flex: 1;
  overflow: hidden;
  padding: 0;
}

.editor-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

@media (min-width: 721px) {
  .editor-body { flex-direction: row; }
}

.vditor-wrap {
  height: 100%;
}

/* Vditor 覆盖 — 让它占满容器 */
.vditor-wrap :deep(.vditor) {
  border: none !important;
  border-radius: 0 !important;
}

.vditor-wrap :deep(.vditor-toolbar) {
  border-bottom: 1px solid var(--el-border-color-light) !important;
}

/* ── 拖拽手柄 ── */
.drag-handle-v {
  flex-shrink: 0;
  width: 0;
  cursor: col-resize;
  position: relative;
  z-index: 10;
  transition: width 0.15s, background 0.15s;
}
.drag-handle-v::before {
  content: '';
  position: absolute;
  top: 0; bottom: 0;
  left: -4px; right: -4px;
}
.drag-handle-v:hover,
.drag-handle-v.active {
  width: 3px;
  background: var(--el-color-primary);
}

/* ── 快照抽屉 ── */
.snapshot-drawer-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.create-snapshot-btn {
  width: 100%;
}

.snapshot-empty {
  padding: 40px 0;
}

.snapshot-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.snapshot-item {
  padding: 10px 8px;
  cursor: pointer;
  transition: background 0.15s;
  border-bottom: 1px solid var(--el-border-color-light);
}

.snapshot-item:last-child {
  border-bottom: none;
}

.snapshot-item:hover {
  background: var(--el-fill-color-light);
}

.snapshot-item.active {
  background: var(--el-color-primary-light-9);
}

.snapshot-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.snapshot-version {
  font-weight: 600;
  font-size: 12px;
  color: var(--el-color-primary);
}

.snapshot-chars {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-lighter);
  padding: 1px 6px;
  border-radius: 4px;
}

.snapshot-time {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  margin-left: auto;
}

.snapshot-delete-btn {
  flex-shrink: 0;
}

.snapshot-preview {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--el-border-color-light);
}

.preview-content {
  font-size: 12px;
  line-height: 1.5;
  color: var(--el-text-color-secondary);
  max-height: 200px;
  overflow-y: auto;
  white-space: pre-wrap;
  margin-bottom: 12px;
  padding: 8px;
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
}

.snapshot-actions {
  display: flex;
  gap: 8px;
}

.action-btn {
  flex: 1;
}

.restore-btn {
  width: 100%;
}

/* ════════════════════════════════════════
   编辑器 — 移动端自适应
   ════════════════════════════════════════ */
@media (max-width: 1100px) {
  .toolbar-actions .el-button span { display: none; }
  .toolbar-actions { gap: 2px; }
  .toolbar-actions .el-button { padding: 5px 6px; font-size: 12px; }
}

@media (max-width: 900px) {
  .editor-toolbar { flex-wrap: wrap; gap: 4px; padding: 6px 8px; }
  .title-area { flex: 1; min-width: 100px; }
}

@media (max-width: 720px) {
  .toolbar-actions .el-tag { display: none; }
  .mobile-back-btn { display: inline-flex; }
  .drag-handle-v { display: none; }
  .editor-body > .ai-panel { width: auto !important; flex-shrink: unset !important; }
}

@media (min-width: 721px) {
  .mobile-back-btn { display: none; }
}

@media (max-width: 480px) {
  .editor-toolbar { padding: 4px 4px; gap: 2px; }
  .toolbar-actions .el-button { padding: 4px 4px; }
  .toolbar-actions { gap: 2px; }
}

</style>
<style>
html.dark .vditor-reset pre,
html.dark .vditor-reset .hljs,
html.dark .vditor-ir__marker--pre,
html.dark .vditor-wysiwyg pre,
html.dark .vditor-wysiwyg code {
  background: #1e1e1e !important;
  color: #d4d4d4 !important;
}
/* 行内代码 —— 柔和暗色 */
html.dark .vditor-reset code:not(.hljs):not([class*="language-"]),
html.dark .vditor-ir__marker--info code,
html.dark .vditor-ir__marker code {
  background: #2d2d2d !important;
  color: #ce9178 !important;
  border: 1px solid #3a3a3a !important;
  padding: 1px 5px !important;
  border-radius: 3px !important;
}
/* 编辑器内围栏代码块 —— 去掉暗蓝底 */
html.dark .vditor-ir__marker--pre {
  background: #252526 !important;
  border-color: #3a3a3a !important;
}
html.dark .vditor-ir__marker--pre .vditor-ir__marker {
  background: transparent !important;
}
html.dark .vditor-copy span { color: #ccc !important; }
html.dark .vditor-linenumber__rows { background: #1e1e1e !important; }
/* highlight.js VS Code Dark+ */
html.dark .hljs-keyword { color: #569cd6 !important; }
html.dark .hljs-string { color: #ce9178 !important; }
html.dark .hljs-comment, html.dark .hljs-quote { color: #6a9955 !important; }
html.dark .hljs-number { color: #b5cea8 !important; }
html.dark .hljs-built_in { color: #4ec9b0 !important; }
html.dark .hljs-attr, html.dark .hljs-attribute { color: #9cdcfe !important; }
html.dark .hljs-selector-tag, html.dark .hljs-tag, html.dark .hljs-name, html.dark .hljs-literal { color: #569cd6 !important; }
html.dark .hljs-selector-id, html.dark .hljs-selector-class, html.dark .hljs-section, html.dark .hljs-deletion { color: #e06c75 !important; }
html.dark .hljs-type { color: #4ec9b0 !important; }
html.dark .hljs-addition { color: #6a9955 !important; }
html.dark .hljs-params { color: #d4d4d4 !important; }
html.dark .hljs-meta { color: #dcdcaa !important; }
html.dark .hljs-link { color: #569cd6 !important; text-decoration: underline; }
html.dark .hljs-title, html.dark .hljs-title.function_ { color: #dcdcaa !important; }
</style>

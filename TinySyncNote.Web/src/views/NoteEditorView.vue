<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, nextTick } from 'vue'
import { useRoute, useRouter, onBeforeRouteLeave } from 'vue-router'
import { useNoteStore } from '../stores/note'
import { useSnapshotStore } from '../stores/snapshot'
import type { NoteSnapshot } from '../types'
import { ElMessage, ElNotification } from 'element-plus'
import { ArrowLeft, Delete, Download, Clock, Upload } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import Vditor from 'vditor'
import 'vditor/dist/index.css'
import { useSync } from '../composables/useSync'

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
const previewSnapshot = ref<NoteSnapshot | null>(null)
const dirty = ref(false)   // 是否有未保存修改

let vditor: Vditor | null = null
let saveTimer: ReturnType<typeof setTimeout> | null = null
let modeCheckTimer: ReturnType<typeof setInterval> | null = null
let lastSavedVersion = 0
let pendingSaveVersion = 0  // 正在保存的版本号，用于过滤自己的 SignalR 通知

// ── 离开页面确认 ──
onBeforeRouteLeave(async (_to, _from, next) => {
  if (dirty.value) {
    try {
      await ElMessageBox.confirm(
        '有未保存的修改，是否保存后离开？',
        '离开确认',
        {
          confirmButtonText: '保存并离开',
          cancelButtonText: '放弃修改',
          distinguishCancelAndClose: true,
          type: 'warning'
        }
      )
      await handleSave()
      next()
    } catch (action: any) {
      if (action === 'cancel') {
        next()
      } else {
        next(false) // 关闭弹窗 → 不离开
      }
    }
  } else {
    next()
  }
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
  remoteUpdateBanner.value = true
  ElNotification({
    title: '笔记已更新',
    message: '其他设备修改了此笔记',
    type: 'info',
    duration: 4000
  })
})

onNoteDeleted((evt) => {
  if (evt.noteId === noteId.value) {
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
  noteId.value = route.params.id as string

  await loadNote()
})

onBeforeUnmount(() => {
  if (saveTimer) clearTimeout(saveTimer)
  if (modeCheckTimer) clearInterval(modeCheckTimer)
  vditor?.destroy()
})

watch(() => route.params.id, async (id) => {
  if (id && typeof id === 'string' && id !== noteId.value) {
    noteId.value = id
    if (saveTimer) clearTimeout(saveTimer)
    vditor?.destroy()
    vditor = null
    loaded.value = false
    await loadNote()
  }
})

async function loadNote() {
  try {
    const note = await noteStore.fetchById(noteId.value)
    if (!note) { router.push('/notebooks'); return }
    title.value = note.title
    lastSavedVersion = note.version  // 同步当前版本，防止 SignalR 事件误判

    // 先显示 DOM，再初始化编辑器
    loaded.value = true
    await nextTick()
    initEditor(note.content)
  } catch {
    ElMessage.error('加载笔记失败')
    router.push('/notebooks')
  }
}

function initEditor(content: string) {
  if (!editorRef.value) return

  const isDark = document.documentElement.classList.contains('dark')
  const savedMode = (localStorage.getItem('vditorMode') as 'sv' | 'ir' | 'wysiwyg') || 'ir'

  vditor = new Vditor(editorRef.value, {
    value: content || '',
    height: '100%',
    minHeight: 400,
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
      // 图片粘贴 → base64
      handler: async (files: File[]) => {
        for (const file of files) {
          const base64 = await fileToBase64(file)
          if (vditor) {
            vditor.insertValue(`![${file.name}](${base64})`)
          }
        }
        return JSON.stringify({ msg: '', code: 0, data: { errFiles: [], succMap: {} } })
      },
      accept: 'image/*',
      multiple: true
    },
    input: () => {
      dirty.value = true
      // 防抖自动保存
      if (saveTimer) clearTimeout(saveTimer)
      saveTimer = setTimeout(autoSave, 3000)
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
    }
  })
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
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

async function handleDelete() {
  try {
    await ElMessageBox.confirm('确定删除这篇笔记？删除后不可恢复。', '删除确认', {
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await noteStore.remove(noteId.value)
    ElMessage.success('笔记已删除')
    router.back()
  } catch {
    // 取消
  }
}

function goBack() {
  // 先保存再返回
  handleSave().then(() => router.back())
}

// 重新加载笔记（其他设备修改后）
async function reloadNote() {
  remoteUpdateBanner.value = false
  vditor?.destroy()
  vditor = null
  loaded.value = false
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
}

// ── 导出 ──
function exportAsMarkdown() {
  window.open(`/api/export/note/${noteId.value}`, '_blank')
}

// 触发防抖保存（标题变化）
function onTitleChange() {
  dirty.value = true
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(autoSave, 3000)
}

</script>

<template>
  <div class="note-editor-view" v-if="loaded">
    <!-- 顶栏 -->
    <div class="editor-toolbar">
      <el-button text :icon="ArrowLeft" @click="goBack">返回</el-button>

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
        >
          {{ saving ? '保存中' : dirty ? '未保存' : '已保存' }}
        </el-tag>

        <el-button text :icon="Clock" @click="openSnapshotDrawer">历史版本</el-button>
        <el-button text :icon="Download" @click="handleSave">保存</el-button>
        <el-button text :icon="Upload" @click="exportAsMarkdown">导出</el-button>
        <el-popconfirm title="确定删除这篇笔记？" @confirm="handleDelete">
          <template #reference>
            <el-button text :icon="Delete" type="danger">删除</el-button>
          </template>
        </el-popconfirm>
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

    <!-- Vditor 容器 -->
    <div class="editor-container">
      <div ref="editorRef" class="vditor-wrap" />
    </div>
  </div>

  <!-- ═══ 快照历史抽屉 ═══ -->
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
            <span class="snapshot-time">{{ new Date(s.snapshotAt).toLocaleString() }}</span>
          </div>

          <div class="snapshot-title">{{ s.title }}</div>

          <div v-if="previewSnapshot?.id === s.id" class="snapshot-preview">
            <div class="preview-content">{{ s.content || '（空）' }}</div>
            <el-button
              type="warning"
              size="small"
              @click.stop="handleRestoreSnapshot(s)"
              class="restore-btn"
            >
              恢复到此版本
            </el-button>
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
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.snapshot-item:hover {
  border-color: var(--el-color-primary-light-5);
}

.snapshot-item.active {
  border-color: var(--el-color-primary);
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

.snapshot-time {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  margin-left: auto;
}

.snapshot-title {
  font-size: 14px;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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
}

@media (max-width: 480px) {
  .editor-toolbar { padding: 4px 4px; gap: 2px; }
  .toolbar-actions .el-button { padding: 4px 4px; }
  .toolbar-actions { gap: 2px; }
}
</style>

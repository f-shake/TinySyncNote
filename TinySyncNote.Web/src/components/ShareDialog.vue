<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useShareStore } from '../stores/share'
import { ElMessage } from 'element-plus'
import { Download, Link, User } from '@element-plus/icons-vue'

const props = defineProps<{
  noteId: string
  noteTitle?: string
  onSave?: () => Promise<void>
}>()
const emit = defineEmits<{ close: [] }>()

const shareStore = useShareStore()
const activeTab = ref('markdown')
const exporting = ref(false)
const sharing = ref(false)
const creatingLink = ref(false)
const htmlTheme = ref('light')

// 导出图片处理方式
const mdAssets = ref<'none' | 'embed' | 'external'>('none')
const htmlAssets = ref<'embed' | 'external'>('embed')
const windowWidth = ref(window.innerWidth)
function onResize() { windowWidth.value = window.innerWidth }
const dialogWidth = computed(() => windowWidth.value <= 720 ? '92vw' : '540px')

// 打开时先保存，确保内容最新
onMounted(async () => {
  if (props.onSave) await props.onSave()
  shareStore.fetchPublicLinks(props.noteId)
  window.addEventListener('resize', onResize)
})
onUnmounted(() => window.removeEventListener('resize', onResize))

// ── Tab 1: 导出 Markdown ──
async function handleExportMarkdown() {
  exporting.value = true
  try {
    const { data, filename } = await shareStore.exportAsMarkdown(props.noteId, mdAssets.value)
    downloadBlob(data, filename)
    ElMessage.success('导出成功')
  } catch { ElMessage.error('导出失败') }
  finally { exporting.value = false }
}

// ── Tab 2: 导出 HTML ──
async function handleExportHtml() {
  exporting.value = true
  try {
    const { data, filename } = await shareStore.exportAsHtml(props.noteId, htmlTheme.value, htmlAssets.value)
    downloadBlob(data, filename)
    ElMessage.success('导出成功')
  } catch { ElMessage.error('导出失败') }
  finally { exporting.value = false }
}

// ── Tab 3: 分享给用户 ──
const searchQuery = ref('')
const selectedUser = ref<{ id: string; username: string } | null>(null)

function onSearchSelect(item: { id: string; username: string }) {
  selectedUser.value = item
}

function clearSelectedUser() {
  selectedUser.value = null
  searchQuery.value = ''
}

async function handleShareToUser() {
  if (!selectedUser.value) return
  sharing.value = true
  try {
    if (props.onSave) await props.onSave()
    await shareStore.shareNote(props.noteId, selectedUser.value.id)
    clearSelectedUser()
  } catch { /* error shown by store */ }
  finally { sharing.value = false }
}

// ── Tab 4: 公开链接 ──
const expiresIn = ref(0)
const expireOptions = [
  { value: 1, label: '1 天' },
  { value: 7, label: '7 天' },
  { value: 30, label: '30 天' },
  { value: 0, label: '永久' },
]

function calcExpiresAt(days: number): string | undefined {
  if (days <= 0) return undefined
  const d = new Date()
  d.setDate(d.getDate() + days)
  return d.toISOString()
}

async function handleCreatePublicLink() {
  creatingLink.value = true
  try {
    if (props.onSave) await props.onSave()
    await shareStore.createPublicLink(props.noteId, calcExpiresAt(expiresIn.value))
  } catch { /* error shown by store */ }
  finally { creatingLink.value = false }
}

function copyToClipboard(text: string) {
  navigator.clipboard.writeText(text).then(() => {
    ElMessage.success('已复制到剪贴板')
  })
}

async function handleRevokeLink(shareId: string) {
  await shareStore.revokePublicLink(shareId)
}

// ── 通用 ──
function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}
</script>

<template>
  <el-dialog
    :model-value="true"
    title="分享笔记"
    :width="dialogWidth"
    @close="emit('close')"
    :close-on-click-modal="false"
    class="share-dialog"
  >
    <el-tabs v-model="activeTab">
      <!-- Tab 1: 导出 Markdown -->
      <el-tab-pane label="导出 Markdown" name="markdown">
        <div class="share-tab-body">
          <p class="share-tab-desc">导出 Markdown 文件，可选择图片和附件处理方式</p>
          <div class="assets-options">
            <el-radio-group v-model="mdAssets">
              <el-radio value="none">不包含附件</el-radio>
              <el-radio value="embed">Base64内嵌</el-radio>
              <el-radio value="external">作为附件</el-radio>
            </el-radio-group>
          </div>
          <p class="share-tab-hint">{{ mdAssets === 'embed' ? '图片和附件以 base64 编码嵌入 Markdown，体积较大但单文件即可使用。' : mdAssets === 'external' ? '导出为 ZIP，附件放在笔记同级的 .assets/ 文件夹中。' : '仅导出纯文本，不含图片和附件。' }}</p>
          <el-button type="primary" :icon="Download" :loading="exporting" @click="handleExportMarkdown">
            {{ mdAssets === 'external' ? '下载 .zip 文件' : '下载 .md 文件' }}
          </el-button>
        </div>
      </el-tab-pane>

      <!-- Tab 2: 导出 HTML -->
      <el-tab-pane label="导出 HTML" name="html">
        <div class="share-tab-body">
          <p class="share-tab-desc">导出为渲染后的完整 HTML 文件</p>
          <div class="html-theme-row">
            <span class="share-label">主题：</span>
            <el-radio-group v-model="htmlTheme" size="small">
              <el-radio-button value="light">浅色</el-radio-button>
              <el-radio-button value="dark">深色</el-radio-button>
            </el-radio-group>
          </div>
          <div class="assets-options">
            <el-radio-group v-model="htmlAssets">
              <el-radio value="embed">Base64内嵌</el-radio>
              <el-radio value="external">作为附件</el-radio>
            </el-radio-group>
          </div>
          <p class="share-tab-hint">{{ htmlAssets === 'embed' ? '图片以 base64 编码嵌入 HTML，单文件即可使用。' : '导出为 ZIP，图片放在笔记同级的 .assets/ 文件夹中。' }}</p>
          <el-button type="primary" :icon="Download" :loading="exporting" @click="handleExportHtml">
            {{ htmlAssets === 'external' ? '下载 .zip 文件' : '下载 .html 文件' }}
          </el-button>
        </div>
      </el-tab-pane>

      <!-- Tab 3: 分享给用户 -->
      <el-tab-pane label="分享给用户" name="user">
        <div class="share-tab-body">
          <p class="share-tab-desc">搜索用户名并分享笔记，对方将在"共享的笔记"中看到</p>

          <el-autocomplete
            v-model="searchQuery"
            :fetch-suggestions="(q: string, cb: any) => { shareStore.searchUsers(q); cb(shareStore.searchResults.map(r => ({ value: r.username, ...r }))) }"
            placeholder="输入用户名搜索…"
            :trigger-on-focus="false"
            clearable
            class="user-search-input"
            @select="onSearchSelect"
          />

          <div v-if="selectedUser" class="selected-user">
            <el-tag closable @close="clearSelectedUser" type="info">
              <el-icon :size="12"><User /></el-icon>
              {{ selectedUser.username }}
            </el-tag>
            <el-button type="primary" :loading="sharing" @click="handleShareToUser" size="small">
              确认分享
            </el-button>
          </div>
        </div>
      </el-tab-pane>

      <!-- Tab 4: 公开链接 -->
      <el-tab-pane label="公开链接" name="public">
        <div class="share-tab-body">
          <p class="share-tab-desc">创建公开链接，站外用户无需登录即可查看</p>

          <div class="public-link-create">
            <el-select v-model="expiresIn" placeholder="过期时间" size="small" style="width: 120px">
              <el-option v-for="opt in expireOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
            </el-select>
            <el-button type="primary" :icon="Link" :loading="creatingLink" @click="handleCreatePublicLink" size="small">
              创建链接
            </el-button>
          </div>

          <div v-if="shareStore.publicLinks.length === 0" class="public-link-empty">
            暂无公开链接
          </div>

          <div v-else class="public-link-list">
            <div v-for="link in shareStore.publicLinks" :key="link.id" class="public-link-item">
              <div class="public-link-url-row">
                <el-input :model-value="link.shareUrl" readonly size="small" class="public-link-input" />
                <el-button size="small" @click="copyToClipboard(link.shareUrl)">复制</el-button>
                <el-button
                  v-if="link.isActive"
                  type="danger"
                  size="small"
                  @click="handleRevokeLink(link.id)"
                >
                  撤销
                </el-button>
              </div>
              <div class="public-link-meta">
                <span v-if="link.expiresAt" class="public-link-expires">
                  过期：{{ new Date(link.expiresAt).toLocaleDateString() }}
                </span>
                <span v-else class="public-link-expires">永不过期</span>
              </div>
            </div>
          </div>
        </div>
      </el-tab-pane>
    </el-tabs>
  </el-dialog>
</template>

<style scoped>
.share-tab-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-height: 120px;
}

.share-tab-desc {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin: 0;
}

.share-label {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}

.html-theme-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.assets-options {
  padding-left: 4px;
}
.share-tab-hint {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}

.user-search-input {
  width: 100%;
}

.selected-user {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* ── 公开链接 ── */
.public-link-create {
  display: flex;
  gap: 8px;
  align-items: center;
}

.public-link-empty {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  text-align: center;
  padding: 16px 0;
}

.public-link-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.public-link-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.public-link-url-row {
  display: flex;
  gap: 6px;
  align-items: stretch;
}

.public-link-input {
  flex: 1;
}

.public-link-input {
  flex: 1;
}

.public-link-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
@media (max-width: 720px) {
  .share-dialog :deep(.el-dialog__body) { padding: 10px; overflow-x: hidden; }
  .share-dialog :deep(.el-tabs__content) { overflow-x: hidden; }
  .share-dialog :deep(.el-overlay-dialog) { overflow: hidden; }
  .public-link-url-row { flex-wrap: wrap; }
  .public-link-url-row .el-button { flex-shrink: 0; }
  .user-search-input { max-width: 100%; }
}
</style>

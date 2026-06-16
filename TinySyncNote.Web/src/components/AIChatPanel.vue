<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { Promotion, Loading, Close } from '@element-plus/icons-vue'
import type { AISettings, AIChatMessage } from '../types'
import { runAIChat } from '../utils/ai'
import type { EditorActions } from '../utils/ai'
import { marked } from 'marked'

const props = defineProps<{
  visible: boolean
  editor: EditorActions
  settings: AISettings | null
}>()

const emit = defineEmits<{
  'close': []
}>()

interface DisplayMessage {
  role: 'user' | 'assistant'
  content: string
}

const messages = ref<DisplayMessage[]>([])
const inputText = ref('')
const loading = ref(false)
const msgListRef = ref<HTMLDivElement | null>(null)
const history: AIChatMessage[] = []

function scrollToBottom() {
  nextTick(() => {
    if (msgListRef.value) {
      msgListRef.value.scrollTop = msgListRef.value.scrollHeight
    }
  })
}

async function send() {
  const text = inputText.value.trim()
  if (!text || loading.value) return

  if (!props.settings?.ai_key) {
    ElMessage.warning('请先在设置中配置 AI')
    return
  }

  inputText.value = ''
  messages.value.push({ role: 'user', content: text })
  scrollToBottom()

  messages.value.push({ role: 'assistant', content: '' })
  const assistantMsg = messages.value[messages.value.length - 1]
  loading.value = true

  try {
    await runAIChat(text, history, props.settings, props.editor, (chunk) => {
      assistantMsg.content += chunk
      scrollToBottom()
    })
  } catch (err: any) {
    assistantMsg.content = `出错了：${err.message}`
  } finally {
    loading.value = false
    scrollToBottom()
  }
}

function renderMarkdown(text: string): string {
  if (!text) return ''
  try {
    return marked.parse(text, { async: false }) as string
  } catch {
    return text
  }
}
</script>

<template>
  <div v-if="visible" class="ai-panel">
      <div class="ai-panel-header">
        <span>AI 助手</span>
        <el-button text :icon="Close" size="small" @click="emit('close')" />
      </div>

      <div ref="msgListRef" class="ai-msg-list" v-if="messages.length > 0">
        <div v-for="(msg, i) in messages" :key="i" class="msg" :class="msg.role">
          <div class="msg-label">{{ msg.role === 'user' ? '你' : 'AI' }}</div>
          <div class="msg-body" v-html="renderMarkdown(msg.content)" />
        </div>
      </div>
      <div v-else class="ai-empty">
        <span>向 AI 提问，让它帮你编辑笔记</span>
      </div>

      <div class="ai-footer">
        <div v-if="loading" class="ai-loading">
          <el-icon class="is-loading"><Loading /></el-icon>
        </div>
        <el-input
          v-model="inputText"
          :disabled="loading"
          placeholder="输入指令..."
          size="small"
          @keyup.enter="send"
        >
          <template #append>
            <el-button :icon="Promotion" @click="send" :loading="loading" />
          </template>
        </el-input>
      </div>
    </div>
</template>

<style scoped>
.ai-panel {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  flex-shrink: 0;
  animation: slideInRight 0.25s ease-out;
}

@media (max-width: 720px) {
  .ai-panel { animation: slideInUp 0.25s ease-out; }
}

.ai-panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  border-bottom: 1px solid var(--el-border-color-light);
}

.ai-msg-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px 12px;
  min-height: 0;
}

.msg {
  padding: 8px 10px;
  border-radius: 6px;
  font-size: 13px;
  line-height: 1.5;
}

.msg.user {
  background: var(--el-color-primary-light-9);
  align-self: flex-end;
  max-width: 85%;
}

.msg.assistant {
  background: var(--el-fill-color-lighter);
  align-self: flex-start;
  max-width: 100%;
  width: 100%;
}

.msg-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  margin-bottom: 2px;
}

.msg-body { color: var(--el-text-color-primary); word-break: break-word; }
.msg-body :deep(p) { margin: 0 0 6px 0; }
.msg-body :deep(p:last-child) { margin-bottom: 0; }
.msg-body :deep(code) { background: var(--el-fill-color); padding: 1px 4px; border-radius: 3px; font-size: 12px; }
.msg-body :deep(pre) { background: var(--el-fill-color-darker); padding: 8px; border-radius: 4px; overflow-x: auto; margin: 6px 0; }
.msg-body :deep(pre code) { background: none; padding: 0; }
.msg-body :deep(ul), .msg-body :deep(ol) { padding-left: 18px; margin: 4px 0; }

.ai-empty {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  min-height: 0;
}

.ai-footer {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-top: 1px solid var(--el-border-color-light);
}

.ai-loading {
  flex-shrink: 0;
  color: var(--el-color-primary);
}

.ai-footer .el-input { flex: 1; }

@keyframes slideInRight {
  from { transform: translateX(100%); }
  to { transform: translateX(0); }
}

@keyframes slideInUp {
  from { transform: translateY(100%); }
  to { transform: translateY(0); }
}
</style>

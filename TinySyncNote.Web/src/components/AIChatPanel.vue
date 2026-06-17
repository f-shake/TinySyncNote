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
  role: 'user' | 'assistant' | 'tool'
  content: string
}

const messages = ref<DisplayMessage[]>([])
const inputText = ref('')
const loading = ref(false)
const msgListRef = ref<HTMLDivElement | null>(null)
const history = ref<AIChatMessage[]>([])

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

  loading.value = true

  try {
    await runAIChat(
      text, history.value, props.settings, props.editor,
      // onText — 追加到最新的 assistant 消息
      (chunk) => {
        const last = messages.value[messages.value.length - 1]
        if (last?.role === 'assistant') last.content += chunk
        scrollToBottom()
      },
      // onNewSegment — 新的 AI 回复段
      () => {
        messages.value.push({ role: 'assistant', content: '' })
      },
      // onToolCall — 显示工具调用
      (name) => {
        // 如果前一条 assistant 消息是空的（AI 没说话直接调工具），删掉它
        const prev = messages.value[messages.value.length - 1]
        if (prev?.role === 'assistant' && !prev.content) {
          messages.value.pop()
        }
        const toolLabels: Record<string, string> = {
          getNoteContent: '读取笔记内容',
          replaceNoteContent: '替换笔记内容',
          insertAtCursor: '在光标处插入',
          getSelectedText: '获取选中文本',
          setTitle: '修改标题'
        }
        messages.value.push({ role: 'tool', content: toolLabels[name] || name })
        scrollToBottom()
      }
    )
    // 更新历史（从最新的 user+assistant pair 重建）
    const msgs = messages.value
    const userMsg = [...msgs].reverse().find(m => m.role === 'user')?.content || text
    const assistantText = msgs.filter(m => m.role === 'assistant').map(m => m.content).join('\n')
    history.value = [
      ...history.value,
      { role: 'user', content: userMsg },
      { role: 'assistant', content: assistantText }
    ]
  } catch (err: any) {
    messages.value.push({ role: 'assistant', content: `出错了：${err.message}` })
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
  <Transition name="slide">
    <div v-if="visible" class="ai-panel">
      <div class="ai-panel-header">
        <span>AI 助手</span>
        <el-button text :icon="Close" size="small" @click="emit('close')" />
      </div>

      <div ref="msgListRef" class="ai-msg-list" v-if="messages.length > 0">
        <div v-for="(msg, i) in messages" :key="i" class="msg" :class="msg.role === 'tool' ? 'assistant' : msg.role">
          <template v-if="msg.role === 'tool'">
            <div class="msg-label">AI</div>
            <div class="msg-body">
              <span class="msg-tool-icon">🔧</span>
              <span class="msg-tool-text">调用工具：{{ msg.content }}</span>
            </div>
          </template>
          <template v-else>
            <div class="msg-label">{{ msg.role === 'user' ? '你' : 'AI' }}</div>
            <div class="msg-body" v-html="renderMarkdown(msg.content)" />
          </template>
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
  </Transition>
</template>

<style scoped>
.ai-panel {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  flex-shrink: 0;
}
@media (max-width: 720px) {
  .ai-panel { max-height: 40vh; }
}

/* ── Transition 动画 ── */
.slide-enter-active, .slide-leave-active {
  transition: transform 0.25s ease-out;
}
.slide-enter-from, .slide-leave-to {
  transform: translateX(100%);
}
@media (max-width: 720px) {
  .slide-enter-active, .slide-leave-active {
    transition: transform 0.25s ease-out;
  }
  .slide-enter-from, .slide-leave-to {
    transform: translateY(100%);
  }
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

/* ── 工具调用（放在 AI 气泡内） ── */
.msg-tool-icon { font-size: 12px; }
.msg-tool-text { font-size: 12px; color: var(--el-text-color-secondary); }

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
  padding: 24px 0;
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

</style>

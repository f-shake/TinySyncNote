<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import http from '../utils/http'
import type { SharedNoteView as SharedNoteViewType } from '../types'

const route = useRoute()
const note = ref<SharedNoteViewType | null>(null)
const error = ref('')
const loaded = ref(false)

onMounted(async () => {
  const token = route.params.token as string
  try {
    const res = await http.get<SharedNoteViewType>(`/api/share/${token}`)
    note.value = res.data
  } catch {
    error.value = '分享链接无效或已失效'
  } finally {
    loaded.value = true
  }
})
</script>

<template>
  <div class="shared-note-page">
    <div v-if="!loaded" class="shared-loading">加载中...</div>

    <div v-else-if="error" class="shared-error">
      <h1>无法查看</h1>
      <p>{{ error }}</p>
    </div>

    <div v-else-if="note" class="shared-content">
      <header class="shared-header">
        <h1 class="shared-title">{{ note.title }}</h1>
        <div class="shared-meta">
          <span>创建于 {{ new Date(note.createdAt).toLocaleString() }}</span>
          <span>更新于 {{ new Date(note.updatedAt).toLocaleString() }}</span>
        </div>
      </header>
      <article class="shared-body" v-html="note.htmlContent" />
    </div>
  </div>
</template>

<style scoped>
.shared-note-page {
  max-width: 860px;
  margin: 0 auto;
  padding: 40px 24px;
  min-height: 100vh;
  background: #fff;
  color: #333;
}

html.dark .shared-note-page {
  background: #1a1a1a;
  color: #e0e0e0;
}

.shared-loading,
.shared-error {
  text-align: center;
  padding: 80px 20px;
  font-size: 16px;
  color: #999;
}

.shared-error h1 {
  font-size: 24px;
  margin-bottom: 12px;
  color: #e74c3c;
}

.shared-header {
  margin-bottom: 32px;
  padding-bottom: 16px;
  border-bottom: 1px solid #eee;
}

html.dark .shared-header {
  border-bottom-color: #333;
}

.shared-title {
  font-size: 28px;
  margin: 0 0 8px 0;
}

.shared-meta {
  font-size: 13px;
  color: #999;
  display: flex;
  gap: 16px;
}

.shared-body {
  line-height: 1.7;
  font-size: 15px;
}

.shared-body :deep(h1),
.shared-body :deep(h2),
.shared-body :deep(h3),
.shared-body :deep(h4) {
  margin-top: 1.5em;
  margin-bottom: 0.5em;
}

.shared-body :deep(p) {
  margin: 0.8em 0;
}

.shared-body :deep(code) {
  background: #f4f4f4;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 0.9em;
}

.shared-body :deep(pre) {
  background: #f4f4f4;
  padding: 16px;
  border-radius: 6px;
  overflow-x: auto;
}

.shared-body :deep(pre code) {
  background: none;
  padding: 0;
}

html.dark .shared-body :deep(code),
html.dark .shared-body :deep(pre) {
  background: #2a2a2a;
}

.shared-body :deep(blockquote) {
  border-left: 4px solid #ddd;
  margin: 0;
  padding: 0 16px;
  color: #666;
}

html.dark .shared-body :deep(blockquote) {
  border-left-color: #555;
  color: #aaa;
}

.shared-body :deep(table) {
  border-collapse: collapse;
  width: 100%;
}

.shared-body :deep(th),
.shared-body :deep(td) {
  border: 1px solid #ddd;
  padding: 8px 12px;
  text-align: left;
}

html.dark .shared-body :deep(th),
html.dark .shared-body :deep(td) {
  border-color: #444;
}

.shared-body :deep(img) {
  max-width: 100%;
}
</style>

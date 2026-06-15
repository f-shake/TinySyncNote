<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { WarningFilled, ArrowRight } from '@element-plus/icons-vue'
import http from '../utils/http'
import type { ConflictListItem } from '../types'

const router = useRouter()
const conflicts = ref<ConflictListItem[]>([])
const loading = ref(true)

onMounted(async () => {
  try {
    const res = await http.get<ConflictListItem[]>('/api/conflicts')
    conflicts.value = res.data
  } catch {
    ElMessage.error('获取冲突列表失败')
  } finally {
    loading.value = false
  }
})

function openConflict(id: string) {
  router.push(`/conflicts/${id}`)
}
</script>

<template>
  <div class="conflict-list-view">
    <div class="page-header">
      <h2>未解决的冲突</h2>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="loading-state">
      <el-skeleton :rows="3" animated />
    </div>

    <!-- Empty -->
    <div v-else-if="conflicts.length === 0" class="empty-state">
      <el-empty description="暂无冲突" :image-size="160">
        <template #image>
          <el-icon :size="64" color="#c0c4cc"><WarningFilled /></el-icon>
        </template>
      </el-empty>
    </div>

    <!-- List -->
    <div v-else class="conflict-list">
      <div
        v-for="c in conflicts"
        :key="c.id"
        class="conflict-item"
        @click="openConflict(c.id)"
      >
        <div class="conflict-icon">
          <el-icon :size="20" color="#e6a23c"><WarningFilled /></el-icon>
        </div>
        <div class="conflict-body">
          <div class="conflict-title">{{ c.noteTitle }}</div>
          <div class="conflict-time">{{ new Date(c.createdAt).toLocaleString() }} 发生冲突</div>
        </div>
        <el-icon><ArrowRight /></el-icon>
      </div>
    </div>
  </div>
</template>

<style scoped>
.conflict-list-view {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.page-header {
  margin-bottom: 24px;
}

.page-header h2 {
  margin: 0;
  font-size: 22px;
}

.loading-state,
.empty-state {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
}

.conflict-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.conflict-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--el-bg-color);
}

.conflict-item:hover {
  border-color: var(--el-color-warning-light-5);
  box-shadow: 0 2px 8px rgba(230, 162, 60, 0.1);
}

.conflict-body {
  flex: 1;
}

.conflict-title {
  font-weight: 500;
  margin-bottom: 4px;
}

.conflict-time {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>

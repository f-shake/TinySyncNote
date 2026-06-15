<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, WarningFilled } from '@element-plus/icons-vue'
import http from '../utils/http'
import type { ConflictResponse } from '../types'

const route = useRoute()
const router = useRouter()

const conflict = ref<ConflictResponse | null>(null)
const loading = ref(true)
const resolving = ref(false)
const mergedContent = ref('')

onMounted(async () => {
  const id = route.params.id as string
  try {
    const res = await http.get<ConflictResponse>(`/api/conflicts/${id}`)
    conflict.value = res.data
    mergedContent.value = res.data.localContent
  } catch {
    ElMessage.error('加载冲突信息失败')
    router.push('/')
  } finally {
    loading.value = false
  }
})

async function resolve(strategy: 'KeepLocal' | 'KeepRemote' | 'Merged') {
  if (!conflict.value) return

  if (strategy === 'Merged' && !mergedContent.value.trim()) {
    ElMessage.warning('合并内容不能为空')
    return
  }

  // 合并模式确认
  if (strategy === 'Merged') {
    try {
      await ElMessageBox.confirm(
        '确认手动合并？合并后本地和服务端内容将被替换为合并内容。',
        '合并确认',
        { confirmButtonText: '确认', cancelButtonText: '取消', type: 'info' }
      )
    } catch {
      return // 用户取消
    }
  }

  resolving.value = true
  try {
    await http.post(`/api/conflicts/${conflict.value.id}/resolve`, {
      strategy,
      mergedContent: strategy === 'Merged' ? mergedContent.value : null
    })
    ElMessage.success('冲突已解决')
    router.push('/conflicts')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '解决冲突失败')
  } finally {
    resolving.value = false
  }
}
</script>

<template>
  <div class="conflict-resolver">
    <!-- Header -->
    <div class="resolver-header">
      <el-button text :icon="ArrowLeft" @click="router.push('/conflicts')">
        返回冲突列表
      </el-button>
      <h2>解决冲突</h2>
      <div v-if="conflict" class="conflict-info">
        <el-tag type="warning" effect="plain">笔记：{{ conflict.noteTitle }}</el-tag>
        <el-tag>本地 v{{ conflict.localVersion }}</el-tag>
        <el-tag>服务端 v{{ conflict.remoteVersion }}</el-tag>
      </div>
    </div>

    <!-- Conflict content -->
    <div v-if="conflict" class="resolver-body">
      <div class="diff-panels">
        <!-- 本地版本 -->
        <div class="diff-panel local">
          <div class="diff-header">
            <el-icon color="#409eff"><WarningFilled /></el-icon>
            <span>我的版本</span>
            <el-tag size="small">v{{ conflict.localVersion }}</el-tag>
          </div>
          <div class="diff-content">
            <div class="diff-title">{{ conflict.noteTitle }}</div>
            <div class="diff-text">{{ conflict.localContent || '（空）' }}</div>
          </div>
          <el-button
            type="primary"
            :loading="resolving"
            @click="resolve('KeepLocal')"
            class="resolve-btn"
          >
            保留我的版本
          </el-button>
        </div>

        <!-- 服务端版本 -->
        <div class="diff-panel remote">
          <div class="diff-header">
            <el-icon color="#e6a23c"><WarningFilled /></el-icon>
            <span>服务端版本</span>
            <el-tag size="small">v{{ conflict.remoteVersion }}</el-tag>
          </div>
          <div class="diff-content">
            <div class="diff-title">{{ conflict.noteTitle }}</div>
            <div class="diff-text">{{ conflict.remoteContent || '（空）' }}</div>
          </div>
          <el-button
            type="warning"
            :loading="resolving"
            @click="resolve('KeepRemote')"
            class="resolve-btn"
          >
            采用服务端版本
          </el-button>
        </div>
      </div>

      <!-- 手动合并 -->
      <div class="merge-section">
        <div class="merge-header">
          <h3>手动合并</h3>
          <el-button
            type="success"
            :loading="resolving"
            @click="resolve('Merged')"
          >
            确认合并
          </el-button>
        </div>
        <el-input
          v-model="mergedContent"
          type="textarea"
          :rows="10"
          placeholder="在此编辑合并后的内容..."
        />
      </div>
    </div>

    <!-- Not found -->
    <div v-else class="not-found">
      <el-empty description="冲突记录不存在或已被解决" />
    </div>
  </div>
</template>

<style scoped>
.conflict-resolver {
  padding: 24px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.resolver-header {
  margin-bottom: 24px;
}

.resolver-header h2 {
  margin: 8px 0 12px 0;
  font-size: 22px;
}

.conflict-info {
  display: flex;
  gap: 8px;
}


.resolver-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 24px;
  overflow-y: auto;
}

.diff-panels {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.diff-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.diff-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  font-weight: 600;
  border-bottom: 1px solid var(--el-border-color-light);
}

.diff-content {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
  max-height: 300px;
}

.diff-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 12px;
  color: var(--el-text-color-primary);
}

.diff-text {
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  color: var(--el-text-color-regular);
}

.resolve-btn {
  margin: 12px 16px;
}

.merge-section {
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 16px;
}

.merge-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.merge-header h3 {
  margin: 0;
  font-size: 16px;
}

.not-found {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>

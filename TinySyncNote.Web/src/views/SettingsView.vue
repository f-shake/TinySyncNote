<script setup lang="ts">
import { ref } from 'vue'
import { Moon, Sunny } from '@element-plus/icons-vue'
const AUTO_SAVE_KEY = 'tsn_autosave_interval'

const isDark = ref(document.documentElement.classList.contains('dark'))

function toggleDark() {
  isDark.value = !isDark.value
  if (isDark.value) {
    document.documentElement.classList.add('dark')
    localStorage.setItem('theme', 'dark')
  } else {
    document.documentElement.classList.remove('dark')
    localStorage.setItem('theme', 'light')
  }
}

// 自动保存间隔（秒）
const autoSaveSeconds = ref(loadAutoSaveInterval())

function loadAutoSaveInterval(): number {
  const saved = localStorage.getItem(AUTO_SAVE_KEY)
  if (saved) {
    const n = parseInt(saved, 10)
    if (n >= 2 && n <= 300) return n
  }
  return 5
}

function onAutoSaveChange(val: number) {
  localStorage.setItem(AUTO_SAVE_KEY, String(val))
}
</script>

<template>
  <div class="settings-view">
    <div class="page-header">
      <h2>设置</h2>
    </div>

    <div class="settings-section">
      <h3 class="section-title">显示</h3>
      <div class="setting-item">
        <span class="setting-label">深色模式</span>
        <el-switch
          :model-value="isDark"
          @change="toggleDark"
          :active-icon="Moon"
          :inactive-icon="Sunny"
        />
      </div>
    </div>

    <div class="settings-section" style="margin-top: 16px;">
      <h3 class="section-title">编辑</h3>
      <div class="setting-item">
        <span class="setting-label">自动保存间隔（秒）</span>
        <el-input-number
          v-model="autoSaveSeconds"
          @change="onAutoSaveChange"
          :min="2"
          :max="300"
          size="small"
          style="width: 120px"
        />
      </div>
      <div class="setting-desc">输入时自动保存，点击"未保存"标签可立即保存</div>
    </div>
  </div>
</template>

<style scoped>
.settings-view {
  padding: 24px;
  max-width: 600px;
}

.page-header {
  margin-bottom: 32px;
}

.page-header h2 {
  margin: 0;
  font-size: 22px;
}

.settings-section {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 20px;
}

.section-title {
  font-size: 14px;
  color: var(--el-text-color-secondary);
  margin: 0 0 16px 0;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.setting-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 0;
}

.setting-label {
  font-size: 15px;
  color: var(--el-text-color-primary);
}

.setting-desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 4px;
}
</style>

<script setup lang="ts">
import { ref } from 'vue'
import { Moon, Sunny } from '@element-plus/icons-vue'

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
</style>

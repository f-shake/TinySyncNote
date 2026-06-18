<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from './stores/auth'

const authStore = useAuthStore()

onMounted(async () => {
  // 暗色模式：优先使用用户手动设置，否则跟随系统
  const theme = localStorage.getItem('theme')
  if (theme === 'dark' || (!theme && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
    document.documentElement.classList.add('dark')
  }
  await authStore.init()
})
</script>

<template>
  <router-view v-if="authStore.isLoaded" />
  <div v-else class="app-loading">
    <el-icon class="is-loading" :size="32" color="#409eff">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="12" cy="12" r="10" />
        <path d="M12 6v6l4 2" />
      </svg>
    </el-icon>
    <p>加载中...</p>
  </div>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #app {
  height: 100%;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}

.app-loading {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100vh;
  gap: 16px;
  color: #909399;
}

/* 浅色模式：提高对比度，加深边框和文字 */
:root {
  --el-border-color: #c0c4cc;
  --el-border-color-light: #d0d5dd;
  --el-border-color-lighter: #d8dce3;
  --el-border-color-extra-light: #e4e8ee;
  --el-text-color-primary: #1f1f1f;
  --el-text-color-regular: #3f4042;
  --el-text-color-secondary: #6b6f76;
  --el-text-color-placeholder: #8f9399;
  --el-text-color-disabled: #afb3b9;
}
</style>

<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
import { useRouter } from 'vue-router'
import { Notebook, Setting, SwitchButton, WarningFilled } from '@element-plus/icons-vue'

const authStore = useAuthStore()
const router = useRouter()

function handleLogout() {
  authStore.logout()
}
</script>

<template>
  <div class="main-layout">
    <!-- 侧边栏 -->
    <el-menu
      :default-active="router.currentRoute.value.path"
      router
      class="sidebar-menu"
      :collapse="false"
    >
      <div class="sidebar-header">
        <h2 class="sidebar-title">TinySyncNote</h2>
      </div>

      <el-menu-item index="/notebooks">
        <el-icon><Notebook /></el-icon>
        <span>笔记本</span>
      </el-menu-item>

      <el-menu-item index="/conflicts">
        <el-icon><WarningFilled /></el-icon>
        <span>冲突列表</span>
      </el-menu-item>

      <el-menu-item index="/settings">
        <el-icon><Setting /></el-icon>
        <span>设置</span>
      </el-menu-item>

      <div class="sidebar-spacer" />

      <div class="sidebar-user">
        <span class="user-name">{{ authStore.user?.username }}</span>
        <el-button
          text
          :icon="SwitchButton"
          @click="handleLogout"
          class="logout-btn"
        >
          退出
        </el-button>
      </div>
    </el-menu>

    <!-- 主内容 -->
    <div class="main-content">
      <router-view />
    </div>
  </div>
</template>

<style scoped>
.main-layout {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.sidebar-menu {
  width: 240px;
  height: 100vh;
  border-right: 1px solid var(--el-border-color-light);
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.sidebar-header {
  padding: 20px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}

.sidebar-title {
  font-size: 18px;
  margin: 0;
  color: var(--el-color-primary);
}

.sidebar-spacer {
  flex: 1;
}

.sidebar-user {
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-light);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.user-name {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.logout-btn {
  flex-shrink: 0;
}

.main-content {
  flex: 1;
  overflow-y: auto;
  background: var(--el-bg-color-page);
}
</style>

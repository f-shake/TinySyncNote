<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useRoute } from 'vue-router'
import {
  Notebook, WarningFilled, Setting,
  SwitchButton, Moon, Sunny
} from '@element-plus/icons-vue'

const authStore = useAuthStore()
const route = useRoute()

// ── 暗色模式 ──
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

// ── 导航高亮 ──
const activeMenu = computed(() => {
  const path = route.path
  if (path.startsWith('/notebook') || path.startsWith('/note')) return '/notebooks'
  return path
})

function handleLogout() {
  authStore.logout()
}
</script>

<template>
  <div class="main-layout">
    <!-- ═══ 顶栏 ═══ -->
    <header class="top-bar">
      <div class="top-bar-left">
        <h2 class="app-title">TinySyncNote</h2>

        <el-menu
          :default-active="activeMenu"
          mode="horizontal"
          router
          class="top-nav-menu"
        >
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
        </el-menu>
      </div>

      <div class="top-bar-right">
        <el-tooltip :content="isDark ? '切换浅色模式' : '切换深色模式'" placement="bottom">
          <el-button
            text
            :icon="isDark ? Sunny : Moon"
            @click="toggleDark"
            class="theme-btn"
          />
        </el-tooltip>

        <span class="user-name">{{ authStore.user?.username }}</span>

        <el-button text :icon="SwitchButton" @click="handleLogout">
          <span class="logout-text">退出</span>
        </el-button>
      </div>
    </header>

    <!-- ═══ 主内容 ═══ -->
    <main class="main-content">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.main-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

/* ── 顶栏 ── */
.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  padding: 0 20px;
  background: var(--el-bg-color);
  flex-shrink: 0;
  z-index: 100;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 24px;
  min-width: 0;
  overflow: hidden;
}

.app-title {
  font-size: 18px;
  color: var(--el-color-primary);
  margin: 0;
  white-space: nowrap;
  flex-shrink: 0;
}

/* 水平导航菜单 — 去掉 el-menu 自带的底部边框 */
.top-nav-menu {
  border-bottom: none !important;
  flex-shrink: 0;
}

.top-nav-menu .el-menu-item {
  height: 56px;
  line-height: 56px;
  padding: 0 16px;
}

.top-bar-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

.user-name {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}

/* ── 主内容区 ── */
.main-content {
  flex: 1;
  overflow-y: auto;
  background: var(--el-bg-color-page);
}

/* ════════════════════════════════════════
   响应式 — 平板 / 手机
   ════════════════════════════════════════ */
@media (max-width: 768px) {
  .top-bar {
    height: 48px;
    padding: 0 8px;
  }

  .app-title {
    font-size: 15px;
  }

  .top-bar-left {
    gap: 8px;
  }

  .top-nav-menu .el-menu-item {
    height: 48px;
    line-height: 48px;
    padding: 0 8px;
    font-size: 12px;
  }

  .top-nav-menu .el-menu-item span {
    font-size: 12px;
  }

  .user-name {
    display: none;
  }
}

@media (max-width: 480px) {
  .top-bar {
    padding: 0 4px;
  }

  .app-title {
    display: none;
  }

  .top-nav-menu .el-menu-item {
    padding: 0 6px;
  }

  .top-nav-menu .el-menu-item span {
    font-size: 11px;
  }
}
</style>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { ElMessage } from 'element-plus'
import { User, Lock } from '@element-plus/icons-vue'
import http from '../utils/http'

const authStore = useAuthStore()

const registrationEnabled = ref(false)
const registrationLoaded = ref(false)

onMounted(async () => {
  try {
    const res = await http.get('/api/auth/registration-status')
    registrationEnabled.value = res.data.enabled
  } catch {
    registrationEnabled.value = true
  } finally {
    registrationLoaded.value = true
  }
})

const form = reactive({
  username: '',
  password: ''
})

async function handleLogin() {
  if (!form.username || !form.password) {
    ElMessage.warning('请填写用户名和密码')
    return
  }
  try {
    await authStore.login(form)
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '登录失败')
  }
}
</script>

<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1 class="app-title">TinySyncNote</h1>
        <p class="app-subtitle">轻量级 Markdown 笔记</p>
      </div>

      <el-form
        :model="form"
        @keyup.enter="handleLogin"
        label-position="top"
        size="large"
      >
        <el-form-item label="用户名">
          <el-input
            v-model="form.username"
            placeholder="请输入用户名"
            :prefix-icon="User"
          />
        </el-form-item>

        <el-form-item label="密码">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="请输入密码"
            show-password
            :prefix-icon="Lock"
          />
        </el-form-item>

        <el-form-item>
          <el-button
            type="primary"
            @click="handleLogin"
            :loading="authStore.loading"
            class="submit-btn"
          >
            登 录
          </el-button>
        </el-form-item>
      </el-form>

      <div class="login-footer" v-if="registrationLoaded">
        <template v-if="registrationEnabled">
          还没有账号？
          <router-link to="/register">立即注册</router-link>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-card {
  width: 400px;
  padding: 40px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.app-title {
  font-size: 28px;
  color: #303133;
  margin: 0 0 8px 0;
}

.app-subtitle {
  font-size: 14px;
  color: #909399;
  margin: 0;
}

.submit-btn {
  width: 100%;
}

.login-footer {
  text-align: center;
  font-size: 14px;
  color: #909399;
  margin-top: 16px;
}

.login-footer a {
  color: #409eff;
  text-decoration: none;
}

.login-footer a:hover {
  text-decoration: underline;
}

/* ── 暗色模式 ── */
html.dark .login-container {
  background: linear-gradient(135deg, #2c3e6b 0%, #3d2a5c 100%);
}

html.dark .login-card {
  background: #1e1e1e;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
}

html.dark .app-title {
  color: #e0e0e0;
}

html.dark .app-subtitle {
  color: #999;
}

html.dark .login-footer {
  color: #999;
}

html.dark .login-footer a {
  color: #5ea6f0;
}

/* ── 移动端：取消卡片样式 ── */
@media (max-width: 768px) {
  .login-container {
    align-items: flex-start;
    padding-top: 60px;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  }

  .login-card {
    width: 100%;
    padding: 24px 20px;
    background: transparent;
    box-shadow: none;
    border-radius: 0;
  }

  html.dark .login-card {
    background: transparent;
    box-shadow: none;
  }

  .app-title { color: #fff; }
  .app-subtitle { color: rgba(255,255,255,0.8); }
  .login-footer { color: rgba(255,255,255,0.7); }
  .login-footer a { color: #8ab4ff; }

  html.dark .app-title { color: #fff; }
  html.dark .app-subtitle { color: rgba(255,255,255,0.8); }
  html.dark .login-footer { color: rgba(255,255,255,0.7); }
  html.dark .login-footer a { color: #8ab4ff; }
}
</style>

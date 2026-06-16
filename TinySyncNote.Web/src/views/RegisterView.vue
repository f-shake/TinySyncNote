<script setup lang="ts">
import { reactive } from 'vue'
import { useAuthStore } from '../stores/auth'
import { ElMessage } from 'element-plus'
import { User, Lock } from '@element-plus/icons-vue'

const authStore = useAuthStore()

const form = reactive({
  username: '',
  password: '',
  confirmPassword: ''
})

async function handleRegister() {
  if (!form.username || !form.password) {
    ElMessage.warning('请填写所有必填字段')
    return
  }
  if (form.password !== form.confirmPassword) {
    ElMessage.warning('两次密码输入不一致')
    return
  }
  if (form.password.length < 6) {
    ElMessage.warning('密码长度不能少于 6 位')
    return
  }
  try {
    await authStore.register({
      username: form.username,
      password: form.password
    })
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '注册失败')
  }
}
</script>

<template>
  <div class="register-container">
    <div class="register-card">
      <div class="register-header">
        <h1 class="app-title">注册账号</h1>
        <p class="app-subtitle">创建你的 TinySyncNote 账号</p>
      </div>

      <el-form
        :model="form"
        @keyup.enter="handleRegister"
        label-position="top"
        size="large"
      >
        <el-form-item label="用户名" required>
          <el-input
            v-model="form.username"
            placeholder="3-50 个字符"
            :prefix-icon="User"
          />
        </el-form-item>

        <el-form-item label="密码" required>
          <el-input
            v-model="form.password"
            type="password"
            placeholder="至少 6 位密码"
            show-password
            :prefix-icon="Lock"
          />
        </el-form-item>

        <el-form-item label="确认密码" required>
          <el-input
            v-model="form.confirmPassword"
            type="password"
            placeholder="再次输入密码"
            show-password
            :prefix-icon="Lock"
          />
        </el-form-item>

        <el-form-item>
          <el-button
            type="primary"
            @click="handleRegister"
            :loading="authStore.loading"
            class="submit-btn"
          >
            注 册
          </el-button>
        </el-form-item>
      </el-form>

      <div class="register-footer">
        已有账号？
        <router-link to="/login">立即登录</router-link>
      </div>
    </div>
  </div>
</template>

<style scoped>
.register-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.register-card {
  width: 420px;
  padding: 40px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.register-header {
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

.register-footer {
  text-align: center;
  font-size: 14px;
  color: #909399;
  margin-top: 16px;
}

.register-footer a {
  color: #409eff;
  text-decoration: none;
}

.register-footer a:hover {
  text-decoration: underline;
}

/* ── 暗色模式 ── */
html.dark .register-container {
  background: linear-gradient(135deg, #2c3e6b 0%, #3d2a5c 100%);
}

html.dark .register-card {
  background: #1e1e1e;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
}

html.dark .app-title {
  color: #e0e0e0;
}

html.dark .app-subtitle {
  color: #999;
}

html.dark .register-footer {
  color: #999;
}

html.dark .register-footer a {
  color: #5ea6f0;
}

/* ── 移动端：取消卡片样式 ── */
@media (max-width: 768px) {
  .register-container {
    align-items: flex-start;
    padding-top: 60px;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  }

  .register-card {
    width: 100%;
    padding: 24px 20px;
    background: transparent;
    box-shadow: none;
    border-radius: 0;
  }

  html.dark .register-card {
    background: transparent;
    box-shadow: none;
  }

  .app-title { color: #fff; }
  .app-subtitle { color: rgba(255,255,255,0.8); }
  .register-footer { color: rgba(255,255,255,0.7); }
  .register-footer a { color: #8ab4ff; }

  html.dark .app-title { color: #fff; }
  html.dark .app-subtitle { color: rgba(255,255,255,0.8); }
  html.dark .register-footer { color: rgba(255,255,255,0.7); }
  html.dark .register-footer a { color: #8ab4ff; }
}
</style>

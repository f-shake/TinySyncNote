import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import http, { setTokens, clearTokens, getAccessToken } from '../utils/http'
import type { UserInfo, AuthResponse, LoginRequest, RegisterRequest } from '../types'
import router from '../router'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserInfo | null>(null)
  const loading = ref(false)

  const isLoggedIn = computed(() => !!user.value)
  const isLoaded = ref(false)

  // 从 localStorage 恢复会话
  async function init() {
    const token = getAccessToken()
    if (token) {
      try {
        const res = await http.get('/api/auth/profile')
        user.value = res.data
      } catch {
        clearTokens()
      }
    }
    isLoaded.value = true
  }

  async function login(request: LoginRequest) {
    loading.value = true
    try {
      const res = await http.post<AuthResponse>('/api/auth/login', request)
      const data = res.data
      setTokens(data.accessToken, data.refreshToken)
      user.value = data.user
      router.push('/')
    } finally {
      loading.value = false
    }
  }

  async function register(request: RegisterRequest) {
    loading.value = true
    try {
      const res = await http.post<AuthResponse>('/api/auth/register', request)
      const data = res.data
      setTokens(data.accessToken, data.refreshToken)
      user.value = data.user
      router.push('/')
    } finally {
      loading.value = false
    }
  }

  function logout() {
    clearTokens()
    user.value = null
    router.push('/login')
  }

  return { user, loading, isLoggedIn, isLoaded, init, login, register, logout }
})

import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import router from '../router'

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  timeout: 30000,
  // 不设 Content-Type 默认值，让 axios 根据请求数据自动判断：
  //   - JSON 请求：自动使用 application/json
  //   - FormData 请求：浏览器自动设置 multipart/form-data; boundary=...
})

// 存储 Token 的 key
const ACCESS_TOKEN_KEY = 'tsn_access_token'
const REFRESH_TOKEN_KEY = 'tsn_refresh_token'

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY)
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function setTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
}

export function clearTokens() {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
}

// 是否正在刷新 Token
let isRefreshing = false
let refreshSubscribers: Array<(token: string) => void> = []

function onRefreshed(token: string) {
  refreshSubscribers.forEach(cb => cb(token))
  refreshSubscribers = []
}

// 请求拦截器：自动附加 JWT
http.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken()
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// 响应拦截器：处理 401 自动刷新 Token
http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean }

    // 不是 401 或已经是重试请求 → 直接拒绝
    if (error.response?.status !== 401 || originalRequest._retry) {
      const message = (error.response?.data as any)?.message || error.message || '请求失败'
      ElMessage.error(message)
      return Promise.reject(error)
    }

    // 登录/注册接口的 401 不需要刷新 Token
    const authPath = originalRequest.url?.replace(http.defaults.baseURL || '', '')
    if (authPath === '/api/auth/login' || authPath === '/api/auth/register') {
      return Promise.reject(error)
    }

    // 正在刷新 → 排队等待
    if (isRefreshing) {
      return new Promise(resolve => {
        refreshSubscribers.push((token: string) => {
          originalRequest.headers.Authorization = `Bearer ${token}`
          resolve(http(originalRequest))
        })
      })
    }

    // 开始刷新 Token
    isRefreshing = true
    originalRequest._retry = true

    try {
      const refreshToken = getRefreshToken()
      if (!refreshToken) throw new Error('No refresh token')

      const res = await axios.post(
        `${http.defaults.baseURL}/api/auth/refresh`,
        { refreshToken }
      )

      const { accessToken, refreshToken: newRefreshToken } = res.data
      setTokens(accessToken, newRefreshToken)

      onRefreshed(accessToken)

      originalRequest.headers.Authorization = `Bearer ${accessToken}`
      return http(originalRequest)
    } catch {
      clearTokens()
      refreshSubscribers = []
      ElMessage.error('登录已过期，请重新登录')
      router.push('/login')
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  }
)

export default http

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  base: process.env.VITE_BASE || '/',
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5062',
        changeOrigin: true
      },
      '/uploads': {
        target: 'http://localhost:5062',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5062',
        changeOrigin: true,
        ws: true
      }
    }
  }
})

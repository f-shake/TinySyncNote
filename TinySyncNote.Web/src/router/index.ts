import { createRouter, createWebHistory } from 'vue-router'
import { getAccessToken } from '../utils/http'

const router = createRouter({
  history: createWebHistory('/note/'),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('../views/LoginView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/register',
      name: 'Register',
      component: () => import('../views/RegisterView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/',
      name: 'Main',
      component: () => import('../views/MainLayout.vue'),
      meta: { requiresAuth: true },
      // 注意：不能在父路由上设置 redirect，否则 Vue Router 4 在初始导航时
      // 会对所有子路由也触发重定向（导致刷新的笔记页面被重定向到 /notebooks）
      children: [
        {
          path: 'notebooks',
          name: 'Notebooks',
          component: () => import('../views/NotebookListView.vue')
        },
        {
          path: 'notebook/:id',
          name: 'NotebookDetail',
          component: () => import('../views/NotebookDetailView.vue')
        },
        {
          path: 'note/:id',
          name: 'NoteEditor',
          component: () => import('../views/NoteEditorView.vue')
        },
        {
          path: 'conflicts',
          name: 'Conflicts',
          component: () => import('../views/ConflictListView.vue')
        },
        {
          path: 'conflicts/:id',
          name: 'ConflictResolver',
          component: () => import('../views/ConflictResolverView.vue')
        },
        {
          path: 'settings',
          name: 'Settings',
          component: () => import('../views/SettingsView.vue')
        }
      ]
    },
    {
      path: '/share/:token',
      name: 'SharedNote',
      component: () => import('../views/SharedNoteView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

// 导航守卫：未登录跳转到登录页；根路径跳转到笔记本列表
router.beforeEach((to, _from, next) => {
  const requiresAuth = to.meta.requiresAuth !== false
  const isAuthenticated = !!getAccessToken()

  if (requiresAuth && !isAuthenticated) {
    next('/login')
  } else if (!requiresAuth && isAuthenticated && (to.path === '/login' || to.path === '/register')) {
    next('/')
  } else if (to.path === '/') {
    // 根路径 → 笔记本列表（不用 redirect 是为了避免 Vue Router 4
    // 初始导航时父路由的 redirect 截获子路由）
    next('/notebooks')
  } else {
    next()
  }
})

export default router

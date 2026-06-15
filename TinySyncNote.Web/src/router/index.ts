import { createRouter, createWebHistory } from 'vue-router'
import { getAccessToken } from '../utils/http'

const router = createRouter({
  history: createWebHistory(),
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
      redirect: '/notebooks',
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
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

// 导航守卫：未登录跳转到登录页
router.beforeEach((to, _from, next) => {
  const requiresAuth = to.meta.requiresAuth !== false
  const isAuthenticated = !!getAccessToken()

  if (requiresAuth && !isAuthenticated) {
    next('/login')
  } else if (!requiresAuth && isAuthenticated && (to.path === '/login' || to.path === '/register')) {
    next('/')
  } else {
    next()
  }
})

export default router

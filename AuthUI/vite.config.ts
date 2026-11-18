import {defineConfig} from 'vite'
import {svelte} from '@sveltejs/vite-plugin-svelte'

// The app is served by the ASP.NET host under /auth/ in production.
// During development it runs on its own port and proxies API calls to the backend.
export default defineConfig({
  base: '/auth/',
  plugins: [svelte()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})

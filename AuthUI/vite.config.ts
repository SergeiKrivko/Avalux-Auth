import {defineConfig} from 'vite'
import {svelte} from '@sveltejs/vite-plugin-svelte'

// The forms are served by the ASP.NET host at /login and /profile and are
// built into the wwwroot root. Assets go to /static so they do not clash with
// the Angular admin panel, which owns /assets on the same host.
// During development it runs on its own port and proxies API calls to the backend.
export default defineConfig({
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
    assetsDir: 'static',
  },
})

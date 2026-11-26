import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development the Vite server proxies /api and /health to the .NET API,
// so the browser talks to a single origin and no CORS setup is needed.
const apiTarget = process.env.API_URL ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
});

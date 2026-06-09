import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget = 'http://localhost:5000';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': apiTarget,
      '/documents': apiTarget,
      '/images': apiTarget,
      '/templates': apiTarget,
    },
  },
});

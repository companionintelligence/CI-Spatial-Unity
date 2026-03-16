/// <reference types="node" />

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { defineConfig } from 'vite';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '..');
const defaultCertPath = path.join(
  repoRoot,
  'cv-server',
  'certs',
  'liam-razer.capybara-ulmer.ts.net.crt'
);
const defaultKeyPath = path.join(
  repoRoot,
  'cv-server',
  'certs',
  'liam-razer.capybara-ulmer.ts.net.key'
);

function resolveHttpsConfig() {
  const certPath = process.env.VITE_SSL_CERT_PATH ?? defaultCertPath;
  const keyPath = process.env.VITE_SSL_KEY_PATH ?? defaultKeyPath;

  if (!fs.existsSync(certPath) || !fs.existsSync(keyPath)) {
    return undefined;
  }

  return {
    cert: fs.readFileSync(certPath),
    key: fs.readFileSync(keyPath),
  };
}

const https = resolveHttpsConfig();

export default defineConfig({
  base: './',
  build: {
    target: 'es2022',
    outDir: 'dist',
    sourcemap: true,
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    https,
    // Proxy API calls to cv-server during development
    proxy: {
      '/v1': {
        target: 'http://localhost:8000',
        changeOrigin: true,
      },
    },
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    strictPort: true,
    https,
  },
  optimizeDeps: {
    include: ['three', '@iwsdk/core'],
  },
});

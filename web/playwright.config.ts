import { defineConfig, devices } from '@playwright/test';

const webPort = 4173;
const webServerCommand = process.env.CI
  ? `npm run preview -- --host 127.0.0.1 --port ${webPort} --strictPort`
  : `npm run dev -- --host 127.0.0.1 --port ${webPort} --strictPort`;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  use: {
    baseURL: `http://127.0.0.1:${webPort}`,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    permissions: ['camera'],
    launchOptions: {
      args: [
        '--use-angle=swiftshader',
        '--use-fake-device-for-media-stream',
        '--use-fake-ui-for-media-stream',
        '--enable-features=WebXR,OpenXR,WebXRHandInput',
      ],
    },
  },
  webServer: {
    command: webServerCommand,
    port: webPort,
    reuseExistingServer: !process.env.CI,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
      },
    },
  ],
});

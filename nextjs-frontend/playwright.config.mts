// E2E Testing Configuration
// Run with: npx playwright test

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  // No shared-login "setup" project: every current spec either logs in itself
  // (dashboard.spec.ts's beforeEach) or doesn't need auth at all (api.spec.ts,
  // auth.spec.ts tests the login form directly). A prior "setup" project here
  // declared `dependencies: ['setup']` and a storageState file that no
  // *.setup.ts ever produced, which failed every single test run
  // (ENOENT: playwright/.auth/user.json) rather than actually sharing a login.
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
    },
  ],

  // Web server configuration for CI
  webServer: process.env.CI
    ? {
        command: 'npm run build && npm run start',
        url: 'http://localhost:3000',
        reuseExistingServer: !process.env.CI,
        timeout: 120 * 1000,
      }
    : undefined,
});

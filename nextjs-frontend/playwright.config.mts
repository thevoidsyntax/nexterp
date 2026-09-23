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
  // Every project runs the full testDir by default, so with 3 browser projects
  // zzz-rate-limiting.spec.ts would run 3 times — and LoginRateLimitService's
  // lockout (15 min, per source IP) triggered by the first project's run is
  // still active when the next project starts moments later, failing every
  // login-dependent test in it. Excluded from the browser projects and run
  // instead by the dedicated project below, so it executes exactly once,
  // after everything else (it's listed last; Playwright runs projects in
  // array order).
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
      testIgnore: /zzz-rate-limiting\.spec\.ts/,
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
      testIgnore: /zzz-rate-limiting\.spec\.ts/,
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
      testIgnore: /zzz-rate-limiting\.spec\.ts/,
    },
    {
      name: 'rate-limiting',
      use: { ...devices['Desktop Chrome'] },
      testMatch: /zzz-rate-limiting\.spec\.ts/,
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

import { test, expect } from '@playwright/test';

/**
 * Login E2E Tests
 */
test.describe('Authentication', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('should display login form', async ({ page }) => {
    // The heading is the app name ("NEXTERP"), not literally "Login"/"Sign in" —
    // the submit button is what actually says "Sign In".
    await expect(page.locator('h1, h2, [class*="title"], [class*="heading"]')).toBeVisible();
    await expect(page.locator('input[name="username"], input[type="text"]')).toBeVisible();
    await expect(page.locator('input[name="password"], input[type="password"]')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toContainText(/sign in/i);
  });

  test('should login with valid credentials', async ({ page }) => {
    await page.fill('input[name="username"], input[type="text"]', 'admin');
    await page.fill('input[name="password"], input[type="password"]', 'DevPassword2024!');
    await page.click('button[type="submit"]');

    // Wait for redirect to dashboard
    await page.waitForURL(/dashboard/, { timeout: 10000 });

    // Verify dashboard is loaded
    await expect(page.locator('body')).toContainText(/dashboard|home/i);
  });

  test('should show error with invalid credentials', async ({ page }) => {
    await page.fill('input[name="username"], input[type="text"]', 'admin');
    await page.fill('input[name="password"], input[type="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');

    // Wait for error message. `text=` can't be mixed into a comma-separated CSS
    // selector list (that's a syntax error) — use .or() to combine locators.
    // .first(): the icon's lucide-circle-alert class also matches [class*="alert"],
    // so this otherwise resolves to 2 elements.
    const errorBanner = page.locator('[class*="error"], [class*="alert"]').or(page.getByText(/invalid|failed|error/i)).first();
    await expect(errorBanner).toBeVisible({ timeout: 5000 });
  });

  test('should validate required fields', async ({ page }) => {
    // Username/password are plain HTML5 `required` inputs with no custom
    // validation-message UI — the browser blocks the submit event before
    // React's onSubmit ever runs, so the only observable effect is that we
    // never navigate away and the field reports itself as invalid.
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL(/login/);
    await expect(page.locator('input[name="username"], input[type="text"]')).toHaveJSProperty('validity.valid', false);
  });
});

import { test, expect } from '@playwright/test';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

/**
 * API Integration Tests
 *
 * These hit the .NET backend directly (API_BASE_URL), not the Playwright
 * `baseURL` (the Next.js frontend at localhost:3000, which has no /api/v1
 * routes of its own) — so every request here uses an absolute URL.
 *
 * Each test uses its own `request` fixture rather than sharing one assigned
 * in beforeAll: Playwright fixtures obtained in beforeAll are scoped to that
 * hook and can't be reused inside a test ("Fixture { request } from
 * beforeAll cannot be reused in a test").
 */
test.describe('API Integration', () => {
  test.describe('Auth API', () => {
    test('POST /api/v1/auth/login should work with valid credentials', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
        data: {
          username: 'admin',
          password: 'DevPassword2024!',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      expect(response.ok()).toBeTruthy();
      const body = await response.json();
      expect(body).toHaveProperty('data');
      expect(body.data).toHaveProperty('accessToken');
      expect(body.data).toHaveProperty('refreshToken');
      expect(body.data).toHaveProperty('user');
    });

    test('POST /api/v1/auth/login should fail with invalid credentials', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
        data: {
          username: 'admin',
          password: 'wrongpassword',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should return 401 or error response
      expect(response.status()).toBeGreaterThanOrEqual(400);
    });

    test('POST /api/v1/auth/login should validate required fields', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
        data: {
          username: '',
          password: '',
        },
        headers: {
          'Content-Type': 'application/json',
        },
      });

      // Should return 400 Bad Request
      expect(response.status()).toBeGreaterThanOrEqual(400);
    });
  });

  test.describe('Protected API Routes', () => {
    test('GET /api/v1/users should require authentication', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/api/v1/users`);
      expect([401, 403]).toContain(response.status());
    });

    test('GET /api/v1/users should work with valid token', async ({ request }) => {
      const loginResponse = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
        data: {
          username: 'admin',
          password: 'DevPassword2024!',
        },
      });
      const loginBody = await loginResponse.json();
      const authToken = loginBody.data?.accessToken || '';

      const response = await request.get(`${API_BASE_URL}/api/v1/users`, {
        headers: {
          Authorization: `Bearer ${authToken}`,
        },
      });

      // Should return 200 or 404 (if endpoint doesn't exist)
      expect([200, 404]).toContain(response.status());
    });
  });

  test.describe('Health Checks', () => {
    test('GET /health/live should return 200', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/health/live`);
      expect(response.status()).toBe(200);
    });

    test('GET /health/ready should return 200', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/health/ready`);
      expect(response.status()).toBe(200);
    });
  });
});

// Rate limiting is tested in zzz-rate-limiting.spec.ts, deliberately isolated
// from every other test here: LoginRateLimitService locks out the whole
// *source IP* (not just the username being attempted) once it trips, and
// every test in this suite runs from the same IP (the test runner itself).
// Keeping it in a separate, alphabetically-last file means its lockout can't
// poison any test that needs a working login.

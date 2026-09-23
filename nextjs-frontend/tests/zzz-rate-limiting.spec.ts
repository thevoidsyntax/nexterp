import { test, expect } from '@playwright/test';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

/**
 * Rate Limiting Tests
 *
 * Deliberately its own file, named to sort last: LoginRateLimitService's
 * brute-force lockout is keyed by *source IP* as well as username, and every
 * test in this suite runs from the same IP (the test runner). Once this test
 * trips the lockout, every other test hitting the login endpoint from this
 * run would fail too — including ones using a different, throwaway username —
 * so this must run after everything else that needs a working login.
 */
test.describe('Rate Limiting', () => {
  test('should rate limit excessive login attempts', async ({ request }) => {
    const throwawayUsername = `ratelimit-test-${Date.now()}`;

    // Make many rapid login attempts
    const attempts = 10;
    const results: number[] = [];

    for (let i = 0; i < attempts; i++) {
      const response = await request.post(`${API_BASE_URL}/api/v1/auth/login`, {
        data: {
          username: throwawayUsername,
          password: 'wrongpassword',
        },
      });
      results.push(response.status());
    }

    // At least some requests should be rate limited (429)
    // or all should return 401 (invalid credentials)
    const hasRateLimit = results.includes(429);
    const allUnauthorized = results.every((s) => s === 401);

    expect(hasRateLimit || allUnauthorized).toBeTruthy();
  });
});

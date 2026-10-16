import { defineConfig, devices } from "@playwright/test";
import {
  BACKEND_ORIGIN,
  BACKEND_PORT,
  E2E_DB_PATH,
  E2E_TIME_ZONE,
  FRONTEND_ORIGIN,
  FRONTEND_PORT,
} from "./e2e/support/env";

/**
 * E2E against the real local stack (SPEC 12, Tester agent), started by `webServer`:
 * 1. Backend: `dotnet run` (Development, SQLite) on :5100 with a fresh throwaway database file.
 * 2. Frontend: `next build` + `next start` on :3100 with BACKEND_URL pointing at :5100.
 * The browser only ever talks to :3100; Next.js rewrites proxy /api/* to the backend (AC-PROXY-1).
 *
 * Run: `npm run test:e2e` (Chromium only: `npx playwright install chromium` once).
 */
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  // No retries: a flaky test should show up as a failure, not be hidden.
  retries: 0,
  workers: process.env.CI ? 1 : 2,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: FRONTEND_ORIGIN,
    locale: "en-US",
    timezoneId: E2E_TIME_ZONE,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      name: "backend",
      // Delete the throwaway DB first (webServers start before globalSetup, and the API opens the file on startup).
      // --no-launch-profile: launchSettings.json would force port 5000.
      command: `node e2e/support/reset-e2e-db.mts "${E2E_DB_PATH}" && dotnet run --project ../backend/src/TaskManager.Api --no-launch-profile`,
      url: `${BACKEND_ORIGIN}/health`,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: BACKEND_ORIGIN,
        ConnectionStrings__Default: `Data Source=${E2E_DB_PATH}`,
        Database__Provider: "Sqlite",
        Database__Schema: "EnsureCreated",
        // Config only: the e2e suite registers many users from one IP; the default is 10 per 60 s.
        RateLimiting__AuthPermitLimit: "1000",
        DOTNET_NOLOGO: "1",
        DOTNET_CLI_TELEMETRY_OPTOUT: "1",
      },
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      stdout: "pipe",
      stderr: "pipe",
    },
    {
      name: "frontend",
      // Production build: no on-demand route compilation during tests. Build output (.next) is separate
      // from `next dev` output (.next/dev). BACKEND_URL in the process env overrides .env.local.
      command: `npx next build && npx next start -p ${FRONTEND_PORT}`,
      url: `${FRONTEND_ORIGIN}/login`,
      env: {
        BACKEND_URL: `http://localhost:${BACKEND_PORT}`,
        NEXT_TELEMETRY_DISABLED: "1",
      },
      reuseExistingServer: !process.env.CI,
      timeout: 300_000,
      stdout: "pipe",
      stderr: "pipe",
    },
  ],
});

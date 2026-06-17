import { defineConfig, devices } from "@playwright/test";

// Pliaforma E2E — gerçek tarayıcı (Chromium headless), same-origin :3000 üzerinden.
// ÖN KOŞUL: backend (:5132) + frontend (:3000) çalışıyor ve `scripts/e2e-seed.sh` koşturulmuş olmalı.
// Sunucuları Playwright BAŞLATMAZ (seed + backend dışsaldır) — bkz. docs/TESTING.md.
export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  workers: 1, // deterministic: tek worker, sıralı
  retries: 1, // dev-server ilk derleme gecikmesini absorbe eder (flaky değil, soğuk başlangıç)
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  use: {
    baseURL: process.env.E2E_BASE_URL || "http://localhost:3000",
    trace: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 45_000,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
      testIgnore: /responsive\.spec\.ts/,
    },
    {
      name: "mobile",
      use: { ...devices["Pixel 7"] },
      testMatch: /responsive\.spec\.ts/,
    },
  ],
});

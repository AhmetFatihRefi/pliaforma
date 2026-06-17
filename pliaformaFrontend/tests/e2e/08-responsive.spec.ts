import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Bu dosya yalnızca "mobile" projesinde (Pixel 7 viewport) koşar (playwright.config.ts).
test.describe("Responsive / mobil smoke", () => {
  test("Owner: /dashboard ve /dashboard/schedule mobilde ana içerik görünür", async ({ page }) => {
    await login(page, users.owner);
    await expect(page.getByRole("heading", { name: /Merhaba/ })).toBeVisible();

    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
    // Yatay taşma olmamalı: belge genişliği viewport'u aşmamalı
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(2);
  });

  test("PlatformAdmin: /platform/studios mobilde ana içerik görünür", async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto("/platform/studios");
    await expect(page.getByRole("heading", { name: "Stüdyolar" })).toBeVisible();
    // Mobilde liste kart olarak gösterilir (tablo gizli) — kart içindeki <p> görünür olmalı
    await expect(page.locator("p").filter({ hasText: "E2E Pilates Studio" }).first()).toBeVisible();
  });
});

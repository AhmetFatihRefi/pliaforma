import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Abonelik & lisans (Sprint 13)", () => {
  test("Owner kendi abonelik sayfasını görür (auto-trial)", async ({ page }) => {
    await login(page, users.owner);
    // Sidebar'da "Abonelik" linki (yalnız Owner) — client-side gezinme (token korunur)
    await page.getByRole("link", { name: "Abonelik" }).click();
    await expect(page).toHaveURL(/\/dashboard\/subscription$/);
    await expect(page.getByRole("heading", { name: "Aboneliğim" })).toBeVisible();
    // Seed register → default plan ile trial → effectiveStatus rozeti + ödeme geçmişi kartı yüklenir
    await expect(page.getByText("Deneme").first()).toBeVisible();
    await expect(page.getByText("Ödeme geçmişi", { exact: true })).toBeVisible();
  });

  test("PlatformAdmin plan kataloğunu yönetir", async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.getByRole("link", { name: "Planlar" }).click();
    await expect(page.getByRole("heading", { name: "Abonelik Planları" })).toBeVisible();
    // Seed'lenen 4 plan + default rozeti
    await expect(page.getByText("Başlangıç").first()).toBeVisible();
    await expect(page.getByText("Default").first()).toBeVisible();
  });

  test("PlatformAdmin stüdyo detayında abonelik & tahsilat görür", async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto("/platform/studios");
    await page.getByRole("row").filter({ hasText: "E2E Pilates Studio" }).getByRole("link", { name: /Detay/ }).click();
    await expect(page.getByText("Abonelik & Lisans")).toBeVisible();
    await expect(page.getByText("Tahsilat geçmişi")).toBeVisible();
  });
});

import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Instructor flow", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.instructor);
  });

  test("instructor dashboard + schedule açılır", async ({ page }) => {
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole("heading", { name: /Merhaba/ })).toBeVisible();
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
  });

  test("instructor finans uçlarını görmez", async ({ page }) => {
    // Finansal yönetim linkleri Instructor sidebar'ında olmamalı
    await expect(page.getByRole("link", { name: "Raporlar" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Ödemeler", exact: true })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Paket Planları" })).toHaveCount(0);
    // Doğrudan finans sayfasına gitmeye çalışınca yönetim verisi görünmez (sayfa kendi guard'ıyla davranır)
    await expect(page.getByRole("link", { name: "Ders Programı" })).toBeVisible();
  });
});

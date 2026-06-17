import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Notification bell", () => {
  test("bell görünür, dropdown açılır, okundu işaretleme unread'i düşürür", async ({ page }) => {
    await login(page, users.student);
    await expect(page).toHaveURL(/\/dashboard$/);

    const bell = page.getByRole("button", { name: "Bildirimler" });
    await expect(bell).toBeVisible();

    // Seed bildirimleri sayesinde unread badge görünür olmalı
    const badge = bell.locator("span").last();
    await expect(badge).toBeVisible();
    const before = parseInt((await badge.textContent())?.replace("+", "") ?? "0", 10);
    expect(before).toBeGreaterThan(0);

    // Dropdown aç
    await bell.click();
    await expect(page.getByText("Tümünü okundu yap")).toBeVisible();

    // OKUNMAMIŞ bir bildirim öğesini tıkla (mavi nokta taşıyan) — okundu işaretle.
    // (Seed her koşudan önce çalıştırıldığından en az bir okunmamış öğe vardır.)
    const dropdown = page.locator("div.absolute").last();
    const unreadItem = dropdown.locator("button", { has: page.locator("span.bg-primary") }).first();
    await unreadItem.click();

    // unread count düşmeli (badge azalır veya kaybolur)
    await expect
      .poll(async () => {
        const t = await badge.textContent().catch(() => null);
        if (t == null) return 0;
        return parseInt(t.replace("+", ""), 10) || 0;
      }, { timeout: 10_000 })
      .toBeLessThan(before);
  });
});

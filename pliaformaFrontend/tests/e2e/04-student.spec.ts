import { test, expect } from "@playwright/test";
import { login, users, gotoAndExpectHeading } from "./helpers";

test.describe("Student flow", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.student);
  });

  test("öğrenci ana sayfaları açılır", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/book-classes", "Ders Bul");
    await gotoAndExpectHeading(page, "/dashboard/my-bookings", "Rezervasyonlarım");
    await gotoAndExpectHeading(page, "/dashboard/my-packages", "Paketlerim");
    await gotoAndExpectHeading(page, "/dashboard/my-payments", "Ödemelerim");
    await gotoAndExpectHeading(page, "/dashboard/my-attendance", "Katılım Geçmişim");
    await gotoAndExpectHeading(page, "/dashboard/notifications", "Bildirimler");
  });

  test("öğrenci finans yönetim uçlarını görmez (sidebar)", async ({ page }) => {
    // Student sidebar'ında yönetim linkleri olmamalı
    await expect(page.getByRole("link", { name: "Raporlar" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Ödemeler", exact: true })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Hocalar" })).toHaveCount(0);
    // Kendi linkleri olmalı
    await expect(page.getByRole("link", { name: "Paketlerim" })).toBeVisible();
  });

  test("seed bildirimi listede görünür", async ({ page }) => {
    await page.goto("/dashboard/notifications");
    await expect(page.getByRole("heading", { name: "Bildirimler" })).toBeVisible();
    // Seed reminder trigger → en az bir bildirim (ders/ödeme)
    await expect(page.getByText(/hatırlatma|gecikti|yaklaşıyor/i).first()).toBeVisible();
  });
});

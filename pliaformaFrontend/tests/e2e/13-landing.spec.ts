import { test, expect } from "@playwright/test";

test.describe("Public landing (/)", () => {
  test("/ oturumsuz landing hero + CTA'ları gösterir", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: /Pilates stüdyonuzu tek panelden yönetin/ })).toBeVisible();
    await expect(page.getByRole("link", { name: "Stüdyo Hesabı Oluştur" }).first()).toBeVisible();
    await expect(page.getByRole("link", { name: "Giriş Yap" }).first()).toBeVisible();
  });

  test("landing'de 'Giriş Yap' → /login", async ({ page }) => {
    await page.goto("/");
    await page.getByRole("link", { name: "Giriş Yap" }).first().click();
    await expect(page).toHaveURL(/\/login$/);
  });

  test("landing'de 'Stüdyo Hesabı Oluştur' → /register-studio", async ({ page }) => {
    await page.goto("/");
    await page.getByRole("link", { name: "Stüdyo Hesabı Oluştur" }).first().click();
    await expect(page).toHaveURL(/\/register-studio$/);
  });
});

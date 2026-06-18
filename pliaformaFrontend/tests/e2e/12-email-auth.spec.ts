import { test, expect } from "@playwright/test";

test.describe("E-posta doğrulama & şifre sıfırlama (Sprint 16)", () => {
  test("login'de 'Şifremi unuttum' linki forgot-password'a gider", async ({ page }) => {
    await page.goto("/login");
    await page.getByRole("link", { name: "Şifremi unuttum" }).click();
    await expect(page).toHaveURL(/\/forgot-password$/);
    await expect(page.getByRole("heading", { name: "Şifremi unuttum" })).toBeVisible();
  });

  test("forgot-password generic başarı mesajı gösterir", async ({ page }) => {
    await page.goto("/forgot-password");
    await page.locator("#email").fill("e2e-owner@pliaforma.local");
    await page.getByRole("button", { name: "Sıfırlama bağlantısı gönder" }).click();
    await expect(page.getByText(/şifre sıfırlama bağlantısı gönderildi/i)).toBeVisible();
  });

  test("reset-password tokensız uyarı gösterir", async ({ page }) => {
    await page.goto("/reset-password");
    await expect(page.getByRole("heading", { name: "Şifre sıfırla" })).toBeVisible();
    await expect(page.getByText(/doğrulama kodu bulunamadı/i)).toBeVisible();
  });

  test("verify-email tokensız hata gösterir", async ({ page }) => {
    await page.goto("/verify-email");
    await expect(page.getByText(/Geçersiz doğrulama bağlantısı/i)).toBeVisible();
  });
});

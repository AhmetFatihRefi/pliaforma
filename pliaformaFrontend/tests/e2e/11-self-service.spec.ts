import { test, expect } from "@playwright/test";
import { login, users, PW } from "./helpers";

test.describe("Self-service kayıt & katılım (Sprint 15)", () => {
  test("/register login'den erişilir ve açılır", async ({ page }) => {
    await page.goto("/login");
    await page.getByRole("link", { name: "Hesap oluşturun" }).click();
    await expect(page).toHaveURL(/\/register$/);
    await expect(page.getByRole("heading", { name: "Hesap oluşturun" })).toBeVisible();
  });

  test("yeni kullanıcı kayıt olunca /join-studio'ya yönlenir", async ({ page }) => {
    // Benzersiz e-posta (seed e2e-% kullanıcılarını siler → sonraki koşuda temizlenir).
    const email = `e2e-ss-${Date.now()}@pliaforma.local`;
    await page.goto("/register");
    await page.locator("#fullName").fill("E2E Self Service");
    await page.locator("#email").fill(email);
    await page.locator("#password").fill(PW);
    await page.getByRole("button", { name: "Hesap oluştur" }).click();
    await expect(page).toHaveURL(/\/join-studio$/);
    await expect(page.getByRole("heading", { name: "Bir stüdyoya katılın" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Başvuru gönder" })).toBeVisible();
  });

  test("Owner ayarlarda katılım kodunu görür + yenileyebilir", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/settings");
    await expect(page.getByRole("heading", { name: "Katılım kodu" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Kodu yenile" })).toBeVisible();
  });
});

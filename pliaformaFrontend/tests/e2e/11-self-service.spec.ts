import { test, expect } from "@playwright/test";
import { login, users, PW } from "./helpers";

test.describe("Self-service kayıt & katılım (Sprint 15)", () => {
  test("/register login'den erişilir ve açılır", async ({ page }) => {
    await page.goto("/login");
    await page.getByRole("link", { name: "Hesap oluşturun" }).click();
    await expect(page).toHaveURL(/\/register$/);
    await expect(page.getByRole("heading", { name: "Hesap oluşturun" })).toBeVisible();
  });

  test("yeni kullanıcı kayıt olunca /check-email'e yönlenir (Sprint 16)", async ({ page }) => {
    // Benzersiz e-posta (seed e2e-% kullanıcılarını siler → sonraki koşuda temizlenir).
    const email = `e2e-ss-${Date.now()}@pliaforma.local`;
    await page.goto("/register");
    await page.locator("#fullName").fill("E2E Self Service");
    await page.locator("#email").fill(email);
    await page.locator("#password").fill(PW);
    await page.getByRole("button", { name: "Hesap oluştur" }).click();
    // Sprint 16: register sonrası önce e-posta doğrulama ekranı.
    await expect(page).toHaveURL(/\/check-email$/);
    await expect(page.getByRole("heading", { name: "E-posta adresinizi doğrulayın" })).toBeVisible();
  });

  // Sprint 24: öğrenci kendi sözleşmesini + ödeme durumunu salt-okunur görür.
  test("öğrenci 'Sözleşmelerim' sayfasında kendi sözleşmesini + ödeme durumunu görür", async ({ page }) => {
    await login(page, users.student);
    // Nav'da "Sözleşmelerim" görünür.
    await page.goto("/dashboard/my-contracts");
    await expect(page.getByRole("heading", { name: "Sözleşmelerim" })).toBeVisible();
    // Seed öğrenciye Reformer sözleşmesi üretir → ders hakkı + ödeme durumu kartları.
    await expect(page.getByText("Reformer").first()).toBeVisible();
    // Sprint 28B: öğrenci self-servis LINE-bazlı (satır kartları).
    await expect(page.getByText("Kalan ders").first()).toBeVisible();
    await expect(page.getByText("Ödeme durumu").first()).toBeVisible();
    // Internal alan sızmamalı: "createdByName" sayfada görünmez (ham alan adı).
    const bodyText = await page.locator("body").innerText();
    expect(bodyText).not.toContain("createdByName");
    expect(bodyText).not.toContain("9999");
  });

  // Sprint 17 Rev3 (invite-only): JoinCode kartı ayarlardan kaldırıldı → artık görünmemeli.
  test("Owner ayarlarında 'Katılım kodu' kartı YOK (invite-only)", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/settings");
    await expect(page.getByRole("heading", { name: "Stüdyo Ayarları" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Katılım kodu" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Kodu yenile" })).toHaveCount(0);
  });
});

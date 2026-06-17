import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("PlatformAdmin flow", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.platformAdmin);
    await expect(page).toHaveURL(/\/platform$/);
  });

  test("/platform overview açılır", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Genel Bakış" })).toBeVisible();
    await expect(page.getByText("Toplam stüdyo", { exact: false }).first()).toBeVisible();
  });

  test("/platform/studios + detay + suspend/reactivate round-trip", async ({ page }) => {
    await page.getByRole("link", { name: "Stüdyolar" }).click();
    await expect(page.getByRole("heading", { name: "Stüdyolar" })).toBeVisible();
    // Askıya alınabilir test stüdyosu (seed) — masaüstü tabloda satırdaki "Detay" linki
    const row = page.getByRole("row").filter({ hasText: "E2E Test Studio" });
    await expect(row).toBeVisible();
    await row.getByRole("link", { name: /Detay/ }).click();
    await expect(page.getByRole("link", { name: "← Stüdyolar" })).toBeVisible();

    // Suspend modalı aç + gerekçe + askıya al (submit modal/dialog içinden)
    await page.getByRole("button", { name: "Askıya al" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("heading", { name: "Stüdyoyu askıya al" })).toBeVisible();
    await dialog.locator("#reason").fill("E2E test askı");
    await dialog.getByRole("button", { name: "Askıya al" }).click();
    await expect(page.getByText("Stüdyo askıya alındı.")).toBeVisible();

    // Yeniden aktifleştir (durumu eski haline döndür → idempotent)
    await page.getByRole("button", { name: "Yeniden aktifleştir" }).click();
    await expect(page.getByText("Stüdyo yeniden aktifleştirildi.")).toBeVisible();
  });

  test("/platform/health açılır", async ({ page }) => {
    await page.getByRole("link", { name: "Sistem Sağlığı" }).click();
    await expect(page.getByRole("heading", { name: "Sistem Sağlığı" })).toBeVisible();
    await expect(page.getByText("Veritabanı", { exact: false }).first()).toBeVisible();
  });

  test("PlatformAdmin normal /dashboard tenant'ını bypass edemez → /platform'a döner", async ({ page }) => {
    // Platform admin'in studio bağlamı yok; /dashboard guard'ı onu platform'a yönlendirir.
    await page.goto("/dashboard");
    await page.waitForURL(/\/(platform|login)$/, { timeout: 30_000 });
    expect(page.url()).toMatch(/\/platform$/);
  });
});

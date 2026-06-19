import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 18b (DEPRECATED Paket Planı V2): Paket modeli ana akıştan çıkarıldı (dormant; DB drop yok).
// Eski "kapsam/fiyat versiyonlama/paket-uygunluk" testleri kaldırıldı. Bu spec artık paket UI'ın
// ana akışta GÖRÜNMEDİĞİNİ ve ana kaynağın SÖZLEŞME olduğunu doğrular (uçtan uca sözleşme: 15-contracts).
test.describe("Paket deprecation (Sprint 18b) — paket UI ana akışta yok, sözleşme ana kaynak", () => {
  test("Owner sidebar'ında paket navigasyonu yok", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard");
    await expect(page.getByRole("link", { name: "Paket Planları" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: /paket/i })).toHaveCount(0);
  });

  test("Owner öğrenci finansı: paket sat/uygunluk UI yok; sözleşme ana akış", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/students");
    await expect(page.getByRole("heading", { name: /Öğrenci/i })).toBeVisible();
    // Sözleşme ana kaynak: her öğrenci satırında "Sözleşmeler" aksiyonu var.
    await expect(page.getByRole("button", { name: "Sözleşmeler" }).first()).toBeVisible();
    // Finans modalı yalnız ödeme — paket sat/plan/kredi bölümü yok.
    await page.getByRole("button", { name: "Ödemeler" }).first().click();
    await expect(page.getByText(/Paket sat|paket planı|kredi sat|paket uygunlu/i)).toHaveCount(0);
  });

  test("Öğrenci: paket navigasyonu yok; book-classes bilgi ekranı (paket-uygunluk 'Ders Bul' değil)", async ({ page }) => {
    await login(page, users.student);
    await page.goto("/dashboard");
    await expect(page.getByRole("link", { name: "Paketlerim" })).toHaveCount(0);
    await page.goto("/dashboard/book-classes");
    await expect(page.getByRole("heading", { name: "Ders rezervasyonu" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Ders Bul" })).toHaveCount(0);
  });
});

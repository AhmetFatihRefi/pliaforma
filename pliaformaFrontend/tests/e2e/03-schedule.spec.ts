import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint UX: Ders Programı artık haftalık takvim ızgarası. Ders aksiyonları (Onayla/Ertele/Yoklama…) ders
// bloğuna tıklayınca açılan DETAY DRAWER'ında. "Bugünü onayla" / "Grup düzeni" üst araç çubuğunda.
test.describe("Schedule / takvim (Owner)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
  });

  // Takvimdeki bir E2E Reformer bloğuna tıkla → detay drawer açılır ("Ders notu" drawer'a özgü).
  const openSession = async (page: import("@playwright/test").Page) => {
    const block = page.locator("button[title*='E2E Reformer']").first();
    await expect(block).toBeVisible({ timeout: 30_000 });
    await block.click();
    await expect(page.getByText("Ders notu")).toBeVisible();
  };

  test("takvimde ders bloğu + tıklayınca detay drawer (öğrenciler + yoklama)", async ({ page }) => {
    await openSession(page);
    await expect(page.getByText(/Öğrenciler \(/)).toBeVisible();
    await expect(page.getByRole("button", { name: "Yoklama" })).toBeVisible();
  });

  test("Sprint 19: 'Bugünü onayla' araç çubuğunda; drawer'da Onayla + Fiilî hoca", async ({ page }) => {
    await expect(page.getByRole("button", { name: "Bugünü onayla" })).toBeVisible();
    await openSession(page);
    await expect(page.getByRole("button", { name: "Onayla", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Fiilî hoca" })).toBeVisible();
  });

  test("Sprint 20: drawer'da 'İşlenmedi' aksiyonu", async ({ page }) => {
    await openSession(page);
    await expect(page.getByRole("button", { name: "İşlenmedi" })).toBeVisible();
  });

  test("Sprint 26: hoca filtresi seçilince o hocanın programı gösterilir", async ({ page }) => {
    await page.locator("select").first().selectOption({ index: 1 });
    await expect(page.getByText(/hocanın programı gösteriliyor/)).toBeVisible();
  });

  test("Sprint 22: 'Grup düzeni' modalı açılır + öğrencileri getir", async ({ page }) => {
    await page.getByRole("button", { name: "Grup düzeni" }).click();
    await expect(page.getByRole("heading", { name: "Grup düzeni değiştir" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Öğrencileri getir" })).toBeVisible();
  });

  test("Sprint 21: drawer'dan Ertele → taşıma modalı (mod A/B)", async ({ page }) => {
    await openSession(page);
    await page.getByRole("button", { name: "Ertele" }).click();
    await expect(page.getByRole("heading", { name: /Tarih\/Saat değiştir/ })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sadece bu dersi taşı" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Zincirleme kaydır" })).toBeVisible();
  });

  test("drawer'dan Rezervasyonlar modalı açılır", async ({ page }) => {
    await openSession(page);
    await page.getByRole("button", { name: "Rezervasyonlar" }).click();
    await expect(page.getByRole("heading", { name: /Rezervasyonlar$/ })).toBeVisible();
    await expect(page.getByText("E2E Student").first()).toBeVisible();
  });

  test("drawer'dan Yoklama modalı açılır", async ({ page }) => {
    await openSession(page);
    await page.getByRole("button", { name: "Yoklama" }).click();
    await expect(page.getByRole("heading", { name: /Yoklama$/ })).toBeVisible();
  });
});

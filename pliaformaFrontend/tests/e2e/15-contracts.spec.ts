import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 18a: sözleşmeden üretilen GRUP seansları backend'de Capacity=9999 sentinel ile temsil edilir.
// Bu teknik değer KULLANICIYA GÖRÜNMEMELİ; grup "limitsiz", bireysel "Bireysel · X/1" görünmeli.
test.describe("Sözleşme programı — kapasite gösterimi (Sprint 18a)", () => {
  test("schedule'da 9999 görünmez; grup 'limitsiz', bireysel 'Bireysel'", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/schedule");
    // Seans kartları yüklensin (seed bu hafta içine grup 10:00 + bireysel 14:00 dersleri üretti).
    await page.waitForLoadState("networkidle");
    await expect(page.getByText(/limitsiz|Bireysel|Doluluk/).first()).toBeVisible({ timeout: 30_000 });

    const bodyText = await page.locator("body").innerText();
    expect(bodyText).not.toContain("9999");      // sentinel asla görünmez
    expect(bodyText).toContain("limitsiz");        // grup seansı limitsiz
    expect(bodyText).toContain("Bireysel");        // bireysel seans etiketi
  });
});

// Sprint 23: sözleşme ek işlemi (amendment) + sözleşme-bazlı borç görünürlüğü (Owner/StudioAdmin).
test.describe("Sözleşme ek işlemi & borç (Sprint 23)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/students");
    await expect(page.getByRole("heading", { name: "Öğrenciler" })).toBeVisible();
  });

  // Seed "E2E Guest Misafir" için aktif (bireysel) sözleşme üretir → deterministik satır.
  const row = (page: import("@playwright/test").Page) =>
    page.locator("tr", { hasText: "E2E Guest Misafir" }).first();

  test("Finans modalında 'Borç özeti' (sözleşme ücreti/ek/ödenen/kalan) görünür", async ({ page }) => {
    await row(page).getByRole("button", { name: "Ödemeler" }).click();
    await expect(page.getByRole("heading", { name: /— Finans/ })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Borç özeti" })).toBeVisible();
    await expect(page.getByText("Kalan borç")).toBeVisible();
    // Ödeme eklerken sözleşme eşleme seçimi (Sözleşmesiz seçeneği) bulunur.
    await expect(page.getByRole("option", { name: "Sözleşmesiz" })).toBeAttached();
  });

  test("Sözleşmeler modalında aktif sözleşmede 'Ek işlem' butonu var", async ({ page }) => {
    await row(page).getByRole("button", { name: "Sözleşmeler" }).click();
    await expect(page.getByRole("heading", { name: /Sözleşmeler —/ })).toBeVisible();
    // Seed aktif sözleşme üretir → ek işlem akışı görünür.
    await expect(page.getByRole("button", { name: "Ek işlem" }).first()).toBeVisible();
  });
});

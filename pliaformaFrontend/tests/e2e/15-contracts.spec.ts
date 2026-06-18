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

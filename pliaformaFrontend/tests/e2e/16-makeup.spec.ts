import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 20: telafi görünürlüğü — kazanılan/kullanılan/kullanılabilir özeti; öğrenci salt-okunur.
test.describe("Telafi kredileri görünürlüğü (Sprint 20)", () => {
  test("Owner telafi ekranı: öğrenci seçince kazanılan/kullanılan/kullanılabilir özeti", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/makeup-credits");
    await expect(page.getByRole("heading", { name: "Telafi Kredileri" })).toBeVisible();
    // Seed E2E Student'a 1 telafi kredisi verir → seç, özet görünür.
    await page.getByRole("button", { name: "E2E Student" }).first().click();
    // Sprint 28C-2: byLine kartları da "kazanılan/kullanılan" içerir → özet etiketini exact eşleştir.
    await expect(page.getByText("Kazanılan", { exact: true })).toBeVisible();
    await expect(page.getByText("Kullanılabilir", { exact: true })).toBeVisible();
  });

  test("Öğrenci telafi ekranı SALT-OKUNUR (özet var, kredi ver/geri al yok)", async ({ page }) => {
    await login(page, users.student);
    await page.goto("/dashboard/my-makeup-credits");
    await expect(page.getByRole("heading", { name: "Telafi Kredilerim" })).toBeVisible();
    await expect(page.getByText("Kullanılabilir")).toBeVisible();
    // Salt-okunur: yönetim aksiyonu yok.
    await expect(page.getByRole("button", { name: "Kredi ver" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Geri al" })).toHaveCount(0);
  });
});

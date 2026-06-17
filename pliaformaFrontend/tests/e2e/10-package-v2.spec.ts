import { test, expect } from "@playwright/test";
import { login, users, gotoAndExpectHeading } from "./helpers";

test.describe("Paket Planı V2 — kapsam & fiyat (Sprint 14)", () => {
  test("Owner paket planlarında ders grubu kapsamı + fiyat sütununu görür", async ({ page }) => {
    await login(page, users.owner);
    await gotoAndExpectHeading(page, "/dashboard/package-plans", "Paket Planları");
    // Sprint 14: yeni "Geçerli grup" sütunu
    await expect(page.getByRole("columnheader", { name: "Geçerli grup" })).toBeVisible();
    // Seed planı backfill ile tüm gruplarda geçerli
    await expect(page.getByText("Tüm gruplar").first()).toBeVisible();
  });

  test("Plan formunda kapsam toggle + gelecek fiyat alanları var", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/package-plans");
    await page.getByRole("button", { name: "+ Yeni plan" }).click();
    await expect(page.getByText("Tüm ders gruplarında geçerli")).toBeVisible();
    await expect(page.getByText("Gelecek fiyat (opsiyonel)")).toBeVisible();
    // Kapsam kapatılınca ders grubu seçimi görünür
    await page.getByText("Tüm ders gruplarında geçerli").click();
    await expect(page.getByText("Bu plan yalnızca seçilen ders gruplarında kullanılabilir.")).toBeVisible();
  });

  test("Öğrenci 'Ders Bul' sayfası paket uygunluğuyla render olur", async ({ page }) => {
    await login(page, users.student);
    await gotoAndExpectHeading(page, "/dashboard/book-classes", "Ders Bul");
  });
});

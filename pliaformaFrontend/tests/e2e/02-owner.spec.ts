import { test, expect } from "@playwright/test";
import { login, users, gotoAndExpectHeading } from "./helpers";

test.describe("Owner dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.owner);
  });

  test("dashboard kartları + sidebar görünür", async ({ page }) => {
    await expect(page.getByRole("heading", { name: /Merhaba/ })).toBeVisible();
    // Studio dashboard Stat kartları
    await expect(page.getByText("Öğrenci", { exact: false }).first()).toBeVisible();
    // Owner sidebar linkleri (rol bazlı)
    await expect(page.getByRole("link", { name: "Raporlar" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Ders Programı" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Öğrenciler" })).toBeVisible();
    // Sprint 26: Hoca müsaitlik modülü ana üründen kaldırıldı → sidebar'da "Müsaitlik" YOK.
    await expect(page.getByRole("link", { name: "Müsaitlik" })).toHaveCount(0);
  });

  test("/dashboard/reports açılır + hoca raporu PARASIZ (Sprint 19)", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/reports", "Raporlar");
    await page.getByRole("button", { name: "Hoca iş hacmi" }).click();
    // Parasal sütun yok; Sprint 30 maaş-hesabı kırılımı: Planlanan/İşlenen/İşlenmedi sütunları var.
    await expect(page.getByRole("columnheader", { name: "Tahmini kazanç" })).toHaveCount(0);
    await expect(page.getByRole("columnheader", { name: "İşlenen" })).toBeVisible();
    await expect(page.getByRole("columnheader", { name: "Planlanan" })).toBeVisible();
  });

  test("/dashboard/students açılır + seed öğrenci görünür", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/students", "Öğrenciler");
    await expect(page.getByText("E2E Student").first()).toBeVisible();
  });

  test("/dashboard/instructors açılır + ücret sütunu YOK (Sprint 19)", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/instructors", /Hocalar/);
    await expect(page.getByRole("columnheader", { name: "Ücret" })).toHaveCount(0);
  });
});

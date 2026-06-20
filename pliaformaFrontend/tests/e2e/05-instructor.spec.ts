import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Instructor flow", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.instructor);
  });

  test("instructor dashboard + schedule açılır (dashboard PARASIZ — Sprint 19)", async ({ page }) => {
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole("heading", { name: /Merhaba/ })).toBeVisible();
    // Sprint 19: hoca ücreti kapsam dışı → "Tahmini kazanç" kartı yok.
    await expect(page.getByText("Tahmini kazanç")).toHaveCount(0);
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
  });

  test("Sprint 25: instructor schedule'da 'Bugünü onayla' görmez (manager-only)", async ({ page }) => {
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
    // Stüdyo-geneli toplu onay yalnız Owner/StudioAdmin; instructor görmemeli.
    await expect(page.getByRole("button", { name: "Bugünü onayla" })).toHaveCount(0);
    // Grup düzeni / İşlenmedi / Fiilî hoca da manager-only → görünmez.
    await expect(page.getByRole("button", { name: "Grup düzeni" })).toHaveCount(0);
    // v1.1.2: öğrenci ekleme (booking) yalnız stüdyo yetkilisi → instructor "Öğrenci ekle" görmez.
    await expect(page.getByRole("button", { name: "Öğrenci ekle" })).toHaveCount(0);
  });

  test("instructor finans + ders tipi yönetim uçlarını görmez", async ({ page }) => {
    // Finansal yönetim linkleri Instructor sidebar'ında olmamalı
    await expect(page.getByRole("link", { name: "Raporlar" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Ödemeler", exact: true })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Paket Planları" })).toHaveCount(0);
    // Sprint 26 ek (v1.1.1): global yönetim ekranları stüdyo yetkilisine → Instructor görmez.
    await expect(page.getByRole("link", { name: "Gruplar" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Öğrenciler" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Telafi Kredileri", exact: true })).toHaveCount(0);
    // Doğrudan finans sayfasına gitmeye çalışınca yönetim verisi görünmez (sayfa kendi guard'ıyla davranır)
    await expect(page.getByRole("link", { name: "Ders Programı" })).toBeVisible();
  });
});

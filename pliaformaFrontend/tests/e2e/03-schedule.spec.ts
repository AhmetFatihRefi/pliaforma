import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Schedule / booking / attendance (Owner)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
  });

  test("ders kartı görünür (seed yaklaşan seans)", async ({ page }) => {
    // Seed: ~20 saat sonrasına bir E2E Reformer seansı + öğrenci rezervasyonu.
    // Kart içindeki <p> (filtre dropdown'undaki <option> değil) görünür olmalı + aksiyon butonu.
    await expect(page.locator("p").filter({ hasText: "E2E Reformer" }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Yoklama" }).first()).toBeVisible();
  });

  test("Sprint 19: Bugünü onayla + Onayla + Fiilî hoca aksiyonları var", async ({ page }) => {
    await expect(page.getByRole("button", { name: "Bugünü onayla" })).toBeVisible();
    // Scheduled kartlarda ders onayı + fiilî hoca aksiyonları (yönetici).
    await expect(page.getByRole("button", { name: "Onayla" }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Fiilî hoca" }).first()).toBeVisible();
  });

  test("Sprint 20: 'İşlenmedi' aksiyonu Scheduled kartlarda var", async ({ page }) => {
    await expect(page.getByRole("button", { name: "İşlenmedi" }).first()).toBeVisible();
  });

  test("Sprint 21: taşıma modalında mod A/B (tek taşı + zincirleme kaydır)", async ({ page }) => {
    await page.getByRole("button", { name: "Ertele" }).first().click();
    await expect(page.getByRole("heading", { name: /Tarih\/Saat değiştir/ })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sadece bu dersi taşı" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Zincirleme kaydır" })).toBeVisible();
    // Zincirleme moda geçince "Önizle" çıkar.
    await page.getByRole("button", { name: "Zincirleme kaydır" }).click();
    await expect(page.getByRole("button", { name: "Önizle" })).toBeVisible();
  });

  test("rezervasyonlar modalı açılır", async ({ page }) => {
    await page.getByRole("button", { name: "Rezervasyonlar" }).first().click();
    // Modal başlığı: "<grup> — Rezervasyonlar"
    await expect(page.getByRole("heading", { name: /Rezervasyonlar$/ })).toBeVisible();
    await expect(page.getByText("E2E Student").first()).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(page.getByRole("heading", { name: /Rezervasyonlar$/ })).toBeHidden();
  });

  test("yoklama modalı açılır", async ({ page }) => {
    await page.getByRole("button", { name: "Yoklama" }).first().click();
    await expect(page.getByRole("heading", { name: /Yoklama$/ })).toBeVisible();
  });
});

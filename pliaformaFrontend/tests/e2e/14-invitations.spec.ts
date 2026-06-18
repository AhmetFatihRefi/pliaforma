import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 17: Davetlerim, stüdyosuz kullanıcı empty-state, hoca self-apply kapalı.
// Serial: 3. test daveti kabul ederek invitee'yi üye yapar (mutasyon) → sıralı koşmalı.
test.describe.serial("Davetlerim & stüdyosuz kullanıcı (Sprint 17)", () => {
  test("stüdyosuz davetli /join-studio empty-state + Davetlerim'de daveti görür", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByRole("heading", { name: /Henüz bir stüdyoya bağlı değilsiniz/ })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Davetlerim" })).toBeVisible();
    await expect(page.getByText("E2E Pilates Studio").first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Kabul et" }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Reddet" }).first()).toBeVisible();
  });

  test("öğrenci başvuru formunda 'Hoca olarak katıl' seçeneği YOK", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByText("Hocalar kodla başvuramaz", { exact: false })).toBeVisible();
    await expect(page.getByText("Hoca olarak katıl")).toHaveCount(0);
  });

  test("davet kabul edilince panele yönlenir", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await page.getByRole("button", { name: "Kabul et" }).first().click();
    await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
  });
});

import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 17 Rev2: kayıt niyetine göre /join-studio UX'i + Davetlerim + hoca self-apply kapalı.
test.describe.serial("Davetlerim & stüdyosuz kullanıcı (Sprint 17 Rev2)", () => {
  // Instructor intent: JoinCode formu YOK, "davet bekliyorsunuz" + Davetlerim.
  test("Instructor intent → davet bekleme ekranı + Davetlerim, JoinCode formu YOK", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByRole("heading", { name: "Davet bekliyorsunuz" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Davetlerim" })).toBeVisible();
    await expect(page.getByText("E2E Pilates Studio").first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Kabul et" }).first()).toBeVisible();
    // Hoca ekranında öğrenci JoinCode formu olmamalı.
    await expect(page.getByLabel("Katılım kodu")).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Öğrenci başvurusu gönder" })).toHaveCount(0);
  });

  // Student intent: JoinCode formu görünür.
  test("Student intent → JoinCode formu görünür", async ({ page }) => {
    await login(page, users.nojoinStudent);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByLabel("Katılım kodu")).toBeVisible();
    await expect(page.getByRole("button", { name: "Öğrenci başvurusu gönder" })).toBeVisible();
    // Öğrenci ekranında da rol seçici (hoca seçeneği) yok.
    await expect(page.getByText("Hoca olarak katıl")).toHaveCount(0);
  });

  // Instructor daveti kabul → panele (verified seed).
  test("Instructor davet kabul edilince panele yönlenir", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await page.getByRole("button", { name: "Kabul et" }).first().click();
    await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
  });
});

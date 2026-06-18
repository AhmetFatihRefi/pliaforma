import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 17 Rev3 (invite-only): /join-studio'da JoinCode formu YOK (hiçbir intent'te). Yalnız davet bekleme + Davetlerim.
test.describe.serial("Davetlerim & invite-only onboarding (Sprint 17 Rev3)", () => {
  test("Instructor intent → davet bekleme metni + Davetlerim, JoinCode YOK", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByRole("heading", { name: /Henüz bir stüdyoya bağlı değilsiniz/ })).toBeVisible();
    await expect(page.getByText(/hoca olarak katılmak için/i)).toBeVisible();
    await expect(page.getByRole("heading", { name: "Davetlerim" })).toBeVisible();
    await expect(page.getByText("E2E Pilates Studio").first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Kabul et" }).first()).toBeVisible();
    // Invite-only: kod inputu / başvuru butonu olmamalı.
    await expect(page.getByLabel("Katılım kodu")).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Öğrenci başvurusu gönder" })).toHaveCount(0);
  });

  test("Student intent → davet bekleme metni, JoinCode YOK", async ({ page }) => {
    await login(page, users.nojoinStudent);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await expect(page.getByText(/öğrenci olarak katılmak için/i)).toBeVisible();
    await expect(page.getByLabel("Katılım kodu")).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Öğrenci başvurusu gönder" })).toHaveCount(0);
  });

  test("Instructor davet kabul edilince panele yönlenir", async ({ page }) => {
    await login(page, users.invitee);
    await page.waitForURL(/\/join-studio/, { timeout: 30_000 });
    await page.getByRole("button", { name: "Kabul et" }).first().click();
    await page.waitForURL(/\/dashboard/, { timeout: 30_000 });
  });
});

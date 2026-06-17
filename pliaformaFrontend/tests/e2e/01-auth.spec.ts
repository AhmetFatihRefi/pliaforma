import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

test.describe("Auth / redirect", () => {
  test("Owner login → /dashboard", async ({ page }) => {
    await login(page, users.owner);
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test("Student login → /dashboard", async ({ page }) => {
    await login(page, users.student);
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test("Instructor login → /dashboard", async ({ page }) => {
    await login(page, users.instructor);
    await expect(page).toHaveURL(/\/dashboard$/);
  });

  test("PlatformAdmin login → /platform", async ({ page }) => {
    await login(page, users.platformAdmin);
    await expect(page).toHaveURL(/\/platform$/);
  });

  test("Logout → /login", async ({ page }) => {
    await login(page, users.owner);
    await page.getByRole("button", { name: "Çıkış" }).click();
    await page.waitForURL(/\/login$/, { timeout: 30_000 });
    await expect(page.getByRole("button", { name: "Giriş yap" })).toBeVisible();
  });

  test("Korumalı sayfa oturumsuz → /login", async ({ page }) => {
    await page.goto("/dashboard");
    await page.waitForURL(/\/login$/, { timeout: 30_000 });
  });
});

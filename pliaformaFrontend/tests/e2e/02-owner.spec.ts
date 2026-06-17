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
  });

  test("/dashboard/reports açılır", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/reports", "Raporlar");
  });

  test("/dashboard/students açılır + seed öğrenci görünür", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/students", "Öğrenciler");
    await expect(page.getByText("E2E Student").first()).toBeVisible();
  });

  test("/dashboard/instructors açılır", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/instructors", /Hocalar/);
  });
});

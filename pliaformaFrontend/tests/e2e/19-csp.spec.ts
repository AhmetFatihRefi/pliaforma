import { test, expect, type Page } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 31C-2 — CSP enforce guard. CSP_MODE=enforce ile koşulduğunda anahtar sayfalar:
// CSP ihlali (Refused to execute…), hydration error veya beyaz ekran olursa FAIL.
// Not: global console-listener tüm suite'e EKLENMEDİ (flakiness); yalnız bu spec içinde.

function attachGuards(page: Page) {
  const csp: string[] = [];
  const fatal: string[] = [];
  page.on("console", (m) => {
    if (m.type() !== "error") return;
    const t = m.text();
    if (/content security policy|refused to (execute|load|apply|connect|create)/i.test(t)) csp.push(t);
    else if (/hydrat|did not match|minified react error #(418|423|425)/i.test(t)) fatal.push(t);
  });
  page.on("pageerror", (e) => fatal.push(String(e)));
  return { csp, fatal };
}

function assertClean(errs: { csp: string[]; fatal: string[] }) {
  expect(errs.csp, `CSP ihlali: ${errs.csp.join(" | ")}`).toHaveLength(0);
  expect(errs.fatal, `hydration/pageerror: ${errs.fatal.join(" | ")}`).toHaveLength(0);
}

test.describe("CSP enforce — console/hydration guard (Sprint 31C-2)", () => {
  test("public: /login ve /register-studio CSP-temiz, beyaz ekran yok", async ({ page }) => {
    const errs = attachGuards(page);
    await page.goto("/login");
    await expect(page.locator("#email")).toBeVisible();
    await page.goto("/register-studio");
    await expect(page.locator("body")).not.toBeEmpty();
    assertClean(errs);
  });

  test("owner: /dashboard ve /dashboard/my-profile CSP-temiz", async ({ page }) => {
    const errs = attachGuards(page);
    await login(page, users.owner);
    await page.goto("/dashboard");
    await expect(page.locator("body")).not.toBeEmpty();
    await page.goto("/dashboard/my-profile");
    await expect(page.locator("body")).not.toBeEmpty();
    assertClean(errs);
  });

  test("platformAdmin: /platform/security-audit CSP-temiz", async ({ page }) => {
    const errs = attachGuards(page);
    await login(page, users.platformAdmin);
    await page.goto("/platform/security-audit");
    await expect(page.getByRole("heading", { name: "Güvenlik Denetimi" })).toBeVisible();
    assertClean(errs);
  });

  test("/mfa-setup sayfası CSP-temiz (render/yönlendirme, beyaz ekran yok)", async ({ page }) => {
    const errs = attachGuards(page);
    await login(page, users.owner);
    await page.goto("/mfa-setup");
    await page.waitForLoadState("networkidle");
    await expect(page.locator("body")).not.toBeEmpty();
    assertClean(errs);
  });
});

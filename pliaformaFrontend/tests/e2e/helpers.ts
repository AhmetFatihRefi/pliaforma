import { Page, expect } from "@playwright/test";

// e2e-seed.sh ile birebir aynı sabit kimlikler (deterministic).
export const PW = "E2ePass123!";
export const users = {
  platformAdmin: "e2e-padmin@pliaforma.local",
  owner: "e2e-owner@pliaforma.local",
  admin: "e2e-admin@pliaforma.local",
  instructor: "e2e-instructor@pliaforma.local",
  student: "e2e-student@pliaforma.local",
  invitee: "e2e-invitee@pliaforma.local", // Sprint 17 Rev2: Instructor intent + bekleyen hoca daveti
  nojoinStudent: "e2e-nojoin-student@pliaforma.local", // Sprint 17 Rev2: Student intent, stüdyosuz
};

/** /login üzerinden giriş yapar ve /login dışına yönlenmeyi bekler. */
export async function login(page: Page, email: string): Promise<void> {
  await page.goto("/login");
  await page.locator("#email").fill(email);
  await page.locator("#password").fill(PW);
  await page.getByRole("button", { name: "Giriş yap" }).click();
  await page.waitForURL((url) => !url.pathname.includes("/login"), { timeout: 30_000 });
}

/** Bir dashboard sayfasına gider ve verilen başlığın görünmesini bekler. */
export async function gotoAndExpectHeading(page: Page, path: string, heading: string | RegExp): Promise<void> {
  await page.goto(path);
  await expect(page.getByRole("heading", { name: heading })).toBeVisible();
}

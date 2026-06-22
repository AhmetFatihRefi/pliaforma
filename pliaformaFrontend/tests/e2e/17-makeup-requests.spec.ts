import { test, expect } from "@playwright/test";
import { login, users } from "./helpers";

// Sprint 27 — Instructor Makeup Request & Pending Schedule Blocks.
// Seed: e2e-instructor'ın E2E Student ile aktif sözleşmesi var + E2E Student'ın telafi kredisi var → hoca talep açabilir.
// Akış: Instructor talep oluşturur → schedule'da "Onay bekliyor" (yoklama/onay YOK) → Owner onaylar → gerçek telafi
// dersi → Student programında "Onaylı telafi" görünür. Serial: 2. test 1.'in oluşturduğu pending'e dayanır.

function tomorrowAt(hour: number): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(hour)}:00`;
}

test.describe.serial("Telafi talepleri (Sprint 27)", () => {
  test("Instructor telafi talebi oluşturur → schedule'da 'Onay bekliyor' (yoklama/onay yok)", async ({ page }, testInfo) => {
    await login(page, users.instructor);
    // Sprint 30: talep oluşturma 'Telafi Talepleri' sayfasında (ders programından kaldırıldı); hoca da erişir.
    await page.goto("/dashboard/makeup-requests");
    await expect(page.getByRole("heading", { name: "Telafi Talepleri" })).toBeVisible();

    await page.getByRole("button", { name: "+ Yeni telafi talebi" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("heading", { name: "Telafi dersi talebi" })).toBeVisible();

    // Sprint 28C-2: talep LINE-bazlı → satır seçici (2. select; 1. select seviye filtresi). Line etiketi öğrenci adını içerir.
    await expect(dialog.locator("option", { hasText: "E2E Student" })).toHaveCount(1);
    const optVal = await dialog.locator("option", { hasText: "E2E Student" }).getAttribute("value");
    await dialog.locator("select").nth(1).selectOption(optVal!);

    // Retry'da çakışmayı önlemek için saat = 6 + retry (farklı slot).
    await dialog.locator('input[type="datetime-local"]').fill(tomorrowAt(6 + testInfo.retry));
    await dialog.getByRole("button", { name: "Talep oluştur" }).click();
    await expect(dialog).toBeHidden({ timeout: 15_000 });

    // Schedule'da pending blok (dashed) — "Onay bekliyor"; yoklama/onay/öğrenci-ekle AKSİYONU YOK.
    await page.goto("/dashboard/schedule");
    await expect(page.getByRole("heading", { name: "Ders Programı" })).toBeVisible();
    const pendingCard = page.locator(".border-dashed").filter({ hasText: "Onay bekliyor" }).first();
    await expect(pendingCard).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("button", { name: "Yoklama" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Onayla", exact: true })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Öğrenci ekle" })).toHaveCount(0);
    // Instructor pending bloğa tıklayınca geri çekme onay modalı (özgün confirm) açılır. Vazgeç → talep PENDING kalır
    // (sonraki test owner olarak onaylar).
    await pendingCard.click();
    const cd = page.getByRole("alertdialog").filter({ hasText: "Telafi talebini geri çek" });
    await expect(cd).toBeVisible();
    await cd.getByRole("button", { name: "Vazgeç" }).click();
    await expect(cd).toBeHidden({ timeout: 15_000 });
  });

  test("Owner onaylar → gerçek telafi dersi; Student programında onaylı görünür", async ({ page }) => {
    await login(page, users.owner);
    await page.goto("/dashboard/makeup-requests");
    await expect(page.getByRole("heading", { name: "Telafi Talepleri" })).toBeVisible();

    // Pending listede E2E Student talebi (satır) + "Onayla" → özgün onay modalı → modaldan "Onayla".
    await expect(page.getByRole("cell", { name: "E2E Student" }).first()).toBeVisible({ timeout: 15_000 });
    await page.getByRole("button", { name: "Onayla" }).first().click();
    const cd = page.getByRole("alertdialog").filter({ hasText: "Telafi talebini onayla" });
    await expect(cd).toBeVisible();
    await cd.getByRole("button", { name: "Onayla" }).click();
    await expect(page.getByText(/telafi dersi onaylandı/i)).toBeVisible({ timeout: 15_000 });

    // Student kendi programında onaylı telafi dersini SALT görür.
    await login(page, users.student);
    await page.goto("/dashboard/my-bookings");
    await expect(page.getByRole("heading", { name: "Telafi Dersleri" })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("Onaylı telafi").first()).toBeVisible();
  });
});

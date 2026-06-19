import { test, expect } from "@playwright/test";
import { login, users, gotoAndExpectHeading, PW } from "./helpers";

// Sprint 18b: öğrenci self-booking KALDIRILDI. Program stüdyo tarafından (sözleşmeden) oluşturulur;
// öğrenci yalnız kendi programını SALT-OKUNUR görür. book/cancel-self uçları 410 Gone.
test.describe("Student flow (Sprint 18b: self-service kapalı, salt-okunur program)", () => {
  test.beforeEach(async ({ page }) => {
    await login(page, users.student);
  });

  test("öğrenci kendi ekranlarını görür (salt-okunur program)", async ({ page }) => {
    await gotoAndExpectHeading(page, "/dashboard/my-bookings", "Rezervasyonlarım");
    await gotoAndExpectHeading(page, "/dashboard/my-payments", "Ödemelerim");
    await gotoAndExpectHeading(page, "/dashboard/my-attendance", "Katılım Geçmişim");
    await gotoAndExpectHeading(page, "/dashboard/notifications", "Bildirimler");
  });

  test("book-classes artık rezervasyon değil bilgi ekranı (booking butonu yok)", async ({ page }) => {
    // Doğrudan eski URL'e gidilse bile 404 değil, bilgi ekranı.
    await gotoAndExpectHeading(page, "/dashboard/book-classes", "Ders rezervasyonu");
    await expect(page.getByText(/stüdyo tarafından oluşturulur/i)).toBeVisible();
    // Rezervasyon yapacak buton YOK; yalnız "Programıma git" yönlendirmesi.
    await expect(page.getByRole("button", { name: /rezervasyon|ders al|book|katıl/i })).toHaveCount(0);
    await expect(page.getByRole("link", { name: /Programıma git/i })).toBeVisible();
  });

  test("sidebar: self-service/paket navigasyonu yok, sadece Programım", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Raporlar" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Hocalar" })).toHaveCount(0);
    // Sprint 18b: paket + "Ders Bul" navigasyonu kaldırıldı.
    await expect(page.getByRole("link", { name: "Paketlerim" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Ders Bul" })).toHaveCount(0);
    // Öğrenci yalnız kendi (salt-okunur) programını görür.
    await expect(page.getByRole("link", { name: "Programım" })).toBeVisible();
  });

  test("self-booking + self-cancel uçları kapalı → 410 Gone", async ({ page }) => {
    const loginResp = await page.request.post("/api/auth/login", {
      data: { email: users.student, password: PW },
    });
    expect(loginResp.ok()).toBeTruthy();
    // Login yanıtı zarflı: { success, message, data: { accessToken } }
    const token = (await loginResp.json()).data.accessToken as string;
    const auth = { Authorization: `Bearer ${token}` };
    const anyGuid = "00000000-0000-0000-0000-000000000001";
    // Controller GoneException'ı hemen fırlatır → herhangi bir id ile 410.
    const book = await page.request.post(`/api/student/class-sessions/${anyGuid}/book`, { headers: auth, data: {} });
    expect(book.status()).toBe(410);
    const cancel = await page.request.post(`/api/student/bookings/${anyGuid}/cancel`, { headers: auth, data: {} });
    expect(cancel.status()).toBe(410);
  });

  test("seed bildirimi listede görünür", async ({ page }) => {
    await page.goto("/dashboard/notifications");
    await expect(page.getByRole("heading", { name: "Bildirimler" })).toBeVisible();
    // Seed reminder trigger → en az bir bildirim (ders/ödeme).
    await expect(page.getByText(/hatırlatma|gecikti|yaklaşıyor/i).first()).toBeVisible();
  });
});

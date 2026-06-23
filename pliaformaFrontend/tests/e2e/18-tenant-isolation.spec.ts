import { test, expect } from "@playwright/test";
import { login, users, PW } from "./helpers";

// Sprint 31C-1 — Tenant izolasyon regresyonu (cross-tenant erişim yok) + PlatformAdmin denetim viewer RBAC.
test.describe("Tenant isolation (Sprint 31C-1)", () => {
  // Bir stüdyonun token'ı başka stüdyonun nesnesine erişemez (404) ve üye olmadığı stüdyoya geçemez (403).
  test("cross-tenant nesne erişimi 404; switch-studio üye olmayana 403; UI leakage yok", async ({ page, request }) => {
    const ts = Date.now();
    const reg = async (n: string) => {
      const r = await request.post("/api/auth/register-studio", {
        data: { studioName: `Iso${n}-${ts}`, ownerFullName: `O${n}`, email: `iso_${n}_${ts}@pf.local`, password: PW },
      });
      expect(r.ok()).toBeTruthy();
      const body = await r.json();
      const sid = JSON.stringify(body).match(/"studioId":"([^"]+)"/)?.[1];
      return { token: body.data.accessToken as string, studioId: sid as string };
    };

    const a = await reg("A");
    const b = await reg("B");
    expect(a.token).toBeTruthy();
    expect(b.studioId).toBeTruthy();

    // Studio B'de bir öğrenci oluştur.
    const created = await request.post("/api/studio/students/guest", {
      headers: { Authorization: `Bearer ${b.token}` },
      data: { fullName: "Iso B Student", source: "WalkIn" },
    });
    expect(created.ok()).toBeTruthy();
    const bStudentId = (await created.json()).data.id as string;

    // A token → B öğrencisi: 404 (cross-tenant gizleme).
    const crossRead = await request.get(`/api/studio/students/${bStudentId}`, {
      headers: { Authorization: `Bearer ${a.token}` },
    });
    expect(crossRead.status()).toBe(404);

    // A token → B öğrencisini pasifleştir (write): 404.
    const crossWrite = await request.post(`/api/studio/students/${bStudentId}/deactivate`, {
      headers: { Authorization: `Bearer ${a.token}` },
      data: {},
    });
    expect(crossWrite.status()).toBe(404);

    // A, üye olmadığı B'ye geçemez: 403.
    const sw = await request.post("/api/auth/switch-studio", {
      headers: { Authorization: `Bearer ${a.token}` },
      data: { studioId: b.studioId },
    });
    expect(sw.status()).toBe(403);

    // Sanity: B kendi öğrencisini görebilir (izolasyon fazla bloklamıyor).
    const ownRead = await request.get(`/api/studio/students/${bStudentId}`, {
      headers: { Authorization: `Bearer ${b.token}` },
    });
    expect(ownRead.status()).toBe(200);
  });

  // Güvenlik denetim viewer'ı yalnız PlatformAdmin'e açık (UI RBAC).
  test("PlatformAdmin denetim viewer'ı görür; Owner erişemez (yönlendirilir)", async ({ page }) => {
    await login(page, users.platformAdmin);
    await page.goto("/platform/security-audit");
    await expect(page.getByRole("heading", { name: "Güvenlik Denetimi" })).toBeVisible();

    // Owner /platform/* erişemez → (platform) layout /dashboard'a yönlendirir.
    await login(page, users.owner);
    await page.goto("/platform/security-audit");
    await page.waitForURL((url) => !url.pathname.includes("/platform"), { timeout: 30_000 });
    await expect(page.getByRole("heading", { name: "Güvenlik Denetimi" })).toHaveCount(0);
  });
});

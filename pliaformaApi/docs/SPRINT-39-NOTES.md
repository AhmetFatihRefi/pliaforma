# SPRINT 39 — Security Hardening / Defense-in-Depth Review

> **Durum:** Analiz + plan turu. Kapsam onaylandı: **A1 + A2 + B1** uygulanacak; **B2 (global tenant query filter)** yalnızca spec, ayrı sprint.
> **Kaynak:** Bu sprint, tam-uygulama güvenlik taramasında (auth, çok-kiracılı izolasyon, injection, crypto/secrets, veri sızıntısı — somut yüksek-riskli açık bulunmadı) not edilen 4 defense-in-depth maddesini ele alır.
> **İlke:** Mevcut çalışan davranışı (login, refresh, Platform paneli, reminder job) **bozmadan** savunma katmanı eklemek.

---

## 0. Özet karar tablosu

| # | Madde | Sınıf | Karar | Risk |
|---|---|---|---|---|
| A1 | EmailComposer `fullName` HTML encode | Gerçek (düşük-orta) hijyen | ✅ Bu sprint — küçük patch | Düşük |
| A2 | LogEmailSender token maskeleme + prod `Log` guard | Gerçek, koşullu token/PII sızıntısı | ✅ Bu sprint — küçük patch | Düşük (smoke testi güncelle) |
| B1 | `AuthorizationOptions.FallbackPolicy = RequireAuthenticatedUser` | Defense-in-depth (redundant) | ⚠️ Bu sprint — **koşullu**: tam `[AllowAnonymous]` matrisi şart | Orta (eksikse login/refresh kırılır) |
| B2 | Global EF tenant query filter (`ITenantScoped` → `StudioId == current`) | Defense-in-depth | ⛔ Bu sprint DEĞİL — ayrı tasarım+sprint | Yüksek (login/Platform/Reminder kırılır) |

---

## 1. Mevcut bulguların doğrulanması

| # | Bulgu | Somut exploit? |
|---|---|---|
| B2 | Global EF tenant query filter yok | Hayır — her handler'da `StudioId == studioId` zaten var, smoke testlerle kanıtlı |
| B1 | `FallbackPolicy` yok | Hayır — 38 controller'dan 36'sı zaten class-level `[Authorize]` taşıyor |
| A1 | EmailComposer `fullName` encode'suz | Kısmen — kullanıcı kayıtta `FullName`'e HTML koyabilir; mail kendi adresine gider → self-XSS/HTML-injection, dar etki |
| A2 | LogEmailSender ham token logluyor | Evet, koşullu — prod'da `SenderMode=Log` kalırsa ham token + e-posta loglanır |

---

## 2. A1 — EmailComposer HTML encode

**Dosya:** `src/Pliaforma.Infrastructure/Services/EmailComposer.cs` (`internal static`, 2 template).

- **Kullanıcı kaynaklı tek alan:** `fullName` ← `user.FullName` ← `Register.cs:73` (`request.FullName.Trim()`). Validation sadece `NotEmpty + MaxLength(150)`, HTML sanitizasyonu yok.
- **Encode gereken (sadece HTML body):**
  - `Verification` → **satır 18** (`<p>Merhaba {fullName},</p>`)
  - `PasswordReset` → **satır 31** (aynı kalıp)
- **Plain-text body (satır 14-16, 27-29):** encode EDİLMEYECEK (düz metinde `<` zararsız; encode yanlış render verir).
- **Yöntem:** `System.Text.Encodings.Web.HtmlEncoder.Default.Encode(...)` (ASP.NET Core'da hazır, ek paket yok). `href` içindeki `{link}` de attribute-encode edilecek (defense-in-depth).
- **Yan keşif (bu sprint kapsamı dışı, not):** `SmtpEmailSender` yalnızca `HtmlBody` gönderiyor (`SmtpEmailSender.cs:31`); üretilen `TextBody` hiç kullanılmıyor (multipart/alternative yok).

**Uygulama planı:**
1. `EmailComposer.cs`'e `using System.Text.Encodings.Web;`
2. Her iki HTML body'de `var nameHtml = HtmlEncoder.Default.Encode(fullName);` ve `var linkAttr = HtmlEncoder.Default.Encode(link);` kullan; interpolasyonları bunlarla değiştir.
3. `TextBody`'de `fullName` ham kalır.

**Test:** `Verification(settings, "<img src=x onerror=alert(1)>", "tok")` → `HtmlBody` `&lt;img...` içermeli, `TextBody` ham korumalı; Türkçe karakter (`Ayşe Yıldız`) regresyonu; `href` içinde `"`/`<` ile attribute bozulmadığı.

---

## 3. A2 — LogEmailSender token maskeleme

**Dosya:** `src/Pliaforma.Infrastructure/Services/LogEmailSender.cs`

- **Loglanan:** `msg.Link` — link ham token içerir (`...?token={Uri.EscapeDataString(rawToken)}`, `EmailComposer.cs:12/25`). Yani **ham verify/reset token + alıcı e-posta (PII)**, `Information` seviyesinde:
  - **satır 26** (EmailVerification), **satır 33** (PasswordReset).
- **SmtpEmailSender:** `ILogger` almıyor, link/token loglamıyor → temiz.
- **DI gerçeği:** Sender seçimi config'e bağlı (`Email:SenderMode`, `DependencyInjection.cs:177-188`). `appsettings.json` varsayılanı **`Log`**. `Disabled` için prod fail-fast guard var (`DependencyInjection.cs:170`); **`Log` için prod guard YOK** → prod'da yanlış config token sızdırır.
- **Dikkat:** `scripts/sprint16-smoke.sh:55` bu loglardan token çekiyor — değişiklik buna göre güncellenmeli.

**Uygulama planı:**
1. `LogEmailSender.cs:26/33` — tam `Link` yerine maskelenmiş çıktı: `token(masked)` (örn. ilk 4 char + uzunluk) + maskelenmiş e-posta; veya tam token gerekiyorsa seviye `Information` → `Debug`/`Trace`.
2. `DependencyInjection.cs` — `Disabled` guard'ının yanına: `isProduction && SenderMode==Log → throw InvalidOperationException`.
3. `scripts/sprint16-smoke.sh:55` token-çekme adımını yeni log formatına/seviyesine göre güncelle.

**Test:** `FakeLogger` ile çağır → ham token içermemeli; DI testi `isProduction + Log → throw`, `Smtp → SmtpEmailSender` resolve.

---

## 4. B1 — FallbackPolicy = RequireAuthenticatedUser

**Mevcut:** `FallbackPolicy`/`DefaultPolicy` yok (`DependencyInjection.cs:84-88`). 36/38 controller class-level `[Authorize...]` taşıyor. Risk yalnızca `AuthController` (class-level attribute yok) + minimal-API endpoint'lerde.

**FallbackPolicy eklenince 401'e düşecek (anonim KALMASI gereken) endpoint'ler — hepsine `[AllowAnonymous]` eklenecek:**

| Endpoint | Konum |
|---|---|
| `POST /api/auth/register-studio` | `AuthController.cs:30` |
| `POST /api/auth/register` | `:39` |
| `POST /api/auth/login` | `:49` |
| `POST /api/auth/refresh` | `:66` (cookie, JWT yok) ⚠ en yüksek risk |
| `POST /api/auth/logout` | `:77` (idempotent) |
| `POST /api/auth/verify-email` | `:96` |
| `POST /api/auth/forgot-password` | `:113` |
| `POST /api/auth/reset-password` | `:121` |
| `POST /api/auth/mfa/challenge/verify` | `:165` (challenge token) ⚠ |
| `POST /api/auth/mfa/recovery-code/verify` | `:173` (challenge token) ⚠ |
| `GET /metrics` (MapMetrics) | `Program.cs:122` → `.AllowAnonymous()` eklenecek (yoksa Prometheus scrape kırılır) |

**Zaten güvende:** `CspReportController` class-level `[AllowAnonymous]` (`:17`); `/health` `.AllowAnonymous()` (`Program.cs:135`); Swagger middleware (endpoint routing dışı, sadece Development).

**Uygulama planı:**
1. `DependencyInjection.cs:84-88` — `AddAuthorization` içine `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();`
2. Yukarıdaki 10 auth action'a tek tek `[AllowAnonymous]`.
3. `Program.cs:122` — `app.MapMetrics().AllowAnonymous();`
4. `MfaPendingGuardMiddleware` sırası bozulmamalı (authorization → mfa guard).

**Test (smoke):**
- Anon uçlar header'sız → 200/expected (401 DEĞİL): login, register, register-studio, forgot/reset-password, verify-email, refresh (sadece cookie), logout, mfa challenge/recovery, `/api/csp-report` 204, `/health` 200, `/metrics` 200.
- Header'sız korumalı uç (`GET /api/notifications`, `GET /api/studio`) → 401.
- Rol uçları: PlatformAdmin-dışı `GET /api/platform/overview` → 403; Student rolüyle owner-only uç → 403.
- `mfa_pending` token guard akışı bozulmamalı.
- Integration suite tam koşulmalı.

**Değer/maliyet:** Redundant (her controller zaten korumalı); değeri gelecekte attribute eklemeyi unutan yeni controller'ı otomatik korumak. Maliyet: tam `[AllowAnonymous]` matrisi + auth/metrics smoke.

---

## 5. B2 — Global tenant query filter (AYRI SPRINT — yalnızca spec)

**Marker zaten var:** `ITenantScoped { Guid StudioId }` (`Domain/Common/ITenantScoped.cs:8`), 28 entity uyguluyor. Filter `ITenantScoped`'e göre uygulanmalı, **StudioId varlığına göre değil** — çünkü `PlatformSubscription`, `PlatformSubscriptionPayment`, `SecurityAuditLog` StudioId taşır ama bilinçli olarak marker'ı uygulamaz (platform/append-only).

**Kıracağı akışlar:**
1. **Auth/onboarding:** `StudioMembership` `ITenantScoped`; login/switch/refresh/MFA bunu studio bağlamı set edilmeden `UserId` ile sorgular (`Login.cs:100`, `SwitchStudio.cs:39`, `RefreshToken.cs:60`, `AuthSessionService.cs:108-120`, `MfaCommands.cs:23`). `StudioId==null` → boş → **login bozulur**.
2. **Platform paneli:** `Features/Platform/*` (9+ handler) cross-tenant okur, hiçbirinde `IgnoreQueryFilters()` yok → geniş ekleme gerekir.
3. **Background worker:** `ReminderBackgroundService` HTTP-dışı scope'ta (`HttpContext==null`) tenant entity tarar → sessizce **hiç reminder üretmez** (en tehlikeli: sessiz regresyon).
4. **Soft-delete ezme tuzağı:** EF'te entity başına **son** `HasQueryFilter` öncekini ezer. Mevcut `!IsDeleted` (`AppDbContext.cs:52-74`) yanına naif ikinci filter → soft-delete iptal, silinmiş kayıtlar geri görünür. Doğrusu: tek combined `e => !e.IsDeleted && e.StudioId == _sid` + marker ayrımı → reflection kurulumunun yeniden yazımı.
5. **Lifetime:** `AppDbContext` + `CurrentUserService` ikisi de Scoped → inject güvenli; null-StudioId davranışı (boş dön / throw / bypass) + model-cache etkisi tasarlanmalı.

**Not:** "global filter YOK" zaten bilinçli, dokümante mimari karar (`docs/multi-tenancy.md:12-18`). Drive-by tersine çevrilmemeli.

**Ayrı-sprint kapsamı (öneri):**
1. `ITenantScoped`-bazlı combined `!IsDeleted && StudioId==current` filter + marker ayrımı.
2. Tenant context'in DbContext'e taşınması + null-studio davranış kararı.
3. Tüm Platform handler'larına + worker scope'larına `IgnoreQueryFilters()`/bypass.
4. Auth/onboarding akışlarının bypass'ı.
5. Cross-tenant + soft-delete regresyon smoke testleri.

---

## 6. Validation planı (A1+A2+B1)

- `dotnet build` 0 warn / 0 error.
- (Frontend dokunulmuyor; yine de) `tsc` 0 / `next build` 0 — değişiklik backend-only, beklenen etki yok.
- **Auth smoke:** login · register-studio · forgot-password · reset-password · refresh-token · MFA challenge flow.
- **Authorization smoke:** anon uçlar çalışıyor mu · authenticated uçlar auth'suz 401 mi · role-restricted uçlar 403 mü.
- **Tenant isolation regression:** cross-tenant erişim reddediliyor mu · PlatformAdmin bozulmadı mı.
- **Reminder/background job smoke** (B1 backend-only olduğundan etkilenmemeli; doğrula).
- **Payment/contract/schedule kısa regresyon.**

---

## 7. Kısıtlar

- Bu sprintte: A1 + A2 + B1 kod değişikliği. Migration YOK (hiçbir madde şema değiştirmiyor).
- Commit/push/tag yalnızca kullanıcı onayıyla.
- B2 uygulanmayacak — yukarıdaki spec gelecekteki ayrı sprint için referans.

# Sprint 31B-2 — Auth/App Hardening (migration var)

Statü: **UYGULANIYOR** · Önceki: 31B-1 (`v1.13.0-pilot`, `7371fb7`). Tag önerisi (yeşil): **`v1.14.0-pilot`**.
Tek sprint (parçalar birbirine bağlı: lockout→audit yazar; change-password→policy kullanır).

## Amaç
Pilot öncesi uygulama seviyesi kimlik/hesap güvenliği + denetim (audit) + CSP temeli.

## Kapsam ve kararlar

### A. Parola politikası
- Shared `PasswordRules` + FluentValidation `RuleForPassword()`. **min 10**, büyük+küçük+rakam+özel karakter, max 128, **yaygın parola reddi**.
- Yaygın liste: `Resources/common-passwords.txt` (embedded, ~top-list), case-insensitive, trim/normalize.
- Register / RegisterStudio / PasswordReset / ChangePassword **aynı kuraldan**. Mevcut kullanıcı **login'de zorlanmaz**.
- Hata mesajı Türkçe.

### B. Change-password
- `POST /api/auth/change-password` `[Authorize]`. `ChangePasswordCommand(CurrentPassword, NewPassword)`.
- Mevcut parola doğrulanır → yeni parola policy'den geçer → hash → **tüm refresh revoke** → **lockout reset** → **audit (PasswordChanged)**.
- Frontend: `my-profile`'a parola değiştirme formu (mevcut/yeni/yeni-tekrar; başarıda "oturumlar yenilendi, tekrar giriş gerekebilir" bildirimi; client-side temel doğrulama; asıl doğrulama backend).

### C. Hesap kilitleme
- `AuthSettings`: `MaxFailedAttempts=5`, `LockoutMinutes=15` (configurable, default 5/15).
- Login: user yok → generic **401**; `LockoutEnd>now` → **423** (generic mesaj: "Giriş geçici olarak kilitlendi. Lütfen bir süre sonra tekrar deneyin."); parola yanlış → `FailedLoginCount++`, eşikte `LockoutEnd=now+15dk`; başarı → `FailedLoginCount=0`, `LockoutEnd=null`. IsActive mantığı korunur; IP rate-limit devam.
- `LockedException` + `ExceptionHandlingMiddleware` 423 eşlemesi.
- Reset/Change sonrası lockout reset (sayaç=0, LockoutEnd=null).
- Admin unlock: **31C** (bu sprintte yok; 15dk auto-unlock).

### D. SecurityAuditLog (append-only, BaseEntity DEĞİL)
Alanlar: `Id, OccurredAt(UTC), EventType, Outcome, ActorUserId?, ActorEmailMasked?, StudioId?, TargetType?, TargetId?, Ip?, UserAgent?(≤400), Detail?(kısa/maskeli)`.
Index: `OccurredAt`, `(ActorUserId, OccurredAt)`, `(StudioId, OccurredAt)`, `EventType`.
- BaseEntity yok → soft-delete filtresi + AuditInterceptor otomatik muaf.
- App **yalnız Add**; update/delete handler yok; soft-delete yok; **audit'e audit yazılmaz**. DB-level REVOKE/trigger → **31D notu**.
- PII: `PiiMask.Email` → `j***@e***.com`. Parola/token/secret/request-body **asla**. IP+UserAgent güvenlik denetimi (KVKK meşru menfaat), retention **2 yıl**.
- Yazıcı: `ISecurityAuditLogger` (Application) + `SecurityAuditLogger` (Infrastructure; `IHttpContextAccessor` ile Ip/UserAgent zenginleştirir). Success-path best-effort (catch+log; user akışını kırmaz); failed-login/lockout audit sayaç ile birlikte kalıcı.
- Eventler: LoginSucceeded, LoginFailed, AccountLockedOut, Logout, PasswordResetRequested, PasswordResetCompleted, PasswordChanged.
- 31C'ye: role change, payment, contract, student export, HealthNotes access, audit viewer.

### E. CSP Report-Only
- `deploy/nginx.conf` 443 bloğuna `Content-Security-Policy-Report-Only` (enforce YOK, report endpoint YOK).
- Policy: `default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-src 'self'; object-src 'none'; base-uri 'self'`.
- Next.js inline tema scripti report-only'de bloklanmaz (yalnız konsol raporu) → enforce + nonce 31C.

### F. Migration
**Tek:** `Sprint31B2_AuthHardeningAndAuditLog` — `User.FailedLoginCount (int NOT NULL DEFAULT 0)`, `User.LockoutEnd (timestamptz NULL)`, `SecurityAuditLogs` tablosu. Additive, geriye uyumlu (mevcut kullanıcılar default alır). Rollback = Down (kolon+tablo düşür). Prod-apply öncesi **backup** (runbook). Design-time: `AppDbContextFactory` (IDesignTimeDbContextFactory) eklenir (yalnız migration tooling; runtime'da kullanılmaz).

### G. Test/Smoke
Yeni: `password-policy-smoke.sh`, `lockout-smoke.sh`, `change-password-smoke.sh`, `security-audit-log-smoke.sh`, `csp-smoke.sh`.
**Test verisi sweep:** tüm smoke/seed/e2e parolaları politika-uyumlu (`Parola123!` — common listede değil). Eski `Parola123`/`Admin!2345` gözden geçirilir; gerçek prod secret'la karıştırılmaz.

## Kapsam dışı
MFA, audit viewer, role/payment/contract/student/HealthNotes audit, tenant isolation expansion, backup encryption, CI scan, token purge, anonymization, CSP enforce, ürün feature.

## Doğrulama
backend build 0/0 · tsc 0 · next build 0 · migration apply (staging compose) · `has-pending` temiz · yeni smoke'lar yeşil · mevcut auth/security-audit/metrics-exposure smoke + e2e yeşil · v1.13 regresyon yok.

## Açık kalan riskler
- Mevcut test parolaları sweep edilmezse register-içeren smoke'lar kırılır.
- Lockout-DoS (kabul: kısa kilit + rate limit). Enumeration (kabul: generic mesaj).
- CSP report-only → kırılma yok; enforce 31C.

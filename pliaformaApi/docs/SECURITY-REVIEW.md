# Güvenlik İncelemesi (MVP / Pilot)

Sprint 12 release-hardening kapsamında yapılan güvenlik denetiminin özeti. Her madde **kod incelemesi** ve/veya
**smoke** ile doğrulanmıştır. Otomatik özet: `scripts/security-audit-smoke.sh` (21 kontrol, ✅).

## 1. Kimlik & Token
| Konu | Durum | Not |
|---|---|---|
| **JWT secret fail-fast** | ✅ | `Infrastructure/DependencyInjection`: `Jwt:Secret` yok veya <32 bayt ise uygulama AÇILMAZ. appsettings'te düz metin YOK (user-secrets/env). |
| **Access token saklama** | ✅ | Frontend'de **bellekte** (`tokenStore`), localStorage'a YAZILMAZ → XSS'te token sızmaz. Kısa ömür (15 dk). |
| **Refresh token** | ✅ | DB'de yalnızca **SHA-256 hash** (düz metin yok), rotasyonlu, logout'ta revoke. Refresh/switch'te User+membership Active re-check. |
| **Refresh cookie** | ✅ | `pf_refresh`: **HttpOnly**, **SameSite=Strict**, **Secure=prod'da true** (`!IsDevelopment()`), **Path=/api/auth** (yalnız auth yoluna gider). |
| **Algoritma** | ✅ | HMAC-SHA256; issuer/audience/lifetime/signing key doğrulanır; ClockSkew 1 dk. |

> **Pilot şartı:** Secure cookie HTTPS ister → pilotta TLS açılmalı (nginx 443). HTTP üzerinde Secure cookie taşınmaz.

## 2. Yetkilendirme & Tenant izolasyonu
| Konu | Durum | Kanıt |
|---|---|---|
| **Tenant izolasyonu** | ✅ | Handler seviyesinde `Id == x && StudioId == currentUser.StudioId` → cross-tenant **404**. Token claim'ine kör güvenilmez. |
| **Body'den StudioId'ye güven YOK** | ✅ | Hiçbir handler request body'sinden StudioId almaz; daima `ICurrentUserService.StudioId` (JWT claim) kullanılır. |
| **MembershipGuard** | ✅ | Yazma isteklerinde üyeliğin DB'de hâlâ Active olduğu doğrulanır (token süresi dolmadan çıkarılma korunur). |
| **PlatformAdmin policy** | ✅ | `/api/platform/*` → `[Authorize(Policy="PlatformAdmin")]` (JWT `global_role` claim) + handler `EnsurePlatformAdmin` (2. katman). |
| **PlatformAdmin bypass YOK** | ✅ | PlatformAdmin'in studio_id/studio_role claim'i yoksa tenant uçları **403** (smoke). Cross-tenant veri yalnız `/api/platform/*`. |
| **Owner platform uçları** | ✅ | Owner/StudioAdmin/Instructor/Student `/api/platform/*` → **403**. |
| **Student kapsamı** | ✅ | Student yalnız kendi verisi (`/api/student/*`); staff uçları → 403; başka öğrenci dashboard → 403. |
| **Instructor finans gizliliği** | ✅ | Instructor payment/package yönetim uçlarına 403; yoklamada paket bakiyesi gösterilmez; başka hocanın seansı → 403. |
| **Suspended studio** | ✅ | `Status != Active` → yazma **403** (StudioStatusBehavior); okuma 200; veri silinmez; worker reminder üretmez. |
| **Notification cross-user** | ✅ | Başka kullanıcının bildirimini okuma → **404** (RecipientUserId==self && StudioId==current). |
| **Cross-tenant finans** | ✅ | By-id payment/booking/student → 404; list uçları tenant-filtreli → boş 200 (veri sızmaz). |

Tümü `security-audit-smoke.sh` + sprint smoke'larıyla kanıtlı.

## 3. Hata yönetimi & bilgi sızıntısı
- **Stack trace sızıntısı YOK** ✅: `ExceptionHandlingMiddleware` işlenmeyen istisnalarda gövdeye sabit
  `"Beklenmeyen bir hata oluştu."` (HTTP 500) yazar; stack trace **yalnızca sunucu logunda**. Dev/prod aynı
  davranır (developer exception page kullanılmaz). Bilinen istisnalar (Validation/NotFound/Forbidden/Conflict)
  uygulama-kontrollü, güvenli mesajlar döner.
- **Üretim logu** ✅: prod'da JSON console log; 500'ler `LogError` ile loglanır.

## 4. Ağ / taşıma
- **Same-origin** ✅: frontend relative `/api`; prod'da nginx tek giriş (`/api`→backend, `/health`→backend,
  `/`→frontend + ws upgrade). CORS ana akış için gerekmez; yine de `PUBLIC_ORIGIN` allowlist'e yazılır
  (`AllowCredentials` cookie için).
- **Rate limit** ✅: auth uçlarında IP bazlı; **prod 10/dk** (default), **dev 1000/dk** (smoke için gevşek).
  Gerçek istemci IP'si `ForwardedHeaders` + nginx `X-Forwarded-For` ile.
- **nginx güvenlik başlıkları** (Sprint 12 eklendi) ✅: `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`,
  `Permissions-Policy: geolocation=(),microphone=(),camera=()`, `server_tokens off`, `client_max_body_size 10m`.

## 5. Repo / secret hijyeni
- **Gerçek secret YOK** ✅: kaynak/konfig taramasında yalnızca placeholder (`CHANGE_ME_*`) ve yerel-dev
  `postgres/postgres` (env-override edilen, atılabilir DB) bulundu. Gerçek JWT/DB/provider key/API key YOK.
- **`.gitignore`** ✅: backend (`.env`, `*.env`, `bin/obj`, `appsettings.*.local.json`), frontend
  (`.env*`, `node_modules`, `.next`, Playwright artifacts), **kök** (Sprint 12 eklendi: `deploy/.env`,
  `*.pem/*.key/*.crt`, `*.sql/*.sql.gz`, `ops/backups/`).
- **`.env.example`** ✅: sadece örnek değerler; gerçek secret yok.

## 6. Bilinçli olarak yapılmayanlar / kalan riskler
- **Content-Security-Policy (CSP)** eklenmedi — Next.js inline tema scripti nonce/hash gerektirir; yanlış CSP
  uygulamayı bozar. Pilot sonrası nonce ile eklenmeli (orta öncelik).
- **TLS pilot-öncesi açılmalı** (nginx 443 taslak halinde) — Secure cookie + şifreli taşıma için ZORUNLU.
- **Backend HTTP healthcheck yok** — `aspnet` imajında curl/wget yok; eklemek Dockerfile değişikliği ister.
  Şu an `restart: unless-stopped` + nginx retry'a güveniliyor (kabul edilebilir; pilot sonrası eklenebilir).
- **2FA / hesap kilitleme / parola politikası zorlama** yok (MVP kapsamı dışı).
- **Worker tek-instance kilidi yok** — çok-instance'ta nadir dedup yarışı (filtered index korur).
- **Audit log / SIEM** yok; gözlem `/platform/health` + JSON log ile.
- Otomatik bağımlılık güvenlik taraması (Dependabot/`npm audit` gate) CI'da değil (CI yok). `npm audit` 2 orta
  uyarı bildirir (dev bağımlılığı; runtime'a girmez).

## Doğrulama
`scripts/security-audit-smoke.sh` (21 kontrol ✅) + 12 sprint smoke + E2E. Detay: [TESTING.md](TESTING.md).

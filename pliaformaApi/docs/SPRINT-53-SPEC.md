# Sprint 53 — Mobil Auth Temeli (Mobile Auth Foundation)

Durum: ONAYA SUNULDU — gatekeeper onayı bekliyor. Commit/push/tag YOK; onay sonrası Builder implementasyonu + validation raporu.

> AGENTS.md §6.3 "Do not introduce cross-origin auth without explicit design review" — bu spec o tasarım
> incelemesinin kendisidir. Web'in same-origin/cookie modeli **bit değişmeden korunur**; mobil için
> **paralel, kanal-bağlı (channel-bound), additive** bir taşıma katmanı eklenir.

## 1. Amaç

Pliaforma'nın native mobil uygulaması (yönetici-önce; App Store + Play Store, Expo/React Native — ayrı sprint)
için backend'i hazırlamak. Mobilde tarayıcı yok → httpOnly cookie / SameSite / origin kavramları çalışmaz.
Bu sprint YALNIZ backend'de:

1. **Mobil auth taşıması:** refresh + trusted-device token'ları mobil istemciye **response body** ile verilir,
   istemci bunları OS güvenli deposunda (iOS Keychain / Android Keystore) saklar.
2. **Refresh token reuse (hırsızlık) tespiti:** pilot denetiminde de not edilen mevcut eksik kapatılır —
   `ReplacedByTokenHash` doldurulur, iptal edilmiş token tekrar sunulursa oturum ailesi topluca iptal edilir
   ve güvenlik denetim kaydı düşülür. (Web dahil TÜM kanallar için geçerli; uzun ömürlü mobil oturumda şart.)

Mobil uygulamanın kendisi, cihaz kaydı/push, deep link ve sürüm kapısı bu sprintte YOKTUR (bkz. §10 yol haritası).

## 2. Mevcut mimari (özet, dosya referanslı)

- Refresh token üretimi/rotasyonu taşıma-bağımsızdır: `AuthSessionService.IssueAsync` ham token'ı `AuthResult`
  içinde döner (`Features/Auth/AuthSessionService.cs:81-95`); cookie kararı YALNIZ controller'dadır
  (`AuthController.AuthOk`, `Api/Controllers/AuthController.cs:231-237` → `RefreshCookie.Write`).
- `RefreshTokenCommand(string RefreshToken, Guid? StudioId, string? Ip)` token'ı parametre alır
  (`Features/Auth/RefreshToken.cs`) — cookie'den mi body'den mi geldiğini bilmez. Rotasyonda yalnız
  `stored.RevokedAt` set edilir (`RefreshToken.cs:74`); `ReplacedByTokenHash` entity'de tanımlı
  (`Domain/Entities/RefreshToken.cs:21`) ama HİÇ doldurulmaz; reuse tespiti yoktur.
- Trusted MFA device: ham opak token yalnız cookie ile taşınır (`Api/Auth/TrustedDeviceCookie.cs`); DB'de
  yalnız SHA-256 hash (`TrustedMfaDeviceService`, `UserTrustedMfaDevice`). Login cookie'den okur
  (`AuthController.cs:68`), challenge/verify cookie yazar (`AuthController.cs:198-199`).
- Token ömürleri: access 15 dk, refresh 7 gün (`Infrastructure/Settings/JwtSettings.cs`).
- `MfaPendingGuardMiddleware`: `mfa_pending` token yalnız `/api/auth/me`, `/logout`, `/mfa/setup/start`,
  `/mfa/setup/verify` yollarına erişir; diğer her yol 403.
- `SecurityAuditEventType` string saklanır → yeni değer **migration gerektirmez**
  (`Domain/Enums/SecurityAuditEnums.cs:6`). Son değer: `BookingMoved = 31`.
- API çağrıları `Authorization: Bearer` header'ı ile çalışır → mobil, mevcut tüm iş endpointlerini
  (dashboard, sessions, attendance, payments, makeup-requests...) DEĞİŞİKLİKSİZ tüketebilir. CORS native
  istemciyi etkilemez (origin yok); nginx/CORS değişikliği gerekmez.

## 3. Onaya sunulan kararlar

1. **Ayrı mobil controller** — `MobileAuthController`, route `api/auth/mobile`. Header ile mod değiştiren tek
   endpoint DEĞİL. Gerekçe (güvenlik): aynı endpoint cookie-girdi + body-çıktı yapabilseydi, web'deki bir XSS
   `X-Client: mobile` benzeri header'la refresh cookie'sini body'ye "exfiltrate" edebilirdi. Ayrı uçlar +
   kanal bağlama bunu yapısal olarak engeller. İş mantığı ÇOĞALTILMAZ: aynı command/handler'lar kullanılır,
   yalnız taşıma (body ↔ cookie) farklıdır.
2. **Kanal bağlama (channel binding)** — `RefreshToken.Client : AuthClient { Web=0, Mobile=1 }` kolonu
   (string saklanır, default `Web`; **additive migration**). Web ucu yalnız `Web`, mobil ucu yalnız `Mobile`
   token'ı kabul eder; uyumsuz kanal = geçersiz token (401). Cookie'deki token mobil uçta ASLA çalışmaz ve
   tersi.
3. **Mobil refresh ömrü** — `Jwt:MobileRefreshTokenDays`, default **45** (web 7 günde kalır). Rotasyonludur;
   yöneticiyi her hafta parolaya döndürmemek için. Env: `Jwt__MobileRefreshTokenDays` (opsiyonel).
4. **Reuse tespiti tüm kanallarda** — rotasyonda `ReplacedByTokenHash` doldurulur. İptal edilmiş token tekrar
   sunulursa: `RevokedAt` üzerinden **60 sn grace** içindeyse yarış sayılır (yalnız 401; web'de iki sekmenin
   eşzamanlı refresh'i meşru yarıştır), grace dışındaysa **kullanıcının TÜM aktif refresh token'ları iptal**
   + `SecurityAuditEventType.RefreshTokenReuseDetected = 32` (Failure) + 401. Süresi dolmuş (revoke edilmemiş)
   token reuse SAYILMAZ (bugünkü gibi düz 401).
5. **Trusted device mobilde body ile** — mobil `mfa/challenge/verify` `rememberDevice=true` ise ham trusted
   token + son kullanma **body'de** döner; mobil login istek body'sinde `trustedDeviceToken` kabul eder.
   AGENTS.md §10.1 değişmez: DB'de yalnız hash; süre 30 gün (`Mfa:TrustedDeviceDays`); parola değişikliği/
   sıfırlama/MFA reset'te toplu iptal; **recovery-code trusted device YAZMAZ**; web cookie kuralları aynen.
6. **Kapsam dışı uçlar** — mobilde register/register-studio YOK (pilot kilidi; yönetici hesapları zaten var).
   `switch-studio`, `me`, `change-password`, `mfa/setup/start` bearer-tabanlı ve taşıma-bağımsız →
   mobil bunları MEVCUT uçlardan kullanır, kopya üretilmez.

## 4. Backend tasarımı

### 4.1 Şema / migration (additive, tek kolon)
- `RefreshToken.Client : AuthClient` (yeni enum `Domain/Enums/AuthClient.cs`), config'de
  `HasConversion<string>() + HasMaxLength(16) + HasDefaultValue(AuthClient.Web)`.
- Migration: `Sprint53_RefreshTokenClient` — yalnız `ADD COLUMN "Client" text NOT NULL DEFAULT 'Web'`.
  DROP/veri taşıma yok; mevcut satırlar `Web` olur (doğru: hepsi cookie ile verildi).

### 4.2 Uygulama katmanı (taşıma-bağımsız kalır)
- `ITokenService.GenerateRefreshToken(AuthClient client = AuthClient.Web)` — ömür: `Web → RefreshTokenDays`,
  `Mobile → MobileRefreshTokenDays` (`JwtSettings`'e int alan, default 45).
- `AuthSessionService.IssueAsync(..., AuthClient client = Web)` → üretilen `RefreshToken` satırına `Client`
  yazar. `IssueWithMfaAsync`/`IssuePostAuthAsync` client'ı geçirir.
- Command'lara opsiyonel `AuthClient Client = AuthClient.Web` parametresi: `LoginCommand`,
  `RefreshTokenCommand`, `MfaChallengeVerifyCommand`, `MfaRecoveryCodeVerifyCommand`, `MfaSetupVerifyCommand`,
  `LogoutCommand`. Mevcut çağrı yerleri (web controller) DEĞİŞMEZ (default Web).
- `RefreshTokenHandler`:
  - Kanal doğrulaması: `stored.Client != request.Client` → `UnauthorizedException` (genel mesaj; kanal bilgisi
    sızdırılmaz).
  - Rotasyon: `stored.RevokedAt = now` **ve** `stored.ReplacedByTokenHash = <yeni token hash>`.
  - Reuse dalı (karar 4): revoked token sunumu → grace kontrolü → gerektiğinde
    `AuthSessionService.RevokeAllRefreshTokensAsync` + `ISecurityAuditLogger.LogAsync(RefreshTokenReuseDetected,
    Failure, actorUserId: stored.UserId, detail: kanal + maskeli hash öneki)` → 401. Audit çağrısı mevcut
    ayrı-bağlantı sink ile transaction rollback'inden etkilenmez (`Persistence/Audit/EfSecurityAuditSink.cs`).
- `MfaChallengeVerifyHandler`/`TrustedMfaDeviceService`: davranış aynı; ham trusted token zaten handler
  sonucunda taşınıyor (`MfaCommands.cs:182`) — mobil controller bunu cookie yerine body'ye koyar.

### 4.3 API katmanı — `MobileAuthController` (route `api/auth/mobile`, hepsi `[EnableRateLimiting("auth")]`)
| Uç | Girdi (body) | Çıktı (body) |
|---|---|---|
| `POST login` (anon) | `email, password, trustedDeviceToken?` | `MobileAuthResponse` VEYA `MfaChallengeResponse` |
| `POST refresh` (anon) | `refreshToken, studioId?` | `MobileAuthResponse` (yeni refresh dahil) |
| `POST logout` (anon, idempotent) | `refreshToken?` | ok (token revoke; cookie işlemi YOK) |
| `POST mfa/challenge/verify` (anon) | `challengeToken, code, rememberDevice?` | `MobileAuthResponse` (+`trustedDeviceToken`, `trustedDeviceExpiresAt` yalnız rememberDevice=true ise) |
| `POST mfa/recovery-code/verify` (anon) | `challengeToken, recoveryCode` | `MobileAuthResponse` (trusted device ASLA yok) |
| `POST mfa/setup/verify` (bearer, mfa_pending) | `code` | `MobileMfaSetupCompleteResponse` (auth + recoveryCodes) |
- Yeni DTO'lar `Api/Auth/MobileAuthContracts.cs`: `MobileAuthResponse` = `AuthResponse` alanları +
  `RefreshToken`, `RefreshTokenExpiresAt` (+ nullable trusted device alanları). Web `AuthResponse`
  DEĞİŞMEZ (refresh body'ye girmez).
- `MfaPendingGuardMiddleware` allowlist'ine mobil karşılıklar eklenir: `/api/auth/mobile/logout`,
  `/api/auth/mobile/mfa/setup/verify` (setup/start mevcut ortak uçtan kullanılır).

### 4.4 Web tarafı
- `AuthController` davranışı bit değişmez (yalnız command'lardaki default-Web parametresi derlenir).
- Web refresh artık kanal doğrulamasından geçer (`Client=Web`) ve reuse tespiti web oturumlarını da korur.

## 5. Frontend etkisi

YOK. Web `pliaformaFrontend` hiçbir değişiklik almaz; cookie akışı aynen. (Mobil istemci Sprint 54.)

## 6. Güvenlik analizi (AGENTS.md uyumu)

- §6.3/§25: origin AYRILMAZ — mobil de `https://pliaforma.com/api/...` çağırır; nginx/CORS/cookie değişmez.
- §10 baseline: MFA zorunluluğu, trusted-device kuralları (§10.1), registration lock, tenant izolasyonu,
  SameSite=Strict modeli — hiçbiri gevşetilmez. Mobil uçlar aynı `auth` rate-limit politikasındadır.
- Yeni yüzeyin riski: ham refresh/trusted token'ın body'de dönmesi. Karşılıklar: (a) yalnız `api/auth/mobile/*`
  uçlarında, (b) kanal bağlama — web cookie token'ı mobil uçta geçersiz (XSS-exfil kapalı), (c) istemci
  tarafında OS güvenli deposu zorunluluğu (Sprint 54 spec + pliaformaMobile/AGENTS.md kuralı), (d) reuse
  tespiti + aile iptali + audit. Net etki: web modeli aynı kalırken çalınan-token senaryosuna karşı tespit
  İLK KEZ eklenir (mevcut duruma göre sıkılaşma).
- PII: audit detayında ham token/hash TAM değeri yazılmaz (maskeli önek); e-posta mevcut `PiiMask` ile.

## 7. Riskler

- İki sekmeli web yarışının reuse sanılması → 60 sn grace (karar 4). Grace, gerçek saldırıyı en fazla 60 sn
  geciktirir; aile iptali yine çalışır.
- `ITokenService` imza değişikliği → tek implementasyon + çağrı yerleri derleyiciyle yakalanır
  (`TreatWarningsAsErrors`).
- Migration default'u eski satırları `Web` işaretler → yanlış sınıflama imkânsız (mobil uç henüz yok).
- Mobil refresh 45 gün → çalınırsa pencere uzun; karşılık: reuse tespiti + secure-store + istenirse
  `Jwt__MobileRefreshTokenDays` ile kısaltma. Rollback: mobil uçlar kullanılmazsa davranış tamamen eski hali.

## 8. Test planı

- `scripts/sprint53-smoke.sh`: mobil login → body'de refresh döner, cookie DÖNMEZ; mobil refresh rotasyonu;
  web cookie token'ı mobil uçta 401 / mobil token web uçta 401 (kanal bağlama); revoked mobil token tekrar
  sunulunca (grace sonrası) tüm oturumlar iptal + audit satırı `RefreshTokenReuseDetected`; grace içinde çift
  refresh → aile iptal EDİLMEZ; MFA'lı yönetici: challenge → code → rememberDevice=true → trusted token body'de,
  ikinci login'de challenge atlanır; recovery-code yolu trusted device DÖNDÜRMEZ; mfa_pending mobil token yalnız
  allowlist uçlarına erişir; logout sonrası refresh 401. Web regresyonu: `auth-smoke.sh` + `mfa-login-smoke.sh`
  aynen geçer.
- Validation: `dotnet build -c Release` 0 uyarı; `dotnet ef migrations has-pending-model-changes` clean;
  migration raporu (ad/additive/kolon/rollback) Builder raporunda.

## 9. Kapsam dışı

Mobil uygulamanın kendisi (Expo scaffold, `pliaformaMobile/`); cihaz kaydı + push kanalı; deep link
(universal/app links); minimum sürüm kapısı; OpenAPI'den tip üretimi; mobilde register/register-studio;
web frontend değişikliği; nginx/CORS değişikliği; rate-limit hesap-boyutu genişletmesi; Notification enum
değişiklikleri.

## 10. Yol haritası (bilgi amaçlı; her biri ayrı spec + onay)

- **Sprint 54 — Mobil iskelet:** `pliaformaMobile/` (Expo + TypeScript + EAS), kendi `AGENTS.md`'si
  (secure-store zorunluluğu, bundle'a secret gömme yasağı, store gönderimi = deploy onayı, `mobile-vX.Y.Z`
  tag öneki), yönetici login + MFA + trusted device + stüdyo seçimi + salt-okunur dashboard.
- **Sprint 55 — Cihaz kaydı + push:** `UserDevice` tablosu, `NotificationChannel.Push`,
  `INotificationChannelSender` push implementasyonu (FCM/APNs; sağlayıcı soyutlanır), bildirim tercihleri.
- **Sprint 56 — Deep link + sürüm kapısı:** universal/app links (AASA + assetlinks), min. desteklenen app
  sürümü ucu + zorunlu güncelleme ekranı.

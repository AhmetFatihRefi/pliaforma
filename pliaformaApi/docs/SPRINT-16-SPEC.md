# Sprint 16 — Email Verification & Password Reset

> **Durum:** ✅ UYGULANDI. Tüm kod/migration/test yazıldı ve doğrulandı.
> sprint16-smoke 27✓ · 17/17 smoke · 38/38 E2E (2x) · same-origin · SMTP fail-fast · backend 0/0.
> **Onayda eklenen 2 karar:** (#6) reset-password başarılı olunca `EmailVerifiedAt` null ise verified yapılır;
> (#3) prod'da `SenderMode=Disabled` (override yoksa) **fail-fast**. Detay: [sprints.md](sprints.md#sprint-16--tamamlanan-kapsam).

---

## 0. Ana Hedef

Açık kayıt (Sprint 15) geldiği için kullanıcıların **e-posta adresini doğrulaması** ve **şifresini güvenli
şekilde sıfırlaması**. Bu **gateway/ödeme sprinti değil** — yalnız auth güvenliği + hesap kurtarma. E-posta
gönderimi **pluggable** (`Log`/`Smtp`/`Disabled`); tokenlar DB'de **düz metin değil, hash'li** tutulur,
**tek kullanımlık** ve süreli. Mevcut Sprint 0-15 kullanıcıları **backfill ile verified** sayılır → kimse
kilitlenmez.

---

## 1. Mevcut Durum (kanıtlanmış kod gerçekleri — referans)

| Alan | Gerçek |
|---|---|
| `User` (`Domain/Entities/User.cs`) | `Email, NormalizedEmail, PasswordHash, FullName, Phone, GlobalRole, IsActive, LastLoginAt` + nav `RefreshTokens`. **Email-doğrulama alanı YOK.** |
| `RefreshToken` | `UserId, TokenHash(SHA-256 Base64), ExpiresAt, RevokedAt?, ReplacedByTokenHash?, CreatedByIp?`; `IsActive => RevokedAt==null && now<ExpiresAt`. Rotasyon var; **toplu-revoke YOK** (yalnız logout tek-token). |
| `AuthSessionService` | `IssueAsync` refresh üretir+hash'ler+saklar; `IssueAccessOnlyAsync`. Revoke metodu yok. |
| `Logout` | tek refresh'i `RevokedAt=now` ile iptal (idempotent). |
| `ITokenService` | refresh token üret/hash'le (SHA-256). Generic token üretimi için yeniden kullanılabilir. |
| `IPasswordHasher` | arayüz Application, impl Infrastructure (BCrypt). **IEmailSender bu deseni izleyecek.** |
| `Infrastructure/DependencyInjection` | `IConfiguration.GetSection().Get<T>()` + **fail-fast** (Jwt:Secret <32B → app açılmaz). Email config buraya eklenecek. |
| `AuthController` | `register-studio, register, login, switch-studio, refresh, logout, me` — auth uçları `[EnableRateLimiting("auth")]`. |
| Rate limit | `RateLimitPolicies.Auth` = IP başına fixed-window (prod 10/dk, dev 1000). |
| `IDateTime` | `UtcNow`. `IAppDbContext` = DbSet soyutlaması (yeni `AuthTokens` DbSet'i buraya). |
| Public origin | Email link tabanı için **ayrı config YOK** (yalnız `Cors:Origins`). `Email:PublicOrigin` eklenecek. |
| `.env.example` | `ConnectionStrings__`, `Jwt__`, `Seed__`, `Cors__` placeholder deseni. SMTP buraya eklenecek. |

---

## 2. Locked Decisions (kullanıcı onayı)

1. **Ayrı token tablosu** (`AuthToken`) — User'a token gömülmez. User'a yalnız `EmailVerifiedAt?` eklenir.
2. **Token DB'de hash'li** (düz metin asla); kullanıcıya giden ham token random (CSPRNG); **tek kullanımlık**
   (`ConsumedAt`); süreli (`ExpiresAt`); yeni token üretilince aynı purpose'un eski kullanılmamış tokenları iptal.
3. **Backfill:** mevcut tüm kullanıcılar `EmailVerifiedAt=now` → eski akışlar bozulmaz.
4. **Email sender pluggable:** `IEmailSender` + `Log`/`Smtp`/`Disabled` mod; prod'da verification aktifse
   `PUBLIC_ORIGIN` zorunlu; SMTP eksikse güvenli hata veya açıkça `Disabled`.
5. **Join-studio gating:** kullanıcı **login/logout/resend** yapabilir ama **email doğrulanmadan stüdyoya katılım
   isteği gönderemez** (apply → 403). Banner gösterilir.
6. **register-studio bozulmaz:** stüdyo oluşur, owner `EmailVerifiedAt=null` ise dashboard'da **uyarı** (zorunlu
   doğrulama bu sprintte değil — riskleriyle §6).
7. **reset-password sonrası** kullanıcının **tüm refresh tokenları revoke edilir** (toplu-revoke eklenir).
8. **forgot-password generic 200** (kullanıcı var/yok sızmaz); resend/verify/forgot/reset **rate-limit + cooldown**.

---

## 3. Kapsam (uygulama maddeleri)

### 3.1 User email verification modeli
- `User` + `EmailVerifiedAt (DateTime?)` (null → doğrulanmamış).
- **Ayrı `AuthToken` entity** (email + şifre token'ları tek tablo):
  - `Id, UserId, Purpose (EmailVerification|PasswordReset), TokenHash, ExpiresAt, ConsumedAt?, CreatedAt, CreatedIp?, UserAgent?, IsDeleted`.
  - Index: `(TokenHash)` unique (filtered IsDeleted=false), `(UserId, Purpose)`.
  - **`EmailVerificationSentAt` ayrı alanına gerek yok** — cooldown, ilgili purpose'un en yeni token `CreatedAt`'inden hesaplanır (User şişmez). (Alternatif: User'a alan; tercih: token tablosu.)
- Token üretimi: `ITokenService` deseniyle CSPRNG ham token + SHA-256 hash (refresh ile aynı yaklaşım). Ham token yalnızca linke/maile gider; DB'de yalnız hash.

### 3.2 Migration & backfill (`Sprint16_AuthTokens`)
- Yeni tablo `AuthTokens` + `Users.EmailVerifiedAt` kolonu.
- **Backfill:** `UPDATE "Users" SET "EmailVerifiedAt"=now() WHERE "EmailVerifiedAt" IS NULL` → mevcut herkes verified.
- Backfill kimseyi kilitlemez; doğrulama yalnız **yeni** kayıtlar için boş başlar.

### 3.3 Email sender altyapısı
- **`IEmailSender`** (Application/Common/Interfaces): `Task SendAsync(EmailMessage msg, ct)` (to, subject, htmlBody/textBody).
- Impl (Infrastructure/Services):
  - **`LogEmailSender`** (dev/test): linki + içeriği **loglar** (gerçek gönderim yok).
  - **`SmtpEmailSender`** (prod): `System.Net.Mail.SmtpClient`/MailKit ile SMTP env değerleriyle gönderir.
  - **Disabled** modu: no-op (gönderim yok); açıkça seçilmeli.
- `EmailSettings` (`Email` section): `SenderMode (Log|Smtp|Disabled)`, `FromAddress`, `FromName`, `PublicOrigin`,
  `Smtp:{Host,Port,Username,Password,UseTls}`.
- **Fail-fast (Infrastructure DI):** `SenderMode=Smtp` ise `Host`/`From`/`PublicOrigin` zorunlu (eksikse app açılmaz,
  Jwt:Secret deseni). `SenderMode=Log` dev varsayılanı. Secretlar `.env.example`'da placeholder.
- **Link tabanı:** `Email:PublicOrigin` → `{PublicOrigin}/verify-email?token=...` ve `/reset-password?token=...`
  (frontend rotaları). PublicOrigin = kullanıcıya görünen (frontend) origin.
- **DI registrasyonu:** `SenderMode`'a göre `IEmailSender` seçilir (Log/Smtp/Disabled). Notification kanal deseniyle
  uyumlu (mevcut `INotificationChannelSender` gibi soyut).

### 3.4 Yeni register davranışı (`RegisterCommand`)
- User oluşturur, `EmailVerifiedAt=null`.
- EmailVerification `AuthToken` üretir (hash sakla) + `IEmailSender` ile verification link gönderir (dev → loglar).
- Mevcut davranış korunur: token döner (stüdyosuz oturum). Frontend → **check-email** ekranı.

### 3.5 Join-studio doğrulama kuralı
- `ApplyToStudioCommand` handler'ında: çağıran kullanıcının `EmailVerifiedAt==null` ise **403**
  "Stüdyoya katılmak için önce e-posta adresinizi doğrulayın." (login/dashboard akışı açık kalır).
- Frontend `/join-studio`: unverified ise banner + "Tekrar gönder" + form disabled.
- Backfill kullanıcıları verified → mevcut katılım akışları bozulmaz.

### 3.6 register-studio davranışı (seçenek değerlendirmesi)
- **Seçilen (geçici): A.** register-studio akışı **bozulmaz**; Studio + Owner(Active) oluşur; owner
  `EmailVerifiedAt=null` set + verification token/mail. Owner dashboard'da **uyarı banner**'ı (doğrula).
- **Seçenek B (yapılmıyor):** register-studio için doğrulama zorunlu (studio doğrulanana kadar kısıtlı).
- **Risk (A):** doğrulanmamış e-posta ile stüdyo açılabilir → spam/yanlış e-posta riski; ama Owner zaten ödeme/abonelik
  ve gerçek operasyon için motive. İleride B'ye sıkılaştırılabilir (örn. X gün sonra doğrulama zorunlu). Spam'i
  rate-limit + register cooldown sınırlar.

### 3.7 Email resend — `POST /api/auth/resend-verification-email`
- **`[Authorize]`** (oturumlu kullanıcı). Zaten verified → **generic 200**, yeni token ÜRETMEZ.
- Son gönderimden < cooldown (öneri **60sn**) ise yeni token üretmez, generic 200 (veya 429) — abuse engeli.
- Yeni token üretirken aynı purpose'un eski kullanılmamış tokenları iptal (`IsDeleted`/ConsumedAt). Rate-limit policy `auth`.

### 3.8 Verify email — `POST /api/auth/verify-email`
- **Anon**, body `{ token }`. Ham token hash'lenip `AuthToken (EmailVerification)` ile eşleştirilir.
- Expired/consumed/IsDeleted → geçersiz (generic hata). Başarılı → `User.EmailVerifiedAt=now` + token `ConsumedAt=now`.
- Aynı token ikinci kez → geçersiz. Frontend `/verify-email?token=...` → başarılı/başarısız ekranı.

### 3.9 Password reset
- **`POST /api/auth/forgot-password`** (anon, body `{ email }`): **her durumda generic 200**
  "Eğer bu e-posta ile kayıt varsa şifre sıfırlama bağlantısı gönderildi." Kullanıcı varsa PasswordReset token
  üretilir + mail. Var/yok **sızdırılmaz**; rate-limit + cooldown.
- **`POST /api/auth/reset-password`** (anon, body `{ token, newPassword }`): token valid/unused/unexpired olmalı;
  **password policy** (mevcut min 8) çalışır; `PasswordHash` güncellenir; token `ConsumedAt`; **kullanıcının tüm
  refresh tokenları revoke edilir** (yeni `RevokeAllUserRefreshTokensAsync`). Frontend `/forgot-password`,
  `/reset-password?token=...`.

### 3.10 /me + AuthResult — doğrulama durumu
- `MeResult` ve login/register dönüşündeki user DTO'suna **`emailVerified` (bool)** eklenir → frontend banner/gating
  buna bakar (string'e değil).

---

## 4. Güvenlik
- Token düz metin DB'de **yok** (yalnız SHA-256 hash); ham token CSPRNG, yeterli entropi.
- Token **tek kullanımlık** (`ConsumedAt`) + **expiry** (öneri: verification **48 saat**, reset **60 dakika**).
- **Rate-limit** (auth policy) + **cooldown** (resend/forgot 60sn).
- **forgot-password generic 200** + **email enumeration yok** (var/yok aynı yanıt/süre).
- Existing users **backfill** ile kilitlenmez.
- **Password reset sonrası refresh token revoke** (çalınmış oturum kapatılır).
- Verify/resend abuse'a karşı limitli; expired/consumed token generic hata (bilgi sızmaz).

---

## 5. Frontend UX
- Yeni: `/verify-email` (token query → sonuç), `/forgot-password` (email), `/reset-password` (token+yeni şifre).
- register sonrası **check-email** ekranı ("E-posta adresinizi doğrulayın" + tekrar gönder).
- `/join-studio` **email doğrulama banner**'ı (unverified → apply disabled).
- login ekranına **"Şifremi unuttum"** linki.
- Email unverified kullanıcıya dashboard/owner uyarısı (register-studio sonrası).
- `domain-types`/auth servisleri + auth context'e verify/resend/forgot/reset + `emailVerified` senkron.

---

## 6. Testler (sprint16-smoke + E2E)
- Migration backfill: existing users **verified** (EmailVerifiedAt dolu).
- Yeni register → `EmailVerifiedAt=null`; verification token oluşur (DB'de hash, düz metin yok).
- verify token başarılı → `EmailVerifiedAt` set; **aynı token ikinci kez → geçersiz**; **expired token → geçersiz**.
- resend: verified kullanıcıda **yeni token üretmez** (generic 200); cooldown içinde tekrar → yeni token yok.
- **unverified kullanıcı join-studio apply → 403**; **verified kullanıcı apply → 200**.
- forgot-password: var/yok email için **generic 200** (enumeration yok).
- reset-password: valid token ile şifre değişir; **eski şifre çalışmaz, yeni şifre çalışır**; **reset token ikinci kez → geçersiz**.
- **reset sonrası eski refresh tokenlar revoke** (eski refresh ile /refresh → 401).
- Email sender Log modunda link loglanır (gerçek gönderim yok); Disabled modu no-op.
- **Mevcut 16 smoke + security + 34 E2E bozulmaz** (özellikle auth-smoke, sprint15 self-service; backfill sayesinde mevcut kullanıcılar verified).

---

## 7. Sprint 16 Rapor Başlıkları (bittiğinde)
1. Genel Özet 2. Oluşturulan/Değiştirilen Dosyalar 3. AuthToken & EmailVerifiedAt (entity+migration+backfill)
4. Email Sender Altyapısı (Log/Smtp/Disabled + config + fail-fast) 5. Register + Verify + Resend 6. Forgot +
Reset (+refresh revoke) 7. Join-studio Doğrulama Gating 8. register-studio Uyarısı 9. Frontend UX 10. Güvenlik
11. Test/Doğrulama Sonuçları 12. Bilinçli Yapılmayanlar 13. Riskler/Borçlar 14. Sonraki Sprint Önerisi.

---

## 8. Bu sprintte YAPILMAYACAKLAR
- Gerçek SMS, 2FA, OAuth/Google login, public stüdyo dizini, payment gateway, büyük refactor.
- register-studio için zorunlu doğrulama (Seçenek B — ileride).

---

## 9. Açık Karar Noktaları (kodlama onayında netleşecek)
- **Token süreleri:** öneri verification **48 saat**, reset **60 dakika**. (Onayında sabitlenir.)
- **Resend/forgot cooldown:** öneri **60 saniye** + auth rate-limit. Cooldown aşımı → 200 (sessiz) mi 429 mu? Öneri: **200 generic** (enumeration/abuse bilgisini azaltır).
- **EmailSettings dev varsayılanı:** `SenderMode=Log` (dev/test), prod `Smtp`. `Disabled` = doğrulama maili gönderilmez ama akış çalışır (verify endpoint manuel/loglu token ile). Onayında netleşir.
- **register-studio:** Seçenek A (uyarı) sabit; B'ye geçiş ileride ayrı sprint.
- **`emailVerified` alanı** AuthResult/MeResult'a eklenince frontend tip senkronu (kırıcı değil, opsiyonel alan).

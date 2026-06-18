# Kimlik & Üyelik

## Model

- **User** global kimliktir (e-posta global benzersiz). Stüdyo rolü User'da TUTULMAZ.
- Bir kullanıcı **`StudioMembership`** ile bir stüdyoya bağlanır; çok stüdyoda Active üyeliği olabilir.
- Stüdyonun **tek sahibi hesabı** vardır (Owner membership). Ortaklar tek mail ile girer.
- Hoca olmayan yöneticiler için **StudioAdmin** rolü vardır.

## Davet / Başvuru akışı (entity Sprint 0'da, endpoint Sprint 1'de)

İki yön: stüdyo davet eder (`InvitedByStudio`) **veya** kullanıcı başvurur (`RequestedByUser`).
Üyelik `Pending` başlar, kabulde `Active` + `AcceptedAt`. Çıkarma → `Removed`.

## Oturum akışı (Sprint 0 — uygulanmış)

| Endpoint | Davranış |
|---|---|
| `POST /api/auth/register-studio` | User(Owner) + Studio + Active Owner membership; token + refresh cookie |
| `POST /api/auth/register` | Global Standard User (üyeliksiz; token stüdyosuz) |
| `POST /api/auth/login` | Kimlik doğrula; **tek aktif üyelik varsa otomatik seçilir**, çoklu ise stüdyosuz token + üyelik listesi |
| `POST /api/auth/switch-studio` | (Authorize) Üyelik Active doğrulanır, access token seçilen stüdyo bağlamıyla **yeniden üretilir** (refresh'e dokunulmaz) |
| `POST /api/auth/refresh` | Cookie'deki refresh ile yeni access; User+ (varsa) üyelik Active **yeniden kontrol edilir**; rotasyon |
| `POST /api/auth/logout` | Refresh token revoke + cookie temizlenir (idempotent) |
| `GET /api/auth/me` | (Authorize) Kullanıcı + aktif stüdyo + üyelik listesi |

## JWT içeriği

- Her zaman: `sub` (UserId), `global_role`, `email`, ad.
- Stüdyo seçiliyken: `studio_id`, `studio_role`, `membership_id`.
- Access token **kısa ömürlü** (varsayılan 15 dk). Refresh token **httpOnly cookie**, DB'de **SHA-256 hash**.

## Güvenlik kuralları

- Refresh ve switch'te kullanıcı/üyelik Active değilse **reddedilir** (token süresi dolmasa bile).
- Login'de kullanıcı var/yok sızdırılmaz (aynı hata mesajı).
- Kritik stüdyo-içi yazma işlemlerinde `MembershipGuardBehavior` claim'e değil **DB'ye** bakar.

---

## Sprint 15 — Self-service kayıt & stüdyoya katılım isteği

**Kayıt.** Herkes açık hesap açabilir: `POST /api/auth/register` (frontend `/register`) → stüdyosuz Standard
`User`. Stüdyo sahibi yolu ayrı: `POST /api/auth/register-studio`.

**Katılım kodu (JoinCode).** `Studio.JoinCode` (10 karakter, Base32-benzeri alfabe `23456789ABCDEFGHJKMNPQRSTUVWXYZ`
— 0/1/O/I/L yok, CSPRNG, global benzersiz) + `JoinCodeUpdatedAt` + `JoinCodeIsActive`. Slug **değil** JoinCode
ile katılım (slug sızdırmaz). Owner/StudioAdmin görür/yeniler: `GET /api/studio/join-code`,
`POST /api/studio/join-code/regenerate` (eski kod geçersizleşir; `JoinCodeIsActive=false` → kod çalışmaz).
Girdi `Normalize` ile tireli/tiresiz kabul edilir (`ABCD-EFGH-JK` = `abcdefghjk`).

**Katılım isteği.** `POST /api/members/apply` body `{ joinCode, role }`, role ∈ {Student, Instructor} (Owner/
StudioAdmin/PlatformAdmin reddedilir — validator + handler). Aktif JoinCode'lu stüdyo bulunur; geçersiz/pasif →
**generic 404** (stüdyo varlığı/ismi sızmaz). Pending üyelik (`RequestedByUser`) oluşur; **profil OLUŞMAZ**.
Duplicate: Active/Pending → **409**; Rejected/Removed → mevcut satır Pending'e döner (yeniden başvuru). Kullanıcı
kendi başvurularını `GET /api/members/my-join-requests` ile görür (yalnız kendi; bekleyen+reddedilen).

**Onay → profil.** `POST /api/members/{id}/accept` (RequestedByUser → Owner/StudioAdmin onaylar, `MembershipAccess`).
Onayda **idempotent profil**: Role=Instructor → InstructorProfile (yoksa; Rate=0), Role=Student → StudentProfile
(yoksa; Status=Active, UserId/MembershipId bağlı, Source=Web). Invite yolu Instructor profili önceden oluşturduysa
no-op (çift profil yok). Reject → profil oluşmaz. Stüdyo bekleyen istekleri: `GET /api/studio/join-requests`
(Owner/StudioAdmin; applicant ad/e-posta/rol/tarih).

**Stüdyosuz UX.** Login/register sonrası aktif stüdyo üyeliği olmayan kullanıcı `/join-studio`'ya yönlenir
(PlatformAdmin→`/platform`, çok-üyelik→`/select-studio` korunur). Migration `Sprint15_JoinCode` + backfill: mevcut
her stüdyoya benzersiz JoinCode (kimse etkilenmez).

**Email doğrulaması:** bu sürümde YOK; register/login doğrulamasız çalışır. İleride `User.EmailVerifiedAt`
eklenip doğrulanmamış e-posta bazı işlemleri kısıtlayabilir.

Manuel doğrulama: `scripts/sprint15-smoke.sh` (açık register + duplicate, JoinCode geçerlilik + generic 404,
Student/Instructor başvuru + profil-yok, sınırlı roller, join-requests yetki + cross-tenant, my-join-requests
yalnız-kendi, kabul→profil (Owner/StudioAdmin), reject→profil-yok + yeniden başvuru, regenerate eski-kod-çalışmaz).

---

## Sprint 16 — E-posta doğrulama & şifre sıfırlama

**Model.** `User.EmailVerifiedAt?` (null → doğrulanmamış). Tek-kullanımlık tokenlar ayrı **`AuthToken`** tablosunda
(`Purpose: EmailVerification | PasswordReset`, `TokenHash` SHA-256 — **ham token DB'de YOK**, `ExpiresAt`, `ConsumedAt?`).
Ham token CSPRNG (`OpaqueToken`), yalnız maile/linke gider. Yeni token üretilince aynı purpose'un eski kullanılmamışları
soft-delete ile iptal. Süreler: **verification 48 saat, reset 60 dakika**. `EmailTokenIssuer` issue/consume/cooldown sağlar.

**E-posta gönderimi (pluggable).** `IEmailSender` (Application soyut) → Infrastructure: **Log** (dev/test, linki loglar,
gönderim yok), **Smtp** (prod), **Disabled** (no-op). `Email` config: `SenderMode/FromAddress/FromName/PublicOrigin/Smtp`.
Link tabanı `Email:PublicOrigin` (Infrastructure'da; Application yalnız ham token verir). **Fail-fast:** `SenderMode=Smtp`
iken `PublicOrigin`/`Smtp:Host`/`FromAddress` zorunlu; **production + `SenderMode=Disabled`** (Email:AllowDisabledInProduction
yoksa) **uygulama açılmaz**. Dev/test default `Log`.

**Uçlar (auth rate-limit).**
| Uç | Auth | Davranış |
|---|---|---|
| `POST /api/auth/verify-email {token}` | anon | token tüket → `EmailVerifiedAt=now`; expired/used/invalid → generic 409 |
| `POST /api/auth/resend-verification-email` | Authorize | verified → no-op 200; **60sn cooldown → sessiz 200, yeni token yok** |
| `POST /api/auth/forgot-password {email}` | anon | **her durumda generic 200** (var/yok sızmaz); cooldown |
| `POST /api/auth/reset-password {token,newPassword}` | anon | şifre güncelle + **tüm refresh revoke** + null ise **EmailVerifiedAt=now** |

**Kayıt davranışı.** `register` ve `register-studio` → `EmailVerifiedAt=null` + doğrulama maili. register-studio akışı
**bozulmaz** (Seçenek A: stüdyo oluşur, owner dashboard'da uyarı banner'ı görür; doğrulama bu sürümde ZORUNLU değil).

**Gating.** Doğrulanmamış kullanıcı login/logout/resend yapabilir; **stüdyoya başvuru (`apply`) → 403** "önce e-posta
doğrulayın". `UserDto`/`MeResult`'a **`emailVerified`** alanı (frontend gating buna bakar, mesaj string'ine değil).

**Güvenlik.** Token hash'li + tek kullanımlık + süreli; forgot generic 200 (enumeration yok); reset sonrası refresh revoke
(çalınmış oturum kapanır); cooldown + rate-limit; backfill (mevcut kullanıcılar verified) → kimse kilitlenmez.

**Email doğrulamanın geleceği (not).** Bu sürümde register/login doğrulamasız çalışır. İleride register-studio için
doğrulama zorunlu (Seçenek B) yapılabilir; `EmailVerifiedAt` ileride başka kısıtlamalara temel olabilir.

Manuel doğrulama: `scripts/sprint16-smoke.sh` (yeni register unverified + token hash'li/logda; verify + ikinci kez 409;
expired 409; resend verified/cooldown no-op; unverified apply 403 / verified 200; forgot generic 200; reset eski/yeni
şifre + ikinci token 409 + refresh revoke + reset-verifies). LogEmailSender ham token'ı API loguna yazar → smoke logdan çeker.

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

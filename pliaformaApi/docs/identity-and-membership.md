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

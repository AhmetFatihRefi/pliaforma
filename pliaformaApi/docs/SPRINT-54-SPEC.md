# Sprint 54 — Mobile App Foundation

Durum: ONAYA SUNULDU — gatekeeper onayı bekliyor. Kod YAZILMADI; commit/push/tag YOK; store/EAS işlemi YOK.

> Bağlayıcı referanslar: kök `AGENTS.md` (özellikle **§10.2 Mobile Auth Security Rules**), Sprint 53 spec
> (`SPRINT-53-SPEC.md`). Prod şu an `v1.43.0-pilot` — `api/auth/mobile/*` uçları canlı ve trafik bekliyor.

## 1. Amaç

Pliaforma'nın native mobil uygulamasının ilk temeli: `pliaformaMobile/` altında Expo + TypeScript iskeleti,
kendi `AGENTS.md`'si, **yönetici-önce** login (MFA challenge + recovery + trusted device), OS güvenli
depolu token yönetimi, tekil-uçuş refresh'li API client ve **salt-okunur** dashboard başlangıcı.
Store-ready cila, push, deep link bu sprintte YOK — hedef çalışan, güvenli, sade temel.

## 2. Mevcut backend sözleşmeleri (koddan doğrulandı — tahmin yok)

Zarf: tüm yanıtlar `ApiResponse<T>` → `{ success, data, message, errors }` (`Common/Models/ApiResponse.cs`).
Auth sözleşmeleri `Api/Auth/MobileAuthContracts.cs` + `Api/Auth/AuthContracts.cs` + `Features/Auth/AuthDtos.cs`:

| Uç | İstek (body) | Yanıt (`data`) |
|---|---|---|
| `POST /api/auth/mobile/login` | `email, password, trustedDeviceToken?` | `MobileAuthResponse` **veya** `MfaChallengeResponse { challengeToken, mfaChallengeRequired:true }` |
| `POST /api/auth/mobile/refresh` | `refreshToken, studioId?` | `MobileAuthResponse` (yeni refresh dahil; rotasyon) |
| `POST /api/auth/mobile/logout` | `refreshToken?` | ok (idempotent) |
| `POST /api/auth/mobile/mfa/challenge/verify` | `challengeToken, code, rememberDevice?` | `MobileAuthResponse` (+`trustedDeviceToken`,`trustedDeviceExpiresAt` yalnız rememberDevice=true) |
| `POST /api/auth/mobile/mfa/recovery-code/verify` | `challengeToken, recoveryCode` | `MobileAuthResponse` (trusted alanları ASLA yok) |
| `POST /api/auth/mobile/mfa/setup/verify` | `code` (bearer, **yalnız mfa_pending**; tam token → 403) | `MobileMfaSetupCompleteResponse { auth, recoveryCodes[] }` |
| `POST /api/auth/mfa/setup/start` | `{}` (bearer; mfa_pending allowlist'te) | `{ otpauthUri, manualKey }` |
| `POST /api/auth/switch-studio` | `studioId` (bearer; access-only, cookie YOK) | `AuthResponse` (refresh alanı yok — mevcut refresh geçerli kalır) |
| `GET /api/auth/me` | — (bearer) | `MeResult` |

`MobileAuthResponse`: `accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt,
user{id,email,fullName,phone,globalRole,emailVerified,registrationIntent,mfaEnabled},
activeStudio{membershipId,studioId,studioName,studioSlug,role}?, memberships[], mfaSetupRequired,
trustedDeviceToken?, trustedDeviceExpiresAt?`. `null` alanlar JSON'da GÖNDERİLMEZ (WhenWritingNull).
Login davranışı (`Features/Auth/Login.cs`): tek aktif üyelik → `activeStudio` otomatik dolu; birden çok
üyelik → `activeStudio=null`, istemci `switch-studio` çağırır. Rate limit: auth uçları 10/dk/IP (prod).

Dashboard (salt-okunur; `Api/Controllers/DashboardController.cs` + `Features/Dashboards/DashboardDtos.cs`):
- `GET /api/studio/dashboard?month=YYYY-MM` (Owner/StudioAdmin) → `StudioDashboardDto`: öğrenci/hoca
  sayıları, ay içi seans/booking sayıları, `attendance{present,absent,excused,lateCancel,totalMarked,attendanceRate}`,
  `payments{paidAmount,pendingAmount,overdueAmount,+sayılar}`, `packages`, `makeupCredits`,
  `upcoming{todaySessions, upcomingSessionsNext7Days}`.
- `GET /api/studio/class-sessions?from=YYYY-MM-DD&to=YYYY-MM-DD` (Owner/StudioAdmin/Instructor) →
  `ClassSessionDto[]` (instructorName, classTypeName, startAt/endAt, capacity, bookedCount, status,
  sessionKind...; aralık verilmezse bugün+30 gün, max 90).

## 3. Onaya sunulan kararlar

**K1 — Konum:** Mobil uygulama ana repoda `pliaformaMobile/` klasöründe (backend/frontend'in kardeşi).
Docker/compose'a GİRMEZ (store'a dağıtılır, container'a değil).

**K2 — Stack:** Expo (managed) + React Native + TypeScript (strict) + **Expo Router** + `expo-secure-store`.
Data: **plain `fetch` + el yazması küçük typed API client** (web'deki `lib/api.ts` deseninin mobil eşleniği —
envelope çözme + tekil-uçuş refresh). **React Query ve Zod bu sprintte EKLENMEZ**: tüketilen uç sayısı az
(auth + 2 read), tipler elle yazılıp smoke ile doğrulanır; kütüphane ihtiyacı ekran sayısı büyüyünce (Sprint
55+) yeniden değerlendirilir. UI: kütüphanesiz — StyleSheet + küçük ortak bileşenler (Button/Input/Card),
web'in gül/krem palet token'ları sabit olarak taşınır. Gerekçe: repo felsefesi minimal bağımlılık; ilk hedef
çalışan-güvenli-sade temel, store cilası sonraki sprint.

**K3 — MFA setup mobilde VAR (minimum kapsam).** Gerekçe: hedef persona Owner/StudioAdmin ve onlar için
MFA zorunlu; "web'e git" demek ilk kullanım deneyimini bölerdi; backend tarafı zaten hazır ve güvenli
(`mfa/setup/start` mfa_pending allowlist'te; `mobile/mfa/setup/verify` mfa_pending-only, F1). Kapsam:
`manualKey` metin olarak gösterilir + kopyala düğmesi (**QR YOK** — `qrcode` bağımlılığı eklenmez), kod
doğrulama, recovery kodları **BİR KEZ** gösterilir ("kaydettim" onayı istenir), **recovery kodları cihaza
otomatik KAYDEDİLMEZ** (secure store dahil — kullanıcı sorumluluğu, web davranışıyla aynı).

**K4 — Studio selection koşullu VAR:** `memberships.length > 1 && activeStudio == null` ise basit seçim
ekranı → `POST /api/auth/switch-studio` (bearer; cookie'siz, mobil-güvenli — koddan doğrulandı). Tek
üyelikte ekran hiç görünmez. Refresh'te seçim korunur: `mobile/refresh` body'sine `studioId` yazılır.

**K5 — Dashboard uçları:** birincil `GET /api/studio/dashboard` (tek çağrı, zengin özet kartları:
bugün/7-gün ders sayısı, öğrenci, ödeme, paket, telafi) + ikincil `GET /api/studio/class-sessions?from=bugün&to=bugün`
("bugünün dersleri" listesi: saat, ders tipi, hoca, doluluk). İkisi de salt-okunur; hiçbir write çağrısı yok.

**K6 — M3 kararı (mobil tarih formatı) — backend'e KÜÇÜK ADDITIVE DEĞİŞİKLİK öneriyorum.**
Problem: `TurkeyDateTimeJsonConverter` (`Api/Json/`) tüm `DateTime`'ları **offset'siz İstanbul duvar saati**
yazar; `MobileAuthResponse`'taki expiry alanları makine-tüketimidir; İstanbul dışındaki cihaz naive parse ile
saatlerce sapar. Karar (üç seçenekten 1.si):
- `MobileAuthResponse`'a ve `MobileMfaSetupCompleteResponse.Auth`'a **epoch-seconds ikiz alanlar** eklenir:
  `accessTokenExpiresAtUnix, refreshTokenExpiresAtUnix, trustedDeviceExpiresAtUnix?` (long; UTC epoch).
  Additive DTO değişikliği — **migration YOK, web sözleşmesi DEĞİŞMEZ**, mevcut DateTime alanları kalır ama
  mobil istemcinin bunları PARSE ETMESİ YASAKTIR (yalnız Unix alanları + savunma katmanı olarak JWT `exp`
  claim'i kullanılır). Epoch tercihinin gerekçesi: JS'te parser-varyantsız, tek satır (`new Date(x*1000)`);
  kök AGENTS.md §10.2'nin "UTC/Z tercih edilir, sprint başka format belgelerse o geçerli" istisnası bu
  spec'le belgelenmiş olur. `scripts/sprint53-smoke.sh` ömür assert'leri Unix alanlarını da doğrulayacak
  şekilde güncellenir. Bu, Sprint 54'ün TEK backend değişikliğidir (2 DTO dosyası + controller'da alan
  doldurma + smoke) ve deploy'u gerekirse `v1.44.0-pilot` olarak ayrıca onaylanır.

**K7 — API client kuralları (bağlayıcı):** base URL default `https://pliaforma.com`
(`EXPO_PUBLIC_API_URL` ile dev override — public'tir, secret DEĞİLDİR ve secret TAŞIYAMAZ);
`Authorization: Bearer`; cookie YOK, `credentials: 'include'` YOK; refresh yalnız `/api/auth/mobile/refresh`
(web `/api/auth/refresh` mobilde YASAK); token/parola/auth-yanıtı hiçbir log'a yazılmaz; kullanıcıya sade
Türkçe hata (teknik detay/stack gösterilmez; web `lib/errors.ts` sanitizasyon deseni taşınır).

**K8 — Auth state machine:** §5'te; tekil-uçuş refresh + atomik rotasyon yazımı + sınırlı retry bağlayıcıdır.

**K9 — Tag düzeni:** Backend/web mevcut düzende kalır (`vX.Y.Z-pilot`; K6 deploy edilirse `v1.44.0-pilot`).
Mobil sürümler AYRI önek: **`mobile-vX.Y.Z-pilot`** (ilk store-öncesi iskelet için tag GEREKMEZ; ilk
gerçek release adayında `mobile-v0.1.0-pilot`). Store/EAS/App Store/Play gönderimi her zaman AYRI deploy
onayıdır (kök AGENTS.md §3.3 + mobil AGENTS.md).

**K10 — UX dili:** Türkçe; sade/premium/güven veren; kısa hata mesajları; klavye davranışı (KeyboardAvoiding),
loading/disabled durumları, form validation state'leri; güvenlik mesajları kullanıcıya teknik anlatılmaz.

## 4. `pliaformaMobile/` yapısı + mobil AGENTS.md

```text
pliaformaMobile/
├── AGENTS.md              (mobil çalışma kuralları — aşağıda)
├── app/                   (Expo Router)
│   ├── (auth)/login.tsx · mfa-challenge.tsx · recovery-code.tsx · mfa-setup.tsx · select-studio.tsx
│   ├── (app)/dashboard.tsx · account.tsx
│   ├── _layout.tsx        (AuthProvider + guard: bootstrap → auth/app yönlendirme)
│   └── index.tsx          (bootstrapping/loading ekranı)
├── src/
│   ├── api/client.ts      (fetch wrapper: envelope + bearer + tekil-uçuş refresh)
│   ├── api/types.ts       (§2 sözleşmelerinin elle yazılmış TS karşılığı)
│   ├── auth/tokenStore.ts (SecureStore erişimi — TEK modül; başka yerden SecureStore çağrılmaz)
│   ├── auth/AuthContext.tsx (state machine)
│   └── ui/                (Button, Input, Card, tema token'ları)
├── package.json · tsconfig.json · app.json (bundle id: com.pliaforma.app önerisi)
```

**`pliaformaMobile/AGENTS.md` zorunlu içeriği** (kök AGENTS.md'yi tamamlar, ÇELİŞEMEZ):
token'lar AsyncStorage'da SAKLANAMAZ; refresh + trusted-device token yalnız `expo-secure-store`'da; access
token yalnız bellekte (restart → refresh ile yenilenir); parola asla saklanmaz; token/secret/auth yanıtı
loglanmaz; API base URL public olabilir ama secret olamaz; `EXPO_PUBLIC_*` içine secret KONULAMAZ (bundle
reverse-engineer edilebilir); store submission / EAS build / App Store / Play gönderimi AYRI açık onay ister;
prod kullanıcı verisiyle debug log alınmaz; screenshot/log token veya kişisel veri içeremez; mobil auth
YALNIZ `api/auth/mobile/*` (web refresh ucu yasak); cookie/`credentials:'include'` yasak; kök §10.2 kuralları
aynen geçerli; tag düzeni `mobile-vX.Y.Z-pilot`.

## 5. Auth akışı ve state machine (bağlayıcı)

**Bootstrap (app açılışı):** SecureStore'da `refreshToken` var mı? → YOKSA login'e. VARSA
`POST mobile/refresh {refreshToken, studioId?}` → başarılı: yeni refresh **önce SecureStore'a atomik yazılır,
sonra** eski değerin üzerine geçilmiş sayılır (yazım başarısızsa oturum düşürülür; eski token zaten rotasyonla
öldü — yarım durum bırakılmaz); access token belleğe; app'e gir. Başarısız (401/ağ dışı): storage temizle →
login. (Ağ hatasında token SİLİNMEZ; yeniden dene ekranı.)

**Login:** `mobile/login {email, password, trustedDeviceToken?}` (trusted token SecureStore'da varsa eklenir).
Yanıt üçlü dallanır: (a) `mfaChallengeRequired` → MFA ekranı (kod veya recovery; `rememberDevice` toggle
**default KAPALI**; işaretliyse dönen `trustedDeviceToken` SecureStore'a); (b) `mfaSetupRequired` → K3 setup
akışı (mfa_pending token yalnız setup uçlarına erişir — backend zorlar); (c) normal → K4 studio kontrolü → app.

**Access token yaşam döngüsü:** bellekte; süre kontrolü K6 Unix alanı (+JWT `exp`) ile. Herhangi bir API
çağrısı 401 dönerse: **tekil-uçuş** refresh (aynı anda tek refresh isteği; bekleyenler aynı promise'i paylaşır)
→ başarılıysa orijinal istek **bir kez** retry; refresh de 401 ise → logout. **Sınırsız retry YOK** (istek
başına en fazla 1 refresh+retry; refresh'in kendisi retry edilmez).

**Logout:** `POST mobile/logout {refreshToken}` (best-effort; ağ hatası logout'u engellemez) → SecureStore'dan
refresh + trusted token silinir → bellekteki access token silinir → login'e. Trusted token'ın logout'ta
silinip silinmeyeceği: SİLİNİR (tam temizlik; kullanıcı yeniden "hatırla" seçebilir) — basitlik tercihi.

## 6. Ekranlar (minimum)

Auth stack: Login · MFA challenge (kod + "cihazı hatırla") · Recovery code · MFA setup (K3) ·
Studio selection (K4, koşullu) · Bootstrap/loading. App stack: Dashboard (K5) · Account (ad/e-posta/stüdyo/
rol + çıkış). Settings ayrı ekran olarak YOK (Account yeterli).

## 7. Backend değişikliği (yalnız K6 — onaylanırsa)

`Api/Auth/MobileAuthContracts.cs`: `MobileAuthResponse`'a 3 nullable-olmayan/nullable long alan
(`AccessTokenExpiresAtUnix`, `RefreshTokenExpiresAtUnix`, `TrustedDeviceExpiresAtUnix?`) + `From(...)`
doldurması (kaynak `DateTime`'lar UTC üretiliyor — `TokenService`/`TrustedMfaDeviceService` UtcNow tabanlı;
`DateTimeOffset`'e çevirip `ToUnixTimeSeconds()`). Web `AuthResponse` ve tüm web uçları DEĞİŞMEZ; migration
YOK; `sprint53-smoke.sh` ömür assert'lerine Unix alan kontrolü eklenir. Bu değişiklik yapılırsa Builder
raporunda ayrı belirtilir ve deploy'u ayrı onaya tabidir (`v1.44.0-pilot` adayı).

## 8. Validation planı

Backend (yalnız K6 yapılırsa): `dotnet build -c Release` 0/0 · `has-pending-model-changes` temiz ·
`sprint53-smoke.sh` (güncellenmiş) + `auth-smoke` + `mfa-login` + `mfa-recovery` + `sprint50` GEÇMELİ.

Mobil: `npm install` (lockfile üretimi) · `npx tsc --noEmit` 0 hata · `npx expo-doctor` (veya eşdeğeri) temiz ·
lint kurulursa 0 yeni hata · mümkünse `npx expo export` build-smoke · elle akış doğrulaması: dev backend'e
karşı login→MFA→dashboard (Builder raporunda dürüstçe: hangi akış gerçek cihaz/emülatörde denendi, hangisi denenmedi).

Yeni smoke: **`scripts/sprint54-mobile-smoke.sh`** (statik denetim; backend gerektirmez):
`pliaformaMobile/` ve `pliaformaMobile/AGENTS.md` var · `package.json` script'leri var · `tsc --noEmit`
geçiyor · `expo-secure-store` dependency'de · **AsyncStorage ile token saklama YOK** (grep: `AsyncStorage`
token bağlamında geçmiyor) · SecureStore çağrıları yalnız `tokenStore.ts`'te · mobil auth uçları kullanılıyor
(`/api/auth/mobile/`) · web refresh ucu (`/api/auth/refresh"`) mobil kodda GEÇMİYOR · `credentials`/
`Set-Cookie`/cookie mantığı yok · token/parola log'lanan `console.*` çağrısı yok (grep desenleri) ·
`EXPO_PUBLIC_` değerlerinde secret deseni yok · API base URL public.

## 9. Kapsam dışı

Push notification; device registration; deep link/universal links; minimum version gate; App Store/Play
submission; EAS production build; payment/booking/attendance yazma akışları; öğrenci ve eğitmen mobil
uygulamaları; offline mode; biyometrik kilit; analytics; crash reporting; register/register-studio; public
onboarding; CORS/nginx değişikliği; K6 dışında her tür backend değişikliği; production deploy (K6 sonrası
ayrıca onaylanmadıkça).

## 10. Riskler

- Expo/RN sürüm ekosistemi hızlı değişir → lockfile commit'lenir; sürümler sabitlenir; `expo-doctor` validation'da.
- Auth state machine yanlışlığı = kilitlenme/oturum kaybı → state machine bu spec'te bağlayıcı yazıldı; smoke +
  Reviewer kontrol listesi buna odaklı.
- 10/dk/IP auth rate limit dev'de test akışlarını zorlayabilir → dev backend `AuthPermitPerMinute=1000` (mevcut).
- `mobile/refresh` yanıtındaki `studioId` düşerse bağlam kaybı → refresh çağrısı her zaman aktif `studioId`'yi taşır.
- Trusted token cihazda uzun süre durur → SecureStore (Keychain/Keystore) + logout'ta silme + backend'in 30g süresi.

## 11. Builder rapor beklentisi

Builder raporu şu 20 soruyu cevaplamalı: (1) `pliaformaMobile/` nasıl kuruldu? (2) Mobil AGENTS.md ne
içeriyor? (3) Hangi auth uçları kullanıldı? (4) Token'lar nerede saklanıyor? (5) Access token nasıl
yönetiliyor? (6) Refresh rotasyonu nasıl ele alındı (atomik yazım dahil)? (7) Trusted device nasıl
saklanıyor? (8) Recovery code akışı nasıl çalışıyor? (9) MFA setup desteklendi mi (K3 sınırlarıyla)?
(10) Dashboard hangi uçlardan okuyor? (11) Tarih/expiry formatı nasıl çözüldü (K6)? (12) Backend değişikliği
var mı? (13) Migration var mı (beklenen: HAYIR)? (14) Web frontend etkilendi mi (beklenen: HAYIR)?
(15) CORS/nginx değişti mi (beklenen: HAYIR)? (16) Store/EAS deploy yapıldı mı (beklenen: HAYIR)?
(17) Hangi dosyalar değişti? (18) Validation sonuçları? (19) Smoke sonuçları? (20) Commit/tag önerisi?

## 12. Reviewer kontrol listesi

Reviewer özellikle şunları denetler: bundle'a secret girdi mi (`EXPO_PUBLIC_*` dahil)? SecureStore dışında
refresh/trusted token saklanıyor mu? AsyncStorage token var mı? Token/parola loglanıyor mu? Web refresh ucu
kullanılıyor mu? cookie/`credentials:'include'` var mı? Refresh tekil-uçuş mu? 401 retry-loop riski var mı?
Logout storage'ı tamamen temizliyor mu? Expiry parse timezone-safe mi (yalnız Unix/JWT-exp)? Dashboard'da
herhangi bir write var mı? Backend/web auth davranışı bozuldu mu (K6 diff'i web'e sızdı mı)? Mobil AGENTS.md
kök AGENTS.md + §10.2 ile uyumlu mu? Recovery kodları cihaza otomatik kaydediliyor mu (YASAK)?

## 13. Onay kapıları

Bu spec onaylanınca Builder yalnız şunları üretir: `pliaformaMobile/` iskeleti, mobil AGENTS.md,
`scripts/sprint54-mobile-smoke.sh`, (K6 onaylıysa) 2 backend DTO dosyası + smoke güncellemesi.
Commit/push/tag/deploy/store işlemleri her zamanki gibi ayrı gatekeeper onayına tabidir.

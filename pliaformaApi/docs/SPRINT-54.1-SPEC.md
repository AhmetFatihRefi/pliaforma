# Sprint 54.1 — Mobile CI Validation

Durum: ONAYA SUNULDU — gatekeeper onayı bekliyor. Kod YAZILMADI; commit/push/tag YOK; deploy/store/EAS YOK.
Kapsam: **CI/docs-only** — yalnız `.github/workflows/ci.yml` + bu spec değişir. AGENTS.md §13 ("CI workflow'u
yalnız sprint CI hakkındaysa değiştirilir") bu sprintle sağlanır.

## 1. Amaç

Sprint 54 ile eklenen `pliaformaMobile/` (Expo SDK 57) lokalde 5 doğrulamadan geçti (npm install, tsc,
expo-doctor, expo export, sprint54-mobile-smoke) ama CI'da korunmuyor: mobil kodu kıran bir commit bugün
main'e yeşil girer. Bu sprint, mevcut doğrulama-only CI felsefesini (Sprint 51) bozmadan mobil tarafı da
kapıya bağlar.

## 2. Mevcut durum (dosya referanslı)

`.github/workflows/ci.yml`: tetikler push(main)+tags(v*)+PR+dispatch; `permissions: contents: read`;
concurrency iptalli; `env.NODE_VERSION: '22'` zaten pinli (frontend + mobil için uygun — lokal doğrulama
Node 22 ile yapıldı); 4 job: `backend-build`, `frontend-build` (setup-node cache:npm →
`pliaformaFrontend/package-lock.json`), `docker-backend`, `docker-frontend`. Secret YOK.
`security.yml` ayrı ve bu sprintte DOKUNULMAZ.

`pliaformaApi/scripts/sprint54-mobile-smoke.sh`: konum-bağımsız (mobil dizini `$(dirname $0)/../../pliaformaMobile`
default'uyla bulur, argümanla override edilebilir); içinde `npx tsc` koştuğu için **npm ci'dan SONRA**
çalışmalıdır; `git check-ignore` kullandığı için checkout'lu repo yeterlidir (ignore kuralları repo
dosyalarında — taze clone'da çalışır, lokalde kanıtlı).

## 3. Onaya sunulan kararlar

**K1 — Ayrı workflow DEĞİL, mevcut `ci.yml`'e yeni job.** Job adı **`mobile-validation`**
(görünen ad: `mobile validation (expo)`). Gerekçe: backend/frontend/mobile tek workflow'da görünür;
ileride branch protection tek workflow'a bağlanabilir; tetikler/permissions/concurrency mirası otomatik;
`security.yml` ayrı kalır. Mevcut 4 job'a ve `on:` bloğuna DOKUNULMAZ (diff ile kanıtlanacak).

**K2 — Job adımları** (bağımsız job; diğerlerine `needs` bağı YOK — paralel koşar, timeout 15 dk):
1. `actions/checkout@v4`
2. `actions/setup-node@v4` — `node-version: ${{ env.NODE_VERSION }}` (22), `cache: npm`,
   `cache-dependency-path: pliaformaMobile/package-lock.json`
3. `npm ci` (`working-directory: pliaformaMobile` — job-level default) — **BLOCKING**; lockfile
   tutarlılığını da zorlar (`npm install` KULLANILMAZ)
4. `npx tsc --noEmit` — **BLOCKING**
5. `npx expo-doctor` — **ADVISORY** (`continue-on-error: true`) — gerekçe K4
6. `npx expo export --platform android` — **BLOCKING** — gerekçe K5
7. `bash pliaformaApi/scripts/sprint54-mobile-smoke.sh` — **BLOCKING**; bu adım job default'unu ezerek
   **repo root'tan** koşar (`working-directory: .`) — path karışıklığı olmasın diye açık yazılır; script
   zaten konum-bağımsızdır (§2)

**K3 — Cache:** setup-node'un npm cache'i mobil lockfile'a bağlanır; anahtar lockfile hash'inden türediği
için `frontend-build`'in cache'iyle ÇAKIŞMAZ (farklı dosya → farklı anahtar) ve o job'a dokunulmaz.
Cache miss build'i kırmaz, yalnız yavaşlatır.

**K4 — `expo-doctor` neden advisory:** doctor'ın sürüm-uyum kontrolleri Expo'nun CANLI sürüm kayıtlarına
bakar; Expo yeni bir SDK-57 patch'i yayınladığında repo'da sıfır değişiklikle CI kızarabilir (dış-servis
bağımlılığı). Repo emsali: dış-servise bağımlı denetimler advisory (security.yml deps taramaları,
frontend lint). Deterministik kapılar (npm ci/tsc/export/smoke) blocking kaldığı için koruma zayıflamaz.
Doctor çıktısı yine logda görünür; kronik temizse ileride bilinçli olarak blocking'e çevrilebilir.
*(Alternatif: blocking başlat, flake görülürse gevşet — gatekeeper tercih ederse tek satırlık fark.)*

**K5 — `expo export` BLOCKING** (gatekeeper önerisiyle uyumlu): lokalde tekrarlanabilir şekilde geçti;
Metro tüm importları uçtan uca derlediği için "tsc geçer ama bundle kırılır" sınıfını yakalayan tek kapı.
Yalnız `--platform android` (lokal doğrulamayla aynı; iOS bundle'ı aynı Metro hattı olduğundan ek sinyal
vermez, süre ekler; web bilinçli desteklenmiyor — SecureStore yok).

**K6 — Secret/EAS/store SIFIR:** Bu job store'a göndermez, EAS build yapmaz, imzalamaz. `expo export` ve
`expo-doctor` kimlik doğrulamasız çalışır → **EXPO_TOKEN GEREKMEZ**. CI'ya hiçbir secret eklenmez; özellikle
yasak: EAS_TOKEN, Apple/Google credentials, API secret, production .env, Firebase/FCM/APNs, Resend/DB/JWT/
MFA/backup secret'ları. `EXPO_PUBLIC_API_URL` set EDİLMEZ (gerekmiyor; edilseydi bile public değerdir,
secret taşıyamaz — mobil AGENTS.md). `permissions: contents: read` aynen kalır.

**K7 — Tetikler/isim değişmez:** `on:` bloğu, workflow adı (`ci`), concurrency ve mevcut env aynen korunur;
mobil job da aynı tetiklerle (push main/tags, PR, dispatch) koşar. Path-filter EKLENMEZ (diğer job'larda da
yok; tutarlılık + "mobil değişmese de mobil hâlâ derleniyor" güvencesi).

## 4. Kapsam dışı

Mobil/backend/web frontend uygulama kodu değişikliği; migration; Docker/compose değişikliği; `security.yml`;
EAS build; store submission; app signing; push notification; device registration; deep link; production
deploy; `v1.44.0-pilot` tag/deploy; branch protection ayarı (GitHub UI işi — ileride ayrıca).

## 5. Validation planı (Builder için şart)

Yerel:
- `git diff --name-only` → YALNIZ `.github/workflows/ci.yml` (+ bu spec untracked) — backend/frontend
  job'larına dokunulmadığının kanıtı olarak `git diff .github/workflows/ci.yml` çıktısında mevcut job
  satırlarının değişmediği gösterilir.
- YAML doğrulaması (mümkünse): `actionlint` varsa o; yoksa `node -e "require('js-yaml')..."` ya da
  `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))"` — biri yeterli.
- CI komut dizisinin YEREL birebir tekrarı: `cd pliaformaMobile && npm ci && npx tsc --noEmit &&
  npx expo-doctor && npx expo export --platform android && cd .. && bash pliaformaApi/scripts/sprint54-mobile-smoke.sh`
  (npm ci node_modules'u silip lockfile'dan kurar — bilinçli; süre uzar ama CI'ın gerçek koşusu budur).
- Backend/frontend build TEKRAR ŞART DEĞİL (CI-only değişiklik; uygulama koduna dokunulmuyor) — bunun yerine
  diff'in yalnız yeni job eklediği kanıtlanır. Asıl backend/frontend doğrulaması push sonrası CI'ın
  kendisidir (gatekeeper commit sonrası Actions'tan teyit eder).

## 6. Reviewer kontrol listesi

`npm ci` mi (`npm install` değil) + doğru lockfile path'i mi? · typecheck/doctor/smoke/export adımları var
ve doğru sırada mı (smoke npm ci'dan sonra, root'tan)? · working-directory'ler doğru mu? · secret/EAS/store
izi var mı (job env'inde, adımlarda)? · backend/frontend/docker job'ları ve `on:`/permissions/concurrency
bit değişmemiş mi? · security.yml dokunulmamış mı? · cache path mobil lockfile'a mı bağlı, frontend cache'i
etkilenmiyor mu? · Node 22 mobil için uygun mu (lokal doğrulamayla aynı) · doctor advisory kararı
gerekçesiyle uygulanmış mı · değişiklik CI/docs-only mu?

## 7. Builder rapor beklentisi

(1) ci.yml içine mi ayrı mı? (2) Job adı? (3) Node sürümü? (4) Cache nasıl? (5) Hangi komutlar?
(6) export blocking mi? (7) Smoke hangi path'ten? (8) Secret gerekiyor mu? (9) EAS/store var mı?
(10) Backend/frontend job'ları değişti mi? (11) security.yml değişti mi? (12) Değişen dosyalar?
(13) Yerel validation sonuçları? (14) Commit/tag/deploy önerisi?

## 8. Commit/tag önerisi (onay sonrası için)

Commit: `ci: add mobile validation job (Sprint 54.1)` — Sprint 51 emsaliyle (`ci: add build validation
pipeline`) aynı stil. Tag GEREKMEZ (AGENTS.md §5: CI-only değişiklik tag istemez). Deploy YOK.

# SPRINT 24 — Student Self-Service Contract, Debt & Makeup Visibility

## Amaç

Sprint 23'te staff tarafında sözleşme amendment + borç motoru kuruldu. Sprint 24, öğrencinin **kendi panelinde**
sözleşmesini, kalan ders hakkını, borç/ödeme durumunu, ek işlem geçmişini ve telafi durumunu **güvenli + sade +
salt-okunur** görmesini sağlar. Öğrenci bu sprintte **hiçbir işlem yapmaz** (yalnız görüntüleme).

## Mevcut student panel analizi (kanıtlanan)

- **Dashboard** (`dashboard/page.tsx` → `StudentPanel`): zaten paket hakkı, telafi kredisi, bekleyen/gecikmiş ödeme,
  katılım özeti kartlarını gösteriyor.
- **Sidebar** (Student rolü): Programım (my-bookings), Telafi Kredilerim (my-makeup-credits), Katılım Geçmişim
  (my-attendance), Ödemelerim (my-payments), Profilim. **Sözleşme görünümü YOK.**
- **8 student endpoint** `/api/student/...`, hepsi salt-okunur, plain `[Authorize]` + **resolve-own-profile**:
  `RequireStudioId() + RequireUserId() → StudentProfiles WHERE StudioId==sid AND UserId==uid → profileId → scoped`.
- **Sprint 23 borç kodu** (`GetStudentDebtHandler`) tenant-scoped + rol-agnostik ama `StudentProfileId`'yi
  parametreden alıyor ve controller'da Owner/StudioAdmin kilidi var → öğrenciye doğrudan açılamaz.
- `PaymentDto.StudentContractId` zaten mevcut (Sprint 23) → ödeme↔sözleşme eşlemesi hazır.
- `ContractAmendmentDto` `CreatedByName` (internal) içeriyor → öğrenciye gösterilmemeli.

## Yeni student endpoint'leri

`StudentContractsController` — `[Authorize(Roles="Student")]`, route `api/student`:

| Verb | Route | Handler | Döner |
|---|---|---|---|
| GET | `api/student/contracts` | `GetMyContractsQuery` | `StudentContractViewDto[]` (gömülü amendments) |
| GET | `api/student/debt` | `GetMyDebtQuery` | `MyDebtSummaryDto` (per-currency) |

Her iki handler **parametresizdir**; kimliği token'dan çözer (`StudentSelfResolver.ResolveProfileIdAsync`).
Profil yoksa boş döner. Sprint 23 borç hesabı `ContractDebtService` paylaşılan helper'a çıkarıldı; staff handler
da aynı helper'ı kullanır (davranış değişmedi). Mevcut my-* endpoint'leri (plain `[Authorize]`) **değiştirilmedi**.

## Güvenlik modeli

1. **Rol kapısı:** yeni hassas endpoint'ler `[Authorize(Roles="Student")]` → Instructor/Owner/StudioAdmin **403**.
2. **Resolve-own-profile:** `userId + studioId → kendi StudentProfileId`. **Hiçbir endpoint dışarıdan
   studentProfileId/contractId almaz** → Student A yapısal olarak Student B'nin verisine erişemez.
3. **Amendment id-bazlı endpoint YOK:** amendment'lar sözleşme listesine **gömülü** → öğrenci contractId tahmin
   edip başka veriye erişme yüzeyi oluşmaz.
4. **Tenant izolasyonu:** tüm sorgular `studioId`'ye scoped.
5. **Yazma yok:** yalnız GET; POST/PUT yok → **405**. (Öğrenci ödeme/amendment/ders seçimi yapamaz.)

## Trimmed DTO gerekçesi

Öğrenciye staff-internal alanlar **sızmamalı**. Bu yüzden ayrı trimmed DTO'lar:

- **`StudentContractViewDto`**: `{ id, classTypeName, instructorName, startDate, endDate, totalLessonCount,
  scheduledLessonCount, availableLessonCount, status, amendments[] }`. **Yok:** `Price` (ücret yalnız ödeme
  durumu bölümünde), staff `Notes`, internal id'ler.
- **`StudentAmendmentDto`**: `{ kind, additionalLessonCount, datesExtended, newEndDate, additionalPrice, currency,
  description, createdAt }`. **Yok:** `CreatedByName` (kim yaptı), `Previous/NewTotalLessonCount` (audit snapshot).

Staff DTO'ları (`ContractAmendmentDto`, `StudentContractDto`, `StudentDebtSummaryDto`) korunur; öğrenci ayrı DTO alır.

## Multi-currency kararı

Öğrenci borç görünümü **para birimine göre AYRI** gruplanır (`MyDebtSummaryDto.byCurrency` =
`{ currency, totalCharges, totalPaid, totalOutstanding }[]`). **Tek cross-currency toplam ÜRETİLMEZ** —
farklı para birimleri birbirine eklenmez. Per-contract kartlar da kendi currency'siyle gösterilir. Etiketsiz
ödemeler de para birimine göre ayrı (`untaggedByCurrency`, bilgi amaçlı). Tek para birimli stüdyoda `byCurrency`
tek eleman → normal özet gibi görünür.

## Frontend akışı

- **Sidebar:** Student rolüne "Sözleşmelerim" (`/dashboard/my-contracts`).
- **my-contracts sayfası** (salt-okunur, mobil uyumlu, Card/Badge):
  - Aktif sözleşme kartı: ders tipi · hoca · başlangıç→bitiş · durum.
  - Ders hakkı: toplam / planlanmış / kalan planlanabilir.
  - **Ödeme durumu** (yumuşak dil — "Borç" değil): Toplam tutar · Ek ücretler · Ödenen · **Kalan ödeme**.
  - Ek işlem geçmişi (yumuşak: "+4 ders eklendi", "Bitiş tarihi uzatıldı", "Ek ücret: X", açıklama, tarih —
    kim yaptığı gösterilmez).
  - Genel ödeme durumu: para birimi başına ayrı blok; etiketsiz ödemeler "Diğer ödemeler".
- **my-payments:** bozulmadan zenginleştirildi — `studentContractId` varsa "Reformer sözleşmesi · tarih",
  null ise "Diğer ödeme" gösterilir (sözleşme listesi başarısız olursa zarif düşüş; yeni işlem yok).
- **my-makeup-credits:** dokunulmadı. Dashboard telafi kartı zaten mevcut.

## Test / doğrulama sonuçları

- Backend build **0/0**; **migration YOK**; `has-pending-model-changes` → **No changes**.
- Frontend **tsc 0**, **next build 0** (my-contracts route üretildi).
- **sprint24-smoke 25/25**: öğrenci kendi sözleşmesini/borcunu görür; **Student A, Student B'yi göremez**
  (sözleşme + debt izolasyonu); **Instructor contracts/debt 403**; Owner staff debt 200 devam;
  **createdByName/previousTotalLessonCount/newTotalLessonCount/Price sızmıyor**; **multi-currency TRY+USD ayrı
  (7000 ve 100 birleştirilmedi)**; POST → **405**; my-payments + my-makeup-credits 200 (regresyon yok).
- **Smoke suite 26/26** (dev + compose). **E2E 56/56** (11-self-service "Sözleşmelerim" + 04-student sidebar yeşil).
- **Full compose validation** yeşil: config dev/prod 0 · up --build · migration yok · health · same-origin (401) ·
  seed · compose smoke · compose E2E · down -v + restore.
- Audit: migration yok · yeni write endpoint yok · internal alan sızmıyor · multi-currency tek-toplam yok ·
  9999 UI korunuyor · cross-student izolasyon.

## Kapsam dışı

Online ödeme · ödeme linki · fatura/e-arşiv · muhasebe · kampanya/indirim · taksit otomasyonu · öğrenci
self-booking · telafi dersi seçme · drag/drop grid · closure full-undo · package/rate DB drop. Öğrenci hiçbir
yazma işlemi yapmaz.

## Dürüst not (teknik borç)

- **Staff borç özeti (`StudentDebtSummaryDto`) hâlâ tek-currency MVP varsayımıyla roll-up yapar** (Sprint 23'ten;
  stüdyo tek para birimiyle çalışır varsayımı). Bu sprintte değiştirilmedi. **Öğrenci tarafı per-currency
  güvenlidir** (tek cross-currency toplam üretmez), bu yüzden Sprint 24 için blocker değildir. Staff tarafı
  multi-currency roll-up'ı ileride ele alınabilir.
- **Misafir öğrenci** (UserId null) login olamaz → bu görünümler yalnız kullanıcıya dönüştürülmüş öğrenci içindir.

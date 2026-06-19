# SPRINT 18b — Booking Cutover & Package Deprecation (SPEC)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md) · Faz 1'in ikinci yarısı. **Breaking** (bilinçli). 18a additive'di;
> 18b ana ürün mantığını sözleşmeye geçirir. **Durum: UYGULANDI** (kod + test taşıma tamam; migration YOK; commit/push
> onay bekliyor). Sonuç/rapor için en alttaki "Sonuç — Uygulama & Doğrulama" bölümüne bak. Bu dosya yalnız 18b'yi
> kapsar; epic metni ayrı (EPIC-CONTRACTS.md).

## Amaç (neden)
18a'da sözleşme yolu eklendi ama eski paket/booking yolu hâlâ çalışıyor (yarı-durum). Faz 2'ye (yoklama/telafi/
rapor) geçmeden önce **Regular ders = sözleşme** olmalı; yoksa yoklama/telafi/rapor iki modele göre düşünülür.
Bu sprint: Regular booking'i sözleşme zorunlu yapar, paket gate'i ana akıştan çıkarır, öğrenci self-booking'i
kapatır, staff booking'i sözleşme üzerinden yürütür, test scaffolding'i sözleşmeye taşır. **DB drop YOK.**

---

## 1) Regular booking sözleşme zorunlu

**`BookingCreator.CreateAsync`** (`Features/Bookings/BookingCreator.cs`):
- `SessionKind == Regular` → **sözleşme zorunlu**:
  - `studentContractId` verildiyse → `ContractEligibilityChecker` ile doğrula (18a). Uygun değilse 409.
  - Verilmediyse → **auto-resolve** (yeni `ContractResolver`): öğrenci + seans ders tipi + seans tarihi aralıkta +
    `Status=Active` + available>0 olan sözleşmeyi bul (deterministik: en erken biten). Bulamazsa → **409**
    "Bu ders için uygun aktif sözleşme yok."
  - **Paket dalı kaldırılır** (PackageEligibilityChecker / RequiresPayment / ExpectedStudentPackageId artık
    Regular akışında YOK). `StudentPackageConsumer` çağrılmaz.
- `SessionKind == Makeup` → **değişmez** (telafi kredisi yolu; tam revamp Faz 3). Sözleşme gerektirmez.
- `SessionKind == Trial/Special` → **istisna**: sözleşme gerektirmez (özel seans). (Çakışma/bireysel kuralları yine geçerli.)
- 18a'da eklenen **öğrenci/hoca çakışma + bireysel** kontrolleri korunur.

**Yeni helper `ContractResolver`** (`Features/Contracts/ContractResolver.cs`): `ResolveAsync(studioId, studentId,
classTypeId, sessionDate)` → uygun aktif sözleşme (available>0, tarih/ders tipi uyumlu) veya null. DI'ye eklenir.

## 2) Staff booking sözleşme üzerinden
**`StaffCreateBooking.cs`** (18a'da opsiyonel `StudentContractId` var):
- contractId verildiyse → o öğrenciye+stüdyoya ait doğrulanır (18a) + BookingCreator sözleşme yolu.
- verilmediyse → BookingCreator **auto-resolve** eder. Uygun yoksa 409. (Kullanıcı sözleşmesiz Regular booking yapamaz.)
- Frontend staff "Öğrenci ekle" modalı (`schedule/page.tsx`): opsiyonel **sözleşme seçimi** (yoksa otomatik bulunur);
  hangi sözleşmeden düşüldüğü bilgisi gösterilebilir (nice-to-have).

## 3) Öğrenci self-booking kapatılır (admin-güdümlü)
- **Backend:** `StudentBookCommand`/`StudentBookHandler` (`Features/Bookings/StudentBook.cs`) +
  `StudentBookingsController` `POST class-sessions/{id}/book` → **410 Gone** "Ders rezervasyonu stüdyo tarafından
  yapılır." (GoneException — Rev3'te eklendi.) Cancel-self (`.../bookings/{id}/cancel` student) — açık soru: kapalı mı?
  Öneri: öğrenci kendi rezervasyonunu **iptal de edemesin** (admin yönetir) → 410. (Onay gerek.)
- **Frontend:** `book-classes/page.tsx` ana akıştan kaldırılır (Sidebar'dan link silinir; sayfa kapalı mesajı/redirect).
  `my-bookings/page.tsx` **kalır** (öğrenci kendi programını görür, salt-okunur). `studentBookingService.book/cancel`
  çağrıları kaldırılır.

## 4) Paket/kredi ana akıştan çıkar (dormant, DB drop YOK)
- **Booking/attendance:** Regular booking paket linklemez (`ExpectedStudentPackageId` null kalır) → attendance'taki
  `ConsumePackageCredit` sözleşme booking'leri için **no-op** (zaten paket yok). `StudentPackageConsumer` kodda kalır
  (dormant). Attendance tam akışı Faz 2.
- **Endpoint'ler:** `PackagePlansController`, `StudentPackagesController` **işlevsel kalır ama UI'dan çıkar** (dormant).
  Tamamen kapatmak gerekmez; gerekirse 410. (Öneri: dokunma — yalnız UI gizle; package smoke'ları deprecate edilir.)
- **Frontend paket UI kaldırılır/gizlenir:** `package-plans/page.tsx` (Sidebar link sil), `students/page.tsx`
  finans modalındaki **paket sat/uygunluk** bölümü kaldırılır (ödeme/borç kalır), `book-classes` paket göstergeleri (zaten kapanıyor).
  `studioApi.ts` paket/self-book servisleri dormant (silinmez, çağrı kalmaz).

## Etkilenecek dosyalar (özet)
**Backend:** `BookingCreator.cs`, `StaffCreateBooking.cs`, `StudentBook.cs` + `StudentBookingsController.cs`, yeni
`ContractResolver.cs` (+ DI). (AttendanceMarker/StudentPackageConsumer'a dokunmadan — dormant.)
**Frontend:** `Sidebar.tsx` (nav), `book-classes/page.tsx`, `package-plans/page.tsx`, `students/page.tsx` (finans
modalı paket bölümü), `studioApi.ts`.
**Test:** `e2e-seed.sh`, `sprint4/5/6/8/9-smoke` (booking), `sprint7-smoke`+`sprint14-smoke` (paket), `security-audit-smoke`,
E2E `03-schedule`/`04-student`/`10-package-v2`, yeni `sprint18b-smoke.sh`.
**Docs:** bu spec.

---

## Hangi testler kırılır + nasıl taşınır

| Test | Neden kırılır | Taşıma |
|---|---|---|
| `e2e-seed.sh` | Demo öğrenci booking'i paket/sözleşmesiz | Öğrenciye **sözleşme** oluştur (18a seed'inde grup/bireysel zaten var) ve demo "yaklaşan seans + rezervasyon" + "geçmiş seans + yoklama"yı **bu sözleşmeden** booking'le |
| `sprint4/5/6/8/9-smoke` | Sessions açıp öğrenci booking'liyor (paket/sözleşmesiz) | Her booking öncesi öğrenciye uygun **sözleşme** oluştur (helper); staff booking auto-resolve ile çalışır |
| `sprint7-smoke` (paket) | Paket sat + kredi + booking eligibility | **Deprecate** (paket modeli dormant). Paket CRUD endpoint'i hâlâ yanıt veriyor → "paket endpoint'leri dormant yanıt veriyor" minimal smoke'a indirgenebilir veya skip |
| `sprint14-smoke` (paket v2) | Booking package eligibility/snapshot | Eligibility/booking kısmı **deprecate**; sözleşme karşılığı `sprint18b-smoke`'ta. Fiyat-versiyonu kısmı paket CRUD olarak kalabilir (dormant) veya deprecate |
| `security-audit-smoke` | Booking + tenant izolasyon | Booking yapan yerlere sözleşme ekle; tenant/izolasyon assert'leri korunur |
| E2E `04-student` | Öğrenci self-booking testi | **Self-booking kapalı** assert'ine çevir (410/buton yok); öğrenci yalnız kendi programını/davetini görür |
| E2E `10-package-v2` | Paket UI (sat/uygunluk) | Paket UI kaldırıldı → **deprecate** veya "paket kartı görünmüyor" assert'ine çevir |
| E2E `03-schedule` | Booking/attendance | Staff booking sözleşme auto-resolve ile çalışır; seed sözleşme booking'i ürettiği için yeşil kalır (gerekirse küçük güncelleme) |

> İlke (kullanıcı kuralı): testleri **sırf yeşil yapmak için anlamsızlaştırma**. Paket smoke'ları gerçekten dormant'sa
> deprecate işaretle; booking smoke'ları sözleşmeye **gerçekten** taşı (sözleşme oluştur → booking).

---

## Acceptance criteria (18b sonunda kanıtlanacak)
- Regular booking **sözleşmesiz → 409**; aktif uygun sözleşme varsa **200**.
- Sözleşme **tarih aralığı dışı → 409**; **ders tipi uyuşmaz → 409**; **ders hakkı dolu → 409**.
- Aynı öğrenci çakışan derse **yazılamaz**; aynı hoca çakışan derse **yazılamaz**; **farklı hoca aynı saat → 200**.
- Bireysel derse 2. öğrenci **yazılamaz**; grup derse **birden çok öğrenci**.
- Öğrenci **self-booking kapalı** (410 + UI yok).
- Paket gate yeni booking akışında **kullanılmıyor** (ExpectedStudentPackageId null; PackageEligibilityChecker çağrılmıyor).
- Eski paket smoke/E2E **sözleşmeye taşındı veya deprecated** (anlamsızlaştırılmadan).
- **Tüm smoke yeşil**, **E2E yeşil**, **full compose validation yeşil**.

## Kapsam dışı (Faz 2+)
Yoklama "hepsini onayla", hoca değişikliği kullanımı, telafi tam revamp, closure→telafi, drag/drop, zincir-kaydır,
sözleşme uzatma/borç, hoca raporu, **package tablolarını DB'den silme**, büyük refactor.

## Riskler
- **En büyük risk — test rework genişliği:** 5 booking smoke + 2 paket smoke + security-audit + e2e-seed + 3 E2E.
  Invite-only/Sprint 18a reworklarına benzer ama daha geniş; her booking testine sözleşme eklemek mekanik ama hacimli.
- **Auto-resolve belirsizliği:** öğrencinin birden çok uygun sözleşmesi varsa deterministik seçim (en erken biten) — net kural.
- **Makeup/Trial/Special istisnaları:** Regular zorunluyken bunların sözleşmesiz çalışmaya devam ettiği kanıtlanmalı.
- **Attendance ↔ paket:** contract booking'lerde paket-consume no-op olmalı; attendance UI'da paket-consume seçeneği
  contract booking'lerde görünmemeli (Faz 2 tam ele alır; 18b'de en azından kırılmamalı).
- **Self-cancel kararı:** öğrenci kendi rezervasyonunu iptal edebilmeli mi? (açık soru — öneri: hayır, 410.)
- **Paket smoke'larını "deprecate" vs "taşı" dengesi:** anlamsızlaştırmadan; paket CRUD dormant kalır.

## Açık sorular
1. Öğrenci **self-cancel** de kapansın mı (410) yoksa kendi rezervasyonunu iptal edebilsin mi? (Öneri: kapansın.)
2. Paket smoke'ları (sprint7/14): **deprecate/skip** mi, **minimal "endpoint dormant" smoke**'a mı indirgensin? (Öneri: deprecate + sözleşme karşılığını sprint18b-smoke'a koy.)
3. `book-classes` sayfası: tamamen **kaldır** mı, "stüdyo sizi ekler" **bilgi ekranı** mı? (Öneri: bilgi ekranı + Sidebar'dan link sil.)
4. Trial/Special seanslar üründe kullanılıyor mu — istisna gerçekten gerekli mi, yoksa onlar da mı kapansın? (Öneri: istisna kalsın, dokunma.)

## Doğrulama planı
1. Backend build 0/0 (migration **yok** — 18b davranış değişikliği, şema değişmez).
2. Yeni **`sprint18b-smoke.sh`**: tüm acceptance criteria (sözleşmesiz 409, uygun→200, tarih/ders tipi/hak 409,
   çakışmalar, bireysel/grup, self-booking 410, paket gate yok, auto-resolve).
3. Test scaffolding taşı → **tüm smoke suite yeşil** (paket smoke'ları deprecated/taşınmış).
4. e2e-seed + E2E güncelle → **E2E yeşil** (04 self-booking-kapalı, 10 paket-UI-yok).
5. Frontend tsc 0 + next build 0.
6. **Full compose validation:** config dev/prod, up --build, health, same-origin, compose smoke, compose E2E.
7. Audit: git status, yasak desen yok, gerçek secret yok, **kullanıcıya 9999 görünmüyor** (18a koruması bozulmadı).

---

## Sonuç — Uygulama & Doğrulama (UYGULANDI)

**Build:** Backend compose image build exit 0 (migration **eklenmedi**; son migration `Sprint18_StudentContractFoundation`=18a).
Frontend `tsc --noEmit` 0 + `next build` exit 0.

**Smoke:** `run-smokes.sh` → **20/20** (hem native dev backend hem **compose** ortamında). Yeni `sprint18b-smoke` **16/16**.

**E2E (compose, frontend :3001 same-origin):** **47/47** (chromium 45 + mobile 2). 04-student self-book/cancel→410,
03-schedule rezervasyon modalı (E2E Student), 10-package-v2 paket-deprecation, 15-contracts (9999 gizli) dâhil.

**Full compose validation (gerçek):** `config` dev/prod exit 0 → `up -d --build` → postgres healthy → backend `/health` 200
→ frontend 200 → same-origin (`/health` proxy 200, `/api/auth/login` proxy 401) → `e2e-seed` → **compose smoke 20/20**
→ **compose E2E 47/47** → `down -v` + ortam restore. (Port 3000 harici bir süreçte olduğu için frontend host :3001'e
remap edildi; container içi same-origin proxy değişmedi.)

**Audit:** migration eklenmedi · package tablolarına DROP yok (yalnız dormant) · DI'ye yalnız `ContractResolver` eklendi
· `BookingCreator` Regular akışında `PackageEligibilityChecker`/`StudentPackageConsumer` **yok**, `ExpectedStudentPackageId`
null · kullanıcıya **9999 görünmüyor** (`isUnlimitedCapacity` ile gizli) · Sidebar'da `book-classes`/paket nav yok · gerçek secret yok.

**Yan bulgu (18b dışı):** `sprint3-smoke` tarihe-bağlı latent bug — `$DAY=bugün+3` Pazartesi'ye denk gelince sabit
"Monday" availability ile çakışıyordu; `$OTHERDAY=bugün+4` ile düzeltildi (18b ile ilgisiz, fırsatçı fix).

**Commit/push:** YAPILMADI (onay bekliyor).

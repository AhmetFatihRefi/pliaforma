# SPRINT 22 — Group Schedule Change & Student Reassignment (SPEC)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md) · **Faz 4 (ikinci dilim).** Sprint 21'de tek ders taşıma + zincirleme
> kaydırma geldi. Bu sprint: **bir grubun ders düzeni köklü değişince, ÖĞRENCİLERİ TEK TEK seçerek** yeni gün/saat
> düzenine taşımak (gelecek Regular dersleri telafisiz iptal + yeni slotlardan yeniden üret, önizlemeli + atomik).
> **Bu tur yalnız spec.** Kod/migration/commit YOK.

## Ana ürün kuralı
**Grup dersi ayrılmaz bütün DEĞİLDİR.** Bir grup değişecek diye tüm öğrenciler körlemesine taşınmaz; stüdyo sahibi
**hangi öğrencilerin** yeni düzene geçeceğini tek tek seçer. Her öğrencinin sözleşme tarihi, kalan hakkı ve mevcut
dersleri **ayrı** hesaplanır.

---

## Mevcut durum — analiz (kanıt) + KRİTİK CEVAPLAR

### "Grup" nasıl temsil ediliyor? → **Group/Series entity YOK (implicit)**
- Domain'de **Group/ClassGroup/ScheduleSeries/GroupSeries/Series/Cohort entity'si YOK** (tüm `Domain/Entities` tarandı).
- Bir "grup dersi" = **aynı `ClassSession` üzerinde birden çok öğrencinin booking'i**. Grup kimliği türetilir:
  **aynı `InstructorProfileId` + aynı `ClassTypeId` + aynı `StartAt` + `!IsIndividual`**. `ClassSession`'da
  `GroupId/SeriesId` YOK. ([ClassSession.cs](../src/Pliaforma.Domain/Entities/ClassSession.cs): `IsIndividual`,
  `Capacity` 9999 = grup-sınırsız sentinel.)

### GenerateContractSchedule — reuse mantığı (anahtar referans)
[GenerateContractSchedule.cs](../src/Pliaforma.Application/Features/Contracts/GenerateContractSchedule.cs)
`GenerateContractScheduleCommand(ContractId, Slots[{DayOfWeek, StartTime}], ReuseExistingGroup, Individual?)`:
- Sözleşme `StartDate..EndDate` arası, slot gün/saatine uyan adayları kronolojik gezer; **`available` dolana kadar** üretir.
- **Reuse:** `!individual && ReuseExistingGroup` ise aynı slotta **(instructor + classType + tam StartAt + Scheduled + !IsIndividual)** mevcut grup seansını bulup ona bağlanır; yoksa yeni `ClassSession` (Capacity 9999 grup / 1 bireysel) açar.
- **Skip (aday başına):** geçmiş zaman · kapanış (closure) · hoca çakışması · öğrenci çakışması.
- Sonuç: `GenerateContractScheduleResultDto(CreatedCount, NewSessionCount, ReusedSessionCount, SkippedCount, CreatedStartAts)`.
- **Önizleme zaten VAR:** `POST contracts/{id}/schedule/preview` → `ContractSchedulePreviewDto(availableLessonCount, plannableCount, slots[{startAt, endAt, plannable, skipReason}])` — **ama UI'da kullanılmıyor.**

### Sözleşme available-lesson hesabı
`AvailableLessonCount = TotalLessonCount − COUNT(Booked + Regular bookings of this contract)`
([StudentContractFeature.cs](../src/Pliaforma.Application/Features/Contracts/StudentContractFeature.cs)). **Makeup düşmez.**
→ Bir Regular booking iptal edilince (status Booked→Cancelled) **available geri yükselir.**

### Booking iptali TELAFİSİZ mi? → **Regular için EVET (telafi yok)**
[CancelBooking.cs](../src/Pliaforma.Application/Features/Bookings/CancelBooking.cs): yalnız booking bir makeup
**Used** ettiyse Restore yapar. **Düz Regular booking iptali telafi ÜRETMEZ** (status CancelledByStudio, koltuk
`ReleaseSeatAsync` ile −1). → Köklü değişiklikte gelecek Regular booking'leri kaldırmak için tam istediğimiz davranış.

### Boş session davranışı
Son booking iptal olunca **hiçbir otomatik iptal/silme YOK** — session `Scheduled` + `BookedCount=0` kalır.
`DeleteClassSessionCommand` var (yalnız yönetim yetkisi kontrolü; soft-delete) ama otomatik tetiklenmiyor.

### Atomik transaction → **tek komut = tek transaction**
[TransactionBehavior](../src/Pliaforma.Application/Common/Behaviors) her `ICommand<>`'i **tek DB transaction'ına**
sarar (başarı→commit, hata→rollback). → **Tek bir yeni komut** içinde tüm seçili öğrencileri işleyip **tek
SaveChanges** ile atomik all-or-nothing sağlanır. **DİKKAT:** GenerateContractSchedule'ı öğrenci başına AYRI komut
olarak çağırmak ayrı transaction'lar açar (atomik DEĞİL) → çekirdek mantık **servise çıkarılıp** tek komutta döngüyle çağrılmalı.

---

## Migration — GEREKMİYOR (gerekçe)
**Yeni Group/ScheduleSeries entity'si GEREKMEZ; migration YOK.** Çünkü:
1. **Grup implicit** ve operasyon **sözleşme-bazlı**: seçili her öğrenci için "gelecek Regular booking'leri kaldır +
   yeni slotlardan yeniden üret" — kalıcı bir "seri" kaydına ihtiyaç yok.
2. **Çok-öğrencili reuse** mevcut mantıkla çözülür: öğrenciler tek transaction'da SIRAYLA işlenir; ikinci öğrenci,
   birincinin yeni slotta açtığı grup seansını **(instructor+classType+StartAt)** ile **reuse** eder (ReuseExistingGroup=true).
3. **Atomiklik** TransactionBehavior ile sağlanır (tek komut).

**Bir ScheduleSeries entity'si NE kazandırırdı (ama bu sprintte gerekmiyor):** seri kimliği (UI'da "bu dersler aynı
seriye ait" göstermek), grup-değişikliği **undo**'su, seri-bazlı raporlama. Bunlar olmadığında oluşabilecek "hata"
DEĞİL, yalnız **eksik özellik**tir (undo/seri-görünürlük yok). Bu sprint kapsamı bunları gerektirmiyor → **migration'sız ilerlenebilir.**
**Eğer ileride seri-undo / seri-görünürlük istenirse** `ClassSession`/`ClassBooking`'e `ScheduleSeriesId` + bir
`ScheduleSeries` tablosu gerekir (o zaman dur+raporla). Bu sprintte **kapsam dışı**.

---

## Sprint 22 kapsamı (UYGULANACAK)

### 1) Grup + öğrenci seçimi (sözleşme-anchored)
- Yeni ekran/modal "Grup düzeni değiştir" (Owner/StudioAdmin): **hoca + ders tipi** seçilir → o hoca+ders tipinde
  **gelecekte Booked Regular dersi olan aktif sözleşmeler** (öğrenciler) listelenir (ad, kalan ders, sözleşme tarih
  aralığı, mevcut slot özeti). Kullanıcı **tek tek seçer**. Seçilmeyenler dokunulmaz.
- Yeni query `GetGroupChangeCandidatesQuery(instructorProfileId, classTypeId, effectiveDate)` → aday sözleşme listesi.

### 2) Effective date + yeni slotlar
- Kullanıcı **başlangıç tarihi (effectiveDate)** + **yeni slotlar** (`[{dayOfWeek, startTime}]`) seçer.
- Etkilenen dersler: **yalnız `effectiveDate`'ten sonraki, future, Scheduled, Regular** dersler. Geçmiş/Completed/
  Cancelled/Makeup/Trial/Special **etkilenmez**.

### 3) Önizleme (ŞART — apply'dan önce)
- Yeni query `PreviewGroupScheduleChangeQuery(instructorProfileId, classTypeId, effectiveDate, contractIds[], newSlots[])`
  → seçili her sözleşme için **dry-run** (DB'ye yazmadan simülasyon): 
  - kaldırılacak gelecek Regular ders sayısı + boş kalacak session sayısı;
  - üretilecek yeni ders sayısı + tarih/saatler; her slot **reuse** mi **yeni session** mı;
  - çakışmalar (hoca/öğrenci), sözleşme tarih aralığı dışı kalan slotlar, ders hakkı yetmeyen/üretilemeyen, availability uyarıları;
  - `canApply` (öğrenci-bazlı + genel).
- **Simülasyon doğruluğu:** çok-öğrencili reuse'u doğru göstermek için simülasyon, üretilecek seansları **bellek-içinde
  biriktirip** sonraki öğrencilerin reuse'unu hesaba katar (gerçek apply ile aynı sıra). *(Açık soru #2: tam simülasyon
  mu, öğrenci-bağımsız basit mi.)*

### 4) Apply (atomik)
- Yeni komut `ApplyGroupScheduleChangeCommand(instructorProfileId, classTypeId, effectiveDate, contractIds[], newSlots[])`
  (Owner/StudioAdmin, `IStudioWriteRequest` → tek transaction):
  - Seçili her sözleşme için: (a) `effectiveDate`'ten sonraki **Booked Regular** booking'leri **iptal** (CancelledByStudio,
    **telafi YOK**, koltuk release); (b) **GenerateContractSchedule çekirdeği** (servise çıkarılmış) ile yeni slotlardan
    yeniden üret (reuseExistingGroup=true → taşınan diğer öğrencilerle aynı yeni seansı paylaşır).
  - **Tek SaveChanges → atomik.** Herhangi bir öğrenci için **hata (exception)** olursa **tüm işlem rollback** (yarım
    taşıma olmaz); kullanıcıya neden döner. **Soft-skip** (çakışma/geçmiş/tarih-aralığı → o slot üretilmez) hata değildir,
    önizlemede/raporda gösterilir (GenerateContractSchedule semantiği).
- **Telafi üretmez** (iptal/işlenmedi/closure DEĞİL — program düzeni değişikliği). Makeup ledger'a yazmaz.

### 5) Eski derslerin durumu (karar)
- Seçili öğrenci, `effectiveDate` sonrası gelecek Regular booking'lerinden **çıkarılır** (telafisiz).
- Session'da **başka öğrenci kaldıysa** → session **devam eder** (sadece o öğrencinin booking'i Cancelled).
- Session **tamamen boş kalırsa** (BookedCount=0) → **Scheduled kalır, otomatik silinmez** (öneri — yıkıcı sürpriz yok;
  önizleme "N session boşalacak" der; kullanıcı dilerse sonra manuel siler/iptal eder). *(Açık soru #1: boş session'lar
  otomatik iptal edilsin mi.)*

### 6) Yeni ders üretimi (GenerateContractSchedule reuse)
- **Çekirdek mantık servise çıkarılır** (`ContractScheduleEngine` benzeri) → hem mevcut tekil generate hem yeni grup-değişikliği
  komutu çağırır (kod tekrarı yok). Öğrenci-bazlı: kalan hak (`available` iptal sonrası yükselir), sözleşme tarih aralığı,
  bireysel/grup kuralı korunur; aynı yeni slotta grup seansı varsa reuse, yoksa yeni session.

### 7) Yetkiler
Yalnız **Owner/StudioAdmin** (query + komut). Instructor **yapamaz** (köklü, çok-ders/çok-öğrenci etkisi). Student yapamaz.

---

## Endpoint / komut envanteri (yeni)
`GET /api/studio/group-schedule-change/candidates?instructorProfileId&classTypeId&effectiveDate` ·
`POST /api/studio/group-schedule-change/preview` ·
`POST /api/studio/group-schedule-change/apply`. (Hepsi Owner/StudioAdmin.)
**Yeniden kullanılan:** GenerateContractSchedule çekirdeği (servise çıkarılır), CancelBooking mantığı (telafisiz Regular iptal), kapasite release.

## Kapsam dışı (sonraki sprintler)
Gerçek drag/drop grid · ScheduleSeries entity + seri-undo/görünürlük · closure full-undo · sözleşme uzatma/borç ·
package/rate DB drop · öğrenci self-booking · telafi expiry zorlaması.

## Riskler
- **Çok-öğrencili reuse + atomiklik:** çekirdeği servise çıkarıp tek transaction'da döngü; sıra-bağımlı reuse doğru
  olmalı (preview = apply ile aynı sıra/mantık). En büyük risk; dikkatli test (özellikle 2+ öğrenci aynı yeni slota).
- **Önizleme simülasyonu (dry-run):** DB'ye yazmadan reuse/çakışma doğru hesaplanmalı; gerçek apply'la sapmamalı.
  Alternatif (daha basit ama daha az doğru): öğrenci-bağımsız preview (cross-student reuse'u yaklaşık gösterir). *(Açık soru #2.)*
- **Boş session birikmesi:** otomatik silmeyince schedule'da boş Scheduled session'lar kalabilir → önizlemede sayısı
  gösterilir; otomatik temizlik açık soru #1.
- **Effective date sınırları:** yalnız future Scheduled Regular; Completed/Cancelled/Makeup/Trial/Special hariç — sıkı test.
- **Atomik all-or-nothing zorluğu:** tek komut/tek transaction ile sağlanır (TransactionBehavior kanıtlı). Soft-skip ≠ hata
  ayrımı kullanıcıya net gösterilmeli (yarım-başarı görünümü olmamalı).

## Açık sorular (onay bekleyen)
1. **Boş kalan eski session:** `Scheduled` bırak (öneri, yıkıcı değil) mi, yoksa otomatik **iptal** mi (Cancelled, telafisiz)?
2. **Önizleme:** tam cross-student reuse simülasyonu (öneri, doğru ama karmaşık) mı, öğrenci-bağımsız basit önizleme mi?
3. **Grup seçim anchor'ı:** **hoca + ders tipi → aday sözleşme listesi** (öneri) mi, yoksa schedule'da bir grup seansından mı başlatılsın?
4. **Ders hakkı yetmeyince / yeni slotlar kalan dersleri dolduramayınca:** best-effort üret + uyar (öneri, GenerateContractSchedule semantiği) mı, yoksa o öğrenciyi blokla mı?
5. **effectiveDate dahil mi:** `effectiveDate` günü dâhil (>=) mi, sonrası (>) mı? (Öneri: `>= effectiveDate` 00:00 — o günden itibaren.)
6. **Eski booking iptal statüsü:** `CancelledByStudio` (öneri) mi, yeni bir "moved/reassigned" reason mı? (Yeni reason → enum değeri, migration'sız; izlenebilirlik için.) *(Öneri: CancelledByStudio + cancellationReason metni; yeni enum gerekmez.)*

---

## Test / doğrulama planı

### `sprint22-smoke.sh` (yeni)
- **Aday listesi:** hoca+ders tipinde gelecek Regular dersi olan sözleşmeler listelenir; olmayan görünmez.
- **Effective date:** geçmiş/Completed/Cancelled/Makeup dersler etkilenmez; yalnız future Scheduled Regular kaldırılır.
- **Apply (tek öğrenci):** seçili öğrencinin gelecek Regular booking'leri kaldırılır (telafi YOK → bakiye değişmez);
  yeni slotlardan kalan hak kadar ders üretilir; available tutarlı.
- **Çok öğrenci + reuse:** 2 öğrenci aynı yeni slota taşınır → **tek grup seansı paylaşılır** (reuse; yeni session sayısı doğru).
- **Seçilmeyen öğrenci dokunulmaz:** non-selected öğrencinin dersleri/bookingleri aynı kalır.
- **Atomik abort:** bir öğrenci için zorla hata (ör. çözülemez) → **hiçbir şey değişmez** (tüm rollback).
- **Boş session:** tek öğrencilik eski session boşalır → Scheduled + BookedCount 0 (veya iptal — karara göre); başka öğrenci varsa session devam eder.
- **Telafi yok:** işlem sonrası makeup ledger'da AbsentEarned/NotDelivered/HolidayClosure YOK.
- **Yetki:** Instructor candidates/preview/apply → 403; Student → erişemez.
- **Önizleme = apply tutarlılığı:** preview'in "üretilecek/kaldırılacak/reuse" sayıları apply sonucuyla eşleşir.

### E2E (Playwright)
- Owner "Grup düzeni değiştir" akışı: hoca+ders tipi → öğrenci listesi → seç → yeni slot + effectiveDate → **Önizle**
  (kaldırılacak/üretilecek/çakışma) → **Uygula** → schedule'da yeni günlerde dersler.
- e2e-seed: grup-değişikliği için aynı slota 2 öğrencili bir grup + sözleşmeler (opsiyonel).

### Full compose validation (commit öncesi, gerçek)
config dev/prod exit 0 → up --build → healthy → backend/frontend 200 → same-origin → e2e-seed → **compose smoke
(23+1) yeşil** → **compose E2E yeşil** → down -v + ortam restore.

### Audit
git status · secret/yasak desen yok · **migration yok** (varsa DUR) · package/rate DROP yok · 9999 UI gizli ·
grup-değişikliği telafi üretmiyor · seçilmeyen öğrenci dokunulmuyor · atomik (yarım taşıma yok) · effective date sınırları doğru.

---

## İş sırası (onaylanınca)
1. Backend: GenerateContractSchedule çekirdeğini **servise çıkar** (`ContractScheduleEngine`), mevcut komut + yeni komut paylaşsın (davranış aynı).
2. Backend: `GetGroupChangeCandidates` query.
3. Backend: `PreviewGroupScheduleChange` (dry-run simülasyon, cross-student reuse).
4. Backend: `ApplyGroupScheduleChange` komutu (telafisiz iptal + çekirdek üretim, tek transaction atomik).
5. Backend build 0/0 (migration yok) + has-pending doğrulama.
6. Frontend: "Grup düzeni değiştir" akışı (öğrenci seçimi + slot + effectiveDate + önizleme + uygula).
7. Frontend tsc 0 + next build 0.
8. Test: sprint22-smoke + E2E + e2e-seed + tüm suite yeşil + full compose validation + audit.
9. Dürüst rapor. **Commit/push YOK** (ayrı onay).

---

## Sonuç — Uygulama & Doğrulama (UYGULANDI)

**Kararlar (uygulandı):** 1) Boş eski session telafisiz **Cancelled**. 2) **Tam cross-student reuse** (dry-run+rollback
= apply ile birebir). 3) Anchor **hoca+ders tipi → aday sözleşme listesi**. 4) Best-effort üret + uyar; hard çakışma
(hoca/öğrenci) → blok. 5) effectiveDate **dâhil (>=)**. 6) `CancelledByStudio` + reason metni (yeni enum yok).

**Migration:** YOK — `has-pending-model-changes` → "No changes". Group/Series entity gerekmedi; operasyon sözleşme-bazlı,
mevcut `GenerateContractSchedule` çekirdeği ortaklaştırıldı, atomiklik `TransactionBehavior` ile.

**Mimari:** `ContractScheduleEngine`'e paylaşılan helper'lar çıkarıldı (generate + group-change ortak; mevcut generate
davranışı aynı — sprint15/18a smoke yeşil). **Preview = DRY-RUN:** apply çekirdeği transaction içinde çalıştırılıp
**ROLLBACK** ediliyor → preview ile apply **birebir** (reuse/kaldırma/üretim sayıları). Apply tek komut = tek transaction
(atomik); `canApply=false` → exception → tüm rollback (yarım taşıma yok).

**Build:** Backend 0/0 (compose image exit 0). Frontend tsc 0 + next build 0.

**sprint22-smoke:** **22/22** — candidates; preview dry-run (DB değişmez); apply (telafisiz kaldırma, üretim, cross-student
reuse BookedCount=2, seçilmeyen dokunulmaz, boş session Cancelled, telafi yok); atomik abort (çakışmada hiçbir şey
değişmez); yetki (Instructor 403).

**Smoke suite:** **24/24** (dev + compose). **E2E:** **53/53** (compose; 03 "Grup düzeni" modalı dâhil).

**Full compose validation:** config dev/prod exit 0 → up --build → healthy → backend/frontend 200 → same-origin
(login 401) → seed → compose smoke 24/24 → compose E2E 53/53 → down -v + restore.

**Audit:** migration yok · package/rate DROP yok · GroupScheduleChange makeup ledger'a yazmıyor (telafi yok) ·
Owner/StudioAdmin gate · 9999 UI gizli korundu.

**Commit/push:** YAPILMADI (onay bekliyor).

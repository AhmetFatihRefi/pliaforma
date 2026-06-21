# SPRINT 28C — SPEC: Scheduling / Booking / Makeup Line-Based + Individual Hard Rule + Student Level Filter

> Durum: ONAYLI (prensipte) — **iki alt faza bölündü**. 28C-1 uygulanır; 28C-2 ayrı turda.
> Zemin: 28A line modelini + 4 nullable FK (`Payment/ClassBooking/MakeupCreditTransaction/MakeupRequest.StudentContractLineId`)
> + `StudentProfile.Level` + `StudentContractLine` kurdu (backfill dolu). 28B ödeme/borç'u line-bazlı yaptı (v1.4.0-pilot).
> 28C program + booking + telafi + telafi talebini line-bazlı yapar.
> Kapsam dışı (tüm 28C): due-date reminder (28D), online ödeme, fatura/e-arşiv, drag/drop grid, ClassType kolon drop,
> ContractAmendment drop, complex installment.

---

## 0. Faz ayrımı (NET)

### Sprint 28C-1 — Regular Scheduling / Booking Line-Based + Individual Hard Rule + Student Level Filter  ← **ŞU AN**
- `ContractLineResolver` + `ContractLineEligibilityChecker` (instructor eşleşmesi ZORUNLU).
- Line-bazlı schedule engine + `POST /api/studio/contract-lines/{lineId}/schedule/preview|generate`.
- Eski header schedule endpoint → **deprecated wrapper** (silinmez; aktif line'lar üzerinde döner, geri uyum).
- `BookingCreator` Regular yolu `studentContractLineId` destekler; `ClassBooking.StudentContractLineId` yazılır.
- Individual line hard rule: grup dersine atanamaz; line'dan doğan ders `IsIndividual=true`; group generation individual line'ı grup içine koymaz.
- `GroupScheduleChange` lineId ile çalışır.
- Regular reschedule / chain-shift **line date-range hard block** (`< StartDate` veya `> EndDate` → 409).
- Student level **opsiyonel** filtre (öğrenci-seçim uçları).
- Frontend: LineCard "Programı oluştur"; StaffBookingModal line seçici/etiket; GroupChange line seçimi; level filtreler.
- `sprint28c1-smoke.sh` + E2E güncelleme.

### Sprint 28C-2 — Makeup Ledger / Makeup Request / Makeup Booking Line-Based  ← **SONRA (kod yazma)**
- `MakeupCreditService` line-scoped (balance/available/Grant/Use/AddAbsenceLedger lineId).
- Yeni akışta `MakeupCreditTransaction.StudentContractLineId` zorunlu.
- `AttendanceMarker` AbsentEarned cap = `line.MakeupAllowance` (booking.StudentContractLineId ile).
- `MakeupRequest.StudentContractLineId` zorunlu; pending reserve line-bazlı; eligible-students line-bazlı.
- Approve: `session.IsIndividual = line.IsIndividual` (hardcode `true` kalkar); `Use(line, −1)`.
- Staff doğrudan makeup booking: line seçimi zorunlu (çoklu uygun line varsa 409/seçim; tek ise fallback).
- Makeup dersi `line.EndDate` sonrasına taşınabilir (line-aware).
- Student/instructor makeup UI line etiketleri.
- `sprint28c2-smoke.sh`.

**28C-1, makeup davranışını DEĞİŞTİRMEZ** (Sprint 27 akışı 28C-1'de aynen korunur; regresyon).

---

## 1. Açık soru kararları (onaylı)

1. **Generate endpoint:** Yeni line endpoint kaynak; eski header endpoint **deprecated wrapper** olarak kalır
   (aktif line'lar üzerinde döner, geri uyum; UI yeni endpoint'i kullanır; cleanup sonraki sprint).
2. **Line resolver instructor ZORUNLU:** uygun line = `studentProfileId` + `classTypeId` + `sessionDate ∈ [StartDate,EndDate]`
   + `Status=Active` + `available>0` + **`line.InstructorProfileId == session.InstructorProfileId`**. (Line = "bu öğrencinin şu
   ders tipi + şu hoca ile aldığı hak"; başka hocanın dersine otomatik bağlanmaz. Hoca değişimi → actual-instructor/manager akışı, ayrı.)
3. **Makeup IsIndividual = line.IsIndividual** → **28C-2** konusu. 28C-1'de makeup hardcode davranışı KORUNUR.
4. **Direct staff makeup booking line seçimi** → **28C-2** konusu. 28C-1'de makeup booking davranışı KORUNUR.
5. **Regular line date-range HARD BLOCK:** Regular ders `line.StartDate` öncesine / `line.EndDate` sonrasına taşınamaz;
   reschedule + chain-shift + group-change kontrol eder. Makeup `line.EndDate` sonrası olabilir (28C-2). Geçmiş-zaman kontrolü kalır.
6. **Alt-fazlama: evet** (28C-1 / 28C-2; yukarıdaki kapsam).
7. **Legacy null-line booking fallback (28B paid deseninin aynısı):** `ReservedRegular(line)` ve `available` hesabında —
   `Booking.StudentContractLineId == line.Id` say; **+** `StudentContractLineId==NULL && StudentContractId==line.StudentContractId`
   ise **yalnız header altında TAM 1 active line varsa** o line'a say; **2+ line varsa hiçbir line'a otomatik sayma**; çift sayım ASLA.

---

## 2. Mevcut kod analizi (dosya:satır)

**Scheduling — HEADER-bazlı:** `ContractScheduleEngine.BuildCandidates(StudentContract)` (GenerateContractSchedule.cs:25);
`ReservedRegularAsync(contractId)` = `b.StudentContractId==contractId` (L59-61); `NewBooking(…contractId…)` **lineId YAZMAZ** (L80-89);
handler `contract.TotalLessonCount/IsIndividualDefault/InstructorProfileId/ClassTypeId` (L184-242). → generate'le üretilen booking
`StudentContractLineId=NULL` → 28B line DTO `ScheduledLessonCount` (`b.StudentContractLineId==l.Id`, ContractLineFeature.cs:60) bunları görmez (asıl boşluk).

**Resolver/Eligibility — HEADER:** `ContractResolver.ResolveAsync` (ContractResolver.cs:18) `StudentContracts`, en erken EndDate; **instructor eşleşmesi YOK**.
`ContractEligibilityChecker.EvaluateAsync` (ContractEligibilityChecker.cs:21) tek contractId; instructor eşleşmesi YOK.

**Booking — HEADER + kısmi individual:** `BookingCreator.CreateAsync(…studentContractId?)` (BookingCreator.cs:46); booking'e `StudentContractId`
yazar, **lineId YAZMAZ** (L112-124); individual yalnız `session.IsIndividual` → 2. öğrenci 409 (L64-70). Makeup `_credits.Use(student-scoped)` (L162).

**Group/Chain:** `GroupScheduleChangeService` (GroupScheduleChange.cs:42) `StudentContracts`+`IsIndividualDefault`+`TotalLessonCount`+`NewBooking(contractId)`.
`ChainShiftPlanner` (ChainShift.cs:21) yalnız `ClassSession.StartAt/EndAt` taşır, line linkine dokunmaz; **line-range kontrolü YOK**.

**Frontend:** `StaffBookingModal` `bookingService.create(session.id, studentProfileId)` (schedule/page.tsx:921) — sözleşme/line GEÇMEZ.
`StudentProfile.Level` uçtan uca var ama hiçbir filtrede kullanılmıyor (domain-types.ts:114-160).

---

## 3. 28C-1 — Line-based scheduling modeli

**`ContractLineScheduleEngine`** (mevcut engine'in line ikizi; ortak yardımcılar paylaşılır):
- `BuildCandidates(line, duration, slots)` → `line.StartDate..line.EndDate`.
- `ReservedRegularAsync(line)` → **legacy-fallback'li** (karar #7): `lineId==line.Id` + (tek-line header ise `lineId==NULL && contractId==header`).
- `available = max(0, line.LessonCount − reserved)`.
- `individual = line.IsIndividual` (request override YOK).
- `NewBooking` → `StudentContractLineId=line.Id` **+** `StudentContractId=line.StudentContractId`.
- `line.Status != Active` → üretim yok.
- Reuse grup seansı yalnız `!line.IsIndividual` iken.

**Endpoint:** `POST /api/studio/contract-lines/{lineId}/schedule/preview` + `/generate`
(`PreviewLineScheduleQuery(LineId, Slots)`, `GenerateLineScheduleCommand(LineId, Slots, ReuseExistingGroup)`).
Eski `GenerateContractScheduleCommand(ContractId,…)` / preview → **deprecated wrapper**: contract'ın aktif line'larını sırayla
yeni line engine'den geçirir (geri uyum). UI yeni endpoint kullanır.

---

## 4. 28C-1 — Line-based booking modeli

**`ContractLineResolver.ResolveAsync(studioId, studentProfileId, classTypeId, sessionDate, instructorProfileId)`**
(karar #2 — instructor ZORUNLU): active line'lar, classType + tarih-aralığı + available>0 + `InstructorProfileId==session.InstructorProfileId`;
deterministik **en erken biten EndDate → SequenceNo → CreatedAt**.

**`ContractLineEligibilityChecker.EvaluateAsync(lineId, studentProfileId, classTypeId, sessionDate, instructorProfileId)`**:
line aktif + öğrenci + classType + tarih-aralığı + **instructor eşleşmesi** + available>0 (legacy-fallback'li).

**BookingCreator (Regular):** imzaya `studentContractLineId?` eklenir. lineId verildi → eligibility(line); verilmedi → `ContractLineResolver`
(session.InstructorProfileId geçilir) line seçer. Sonuç: `booking.StudentContractLineId=line.Id` + `StudentContractId=line.StudentContractId`.
Mesaj: "Bu ders için uygun aktif **line** yok (hoca/ders tipi/tarih/hak)."

---

## 5. 28C-1 — Individual line hard rule

`line.IsIndividual=true` için backend zorlaması:
1. Line'dan üretilen ders `IsIndividual=true` (engine line'dan alır).
2. **Individual line grup seansına ATANAMAZ:** çözülen/seçilen `line.IsIndividual && !session.IsIndividual` → **409**
   ("Bireysel pakete (line) sahip öğrenci grup dersine eklenemez; ayrı bireysel ders gerekir").
3. Individual seansa 2. öğrenci 409 (mevcut korunur).
4. Group generation individual line'ı grup içine koymaz (per-line generate; line.IsIndividual ise reuse yok, Capacity=1).

---

## 6. 28C-1 — Group change / reschedule / chain-shift

- **GroupScheduleChange line-bazlı:** seçim birimi `lineId` (contractId→lineId); candidate/execute `StudentContractLines`
  (active, classType+instructor eşleşen); `available=line.LessonCount−reserved(line)`; `individual=line.IsIndividual`; iptal booking'ler
  `b.StudentContractLineId`; yeni booking lineId taşır; individual line grup hedefine alınmaz; **yeni slotlar line tarih aralığında olmalı**.
- **Reschedule (tekil) + Chain-shift:** taşınan **Regular** seansın yeni saati, o seansa bağlı booking'lerin line'ları için
  `line.StartDate ≤ newDate ≤ line.EndDate` olmalı; aksi → **409** ("ders sözleşme satırı tarih aralığını aşıyor"). Makeup seans muaf (28C-2'de line-aware).
  Geçmiş-zaman/hoca/öğrenci çakışma kontrolleri korunur.

---

## 7. 28C-1 — Student level filter

- Backend: öğrenci-seçim sorgularına **opsiyonel `StudentLevel? level`**. `null` → tümü; dolu → `sp.Level==level`.
  Yerler: StaffBookingModal öğrenci listesi, GroupChange candidates, (line schedule öğrenci bağlamı zaten line'dan gelir).
- DTO'lara gösterim için `Level`.
- Frontend: ilgili modallarda opsiyonel seviye dropdown ("Tümü" + `studentLevelLabels`).
- ClassType.Level KULLANILMAZ.
- **Not:** Makeup request eligible-students level filtresi 28C-2'de (modal 28C-2'de line-based olacak).

---

## 8. 28C-1 — Endpoint / UI değişiklikleri

**Backend:**
- (yeni) line schedule preview/generate; (deprecated wrapper) header schedule.
- `BookingCreator` + booking create body `studentContractLineId?`.
- GroupChange preview/apply: `contractIds` → `lineIds`.
- Reschedule/chain-shift: line-range guard.
- Öğrenci-seçim uçlarına `level` query.

**Frontend:**
- LineCard "Programı oluştur" (line endpoint); header "Programı oluştur" geri plana/deprecated.
- StaffBookingModal: line seçici/etiket (Regular) + opsiyonel level filtresi.
- GroupChangeModal: lineId seçimi + level filtresi.
- **Makeup request modalı 28C-1'de DEĞİŞMEZ.**

---

## 9. Migration

**Beklenti: YOK.** 28A FK kolonları (nullable) + Level + line tablosu + enum'lar hazır; kolonlar nullable kalır (geri uyum).
**DUR-VE-RAPORLA:** yeni kolon/composite index ihtiyacı doğarsa kod yazmadan dur, raporla, onay iste. Başta `has-pending` + index doğrula.

---

## 10. Test planı — `sprint28c1-smoke.sh`

- line generate → `booking.StudentContractLineId` yazar.
- line available lesson doğru düşer (line DTO scheduled artar).
- `line.LessonCount` aşılırsa → 409.
- Regular ders line tarih aralığı dışında → 409.
- Regular reschedule line.EndDate dışına → 409.
- Regular chain-shift line.EndDate dışına → 409.
- individual line grup derse atanamaz → 409.
- individual line seansına 2. öğrenci → 409.
- group generation individual line'ı grup içine koymaz.
- group schedule change lineId ile çalışır.
- level filter öğrenci listesini doğru süzer.
- legacy null-line booking fallback yalnız tek-line header'da çalışır.
- çok-line header'da null-line booking hiçbir line'a otomatik sayılmaz (çift sayım yok).
- **Regresyon:** v1.4.0 line payment/debt bozulmaz; v1.2.0 makeup request eski davranışı 28C-1'de bozulmaz; v1.1.2 rol hardening bozulmaz.

**E2E:** iki line'lı öğrenci → line bazlı program/booking; individual line grup'a eklenemez (UI); level filter çalışır.

**Doğrulama:** backend 0/0 · tsc/next 0 · migration NONE + has-pending No changes · smoke suite yeşil · E2E yeşil · full compose.
Commit/push/tag YOK. Yeşilse tag önerisi v1.5.0-pilot.

---

## 11. Riskler

1. **Çift sayım (en kritik):** ReservedRegular line-bazlı olunca eski header-bazlı (lineId=NULL) booking'ler — karar #7 fallback'i
   yalnız tek-line header'da sayar; çok-line'da saymaz. 28A backfill bunları doldurduğu için dev'de tutarlılık doğrulanmalı.
2. **Instructor-zorunlu resolver:** mevcut auto-booking akışında line.InstructorProfileId ≠ session.InstructorProfileId olan
   senaryolar artık 409 → eski "herhangi uygun sözleşme" davranışından sıkı; E2E/smoke'larda hoca eşleşmesi sağlanmalı.
3. **Line-range hard block:** chain-shift/reschedule sıkılaşır — mevcut taşıma testleri line aralığında kalmalı.
4. **Individual hard rule** generate+booking+group-change'in hepsinde tutarlı olmalı.
5. **Tek sprintte çok dosya** (scheduling+booking+group+chain+UI) — makeup'a DOKUNULMAZ (28C-2) → yüzey sınırlı tutuldu.

---

## 12. Önerilen uygulama sırası (28C-1)

1. `ContractLineResolver` + `ContractLineEligibilityChecker` + legacy-fallback `ReservedRegular(line)` yardımcısı.
2. Line schedule engine + preview/generate endpoint + header deprecated wrapper.
3. `BookingCreator` Regular line yolu + individual hard rule (409).
4. GroupChange line-based + reschedule/chain-shift line-range guard.
5. Student level filter (backend param + DTO + UI).
6. Frontend modallar (LineCard generate, line seçici/etiket, level).
7. `sprint28c1-smoke` + E2E + full compose doğrulama.

Yeşil uygulanırsa sonraki tag önerisi: **v1.5.0-pilot**. 28C-2 ayrı turda.

# SPRINT 19 — Attendance Confirmation, Actual Instructor & Instructor Report (SPEC)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md) · **Faz 2.** 18b'de Regular booking sözleşmeye geçti; 19 **ders gerçekleşme
> sürecini** netleştirir: günlük onay + tekil yoklama + "bugünü onayla", planlanan≠fiilî hoca, **parasız** hoca raporu,
> ve **devamsızlık→telafi** (sözleşme limitiyle, idempotent, geri-alınabilir). **Bu tur yalnız spec.** Kod/migration/commit YOK.

## Amaç (neden)
Program artık sözleşmeden üretiliyor ama "ders gerçekten yapıldı mı, kim verdi, kim geldi" akışı yarım: yoklama hâlâ
paket-kredi tüketmeye çalışabiliyor, hoca raporu **para** içeriyor (Rate/RateType), `ActualInstructorProfileId` 18a'da
eklendi ama **kullanılmıyor**, ve sözleşmedeki `MakeupAllowance` **enforce edilmiyor**. Bu sprint bunları kapatır.

---

## Mevcut durum — analiz (kanıt)

### Yoklama
- **Entity** [Attendance.cs](../src/Pliaforma.Domain/Entities/Attendance.cs): `ClassBookingId` (1:1 filtreli unique index → booking başına tek kayıt), `Status` (Present/Absent/Excused/LateCancel), `MakeupCreditTransactionId?` (telafi bağı/idempotency), `MarkedByUserId/At`, `Notes`. **Paket/ödeme tüketmez.**
- **Komutlar** [MarkAttendance.cs](../src/Pliaforma.Application/Features/Attendance/MarkAttendance.cs): `MarkAttendanceCommand(sessionId, items[])` (batch) + `MarkBookingAttendanceCommand(bookingId, status, notes, grantMakeupCredit, **consumePackageCredit**, **studentPackageId**)` (tekil). Çekirdek [AttendanceMarker.cs](../src/Pliaforma.Application/Features/Attendance/AttendanceMarker.cs) `MarkAsync`.
- **Paket kuplajı (KALDIRILACAK):** `AttendanceMarkInput.ConsumePackageCredit` true ise `AttendanceMarker` → `StudentPackageConsumer.ConsumeForAttendanceAsync` çağırıyor (yalnız Owner/StudioAdmin; Instructor 403). Sözleşme booking'lerinde `ExpectedStudentPackageId=null` → paket yolu zaten "uygun paket yok→409" kuralında. Sprint 19'da bu yol **yoklamadan tamamen çıkar**.
- **Markable kuralı** `AttendanceRules.EnsureSessionMarkableAsync`: `Status≠Cancelled` + `StartAt≤now` (gelecek→409). `canMark = StartAt≤now && Status≠Cancelled`.
- **Session.Status**: `Scheduled/Cancelled/Completed` — **`Completed` tanımlı ama kullanılmıyor.** "Confirm session" / "bugünü onayla" **YOK**.
- **Controller** [AttendanceController.cs](../src/Pliaforma.Api/Controllers/AttendanceController.cs): `GET/PUT class-sessions/{id}/attendance`, `PUT bookings/{id}/attendance`, `GET students/{id}/attendance/summary`. Öğrenci: [StudentAttendanceController.cs](../src/Pliaforma.Api/Controllers/StudentAttendanceController.cs) `GET /api/student/attendance(+/summary)` (salt-okunur).
- **Frontend**: [schedule/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/schedule/page.tsx) `AttendanceModal` (paket-consume bölümü Owner/Admin'e görünür → kaldırılacak); [my-attendance/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/my-attendance/page.tsx) (öğrenci salt-okunur); aksiyon butonları yalnız `status==="Scheduled"` iken görünüyor → **Completed için de yoklama görünmeli** (aşağıda).

### Hoca ücreti / Rate (KALDIRILACAK — dormant)
- [InstructorProfile.cs](../src/Pliaforma.Domain/Entities/InstructorProfile.cs) `Rate`, `RateType`; [InstructorClassType.cs](../src/Pliaforma.Domain/Entities/InstructorClassType.cs) `RateOverride`.
- **Para hesabı:** [DashboardCalc.cs](../src/Pliaforma.Application/Features/Dashboards/DashboardCalc.cs) `EstimateEarning(...)` → [InstructorDashboard.cs](../src/Pliaforma.Application/Features/Dashboards/InstructorDashboard.cs) + [InstructorsReport.cs](../src/Pliaforma.Application/Features/Reports/InstructorsReport.cs) çağırıyor.
- **Endpoint/komut:** `UpdateInstructorRate.cs` (`PUT /api/instructors/{id}/rate`), `InviteMember` Rate/RateType, `AssignClassType`/`UpdateAssignment` RateOverride.
- **Frontend:** [instructors/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/instructors/page.tsx) ("Ücret" sütunu, ücret düzenle, override modalı, davet Rate alanı), [dashboard/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/page.tsx) InstructorPanel "Tahmini kazanç", [reports/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/reports/page.tsx) "Tahmini kazanç" sütunu. DTO: `InstructorReportRowDto.EstimatedEarning`, `InstructorDashboardDto.estimatedEarning`.

### Fiilî hoca (substitution)
- [ClassSession.cs](../src/Pliaforma.Domain/Entities/ClassSession.cs): `InstructorProfileId` (planlanan) + `ActualInstructorProfileId?` (18a, **hiçbir yerde set EDİLMİYOR**). DTO `InstructorChanged` computed (`Actual≠null && Actual≠Planned`) — query/DTO/frontend tipinde **var ama UI'da yok, hiçbir komut set etmiyor**.

### Telafi ledger
- [MakeupCreditTransaction.cs](../src/Pliaforma.Domain/Entities/MakeupCreditTransaction.cs): `Amount` (signed), `Type` (Granted/Used/Restored/Revoked/Expired), `Reason` (enum), `SourceClassSessionId?`, `SourceBookingId?`, `UsedForBookingId?`, `ExpiresAt?`, `StudentProfileId`. **`StudentContractId` YOK.**
- [MakeupCreditService.cs](../src/Pliaforma.Application/Features/MakeupCredits/MakeupCreditService.cs): `GetBalanceAsync` (ledger toplamı), `Grant/Use/Restore/Revoke`, `HasGrantForSessionAsync` (session bazlı idempotency), `ComputeExpiry`, `ManualGrantedThisMonthAsync`.
- **Mevcut otomatik grant'lar:** session iptali (`StudioCancelledClass`/`HolidayClosure`, `HasGrantForSessionAsync` ile idempotent), yoklamada Excused/LateCancel + `grantMakeupCredit` (manuel, `Attendance.MakeupCreditTransactionId is null` ile idempotent), makeup booking iptalinde Restore.
- **Index:** `MakeupCreditTransactions` filtreli unique `(StudentProfileId, SourceClassSessionId) WHERE Type=Granted, IsDeleted=false` → **öğrenci+seans başına tek `Granted`**.
- [StudentContract.cs](../src/Pliaforma.Domain/Entities/StudentContract.cs) `MakeupAllowance` — **var, enforce edilmiyor.** [ClassBooking.cs](../src/Pliaforma.Domain/Entities/ClassBooking.cs) `StudentContractId?` (booking→sözleşme bağı).

---

## Sprint 19 kapsamı

### 1) Yoklama paketten ayrılır (Regular = sözleşme)
- **`AttendanceMarkInput` / `MarkBookingAttendanceCommand`** → `ConsumePackageCredit` + `StudentPackageId` alanları **kaldırılır**; `AttendanceMarker` artık `StudentPackageConsumer` **çağırmaz**. `StudentPackageConsumer` kodda kalır (dormant; closure/legacy yolları varsa dokunulmaz).
- **Anlam:** Present yoklama sözleşme dersinin **gerçekleştiğini** kaydeder (hak düşümü booking/program tarafında; yoklama tüketmez). DTO'daki `PackageConsumed` alanı dormant/false.
- **Frontend** `AttendanceModal`: paket-consume bölümü + paket listesi **kaldırılır** (zaten finans gizliliği için Instructor'a kapalıydı).

### 2) Ders onayı (confirm) + "Bugünü onayla"
- **Yeni komutlar** (`Features/Attendance/` veya `Features/Scheduling/`):
  - `ConfirmSessionCommand(sessionId)` — tek ders: `StartAt≤now` + `Status=Scheduled` ise `Status=Completed` yapar; **işaretlenmemiş** Booked öğrencileri varsayılan **Present** işaretler (istisnalar zaten Absent/Excused/LateCancel girilmiş olur). İdempotent: `Completed` ise no-op; gelecek seans→409/uyarı; `Cancelled`→409.
  - `ConfirmTodaySessionsCommand()` — bugünün (stüdyo-yerel) **başlamış/geçmiş**, `Scheduled`, iptal olmayan derslerini topluca confirm eder; **gelecek saatteki dersler atlanır** (sonuç DTO'da "atlanan/onaylanan" sayıları döner). İdempotent (tekrar çağrı 0 yeni onay).
- **`ClassSessionStatus.Completed`** ilk kez aktif kullanılır. **Markable kuralı korunur** (`Completed≠Cancelled` → yoklama düzeltmeye açık).
- **Frontend** schedule: aksiyon görünürlüğü `Scheduled` dışına genişler → **Completed seanslarda yoklama görüntüle/düzelt + "Tamamlandı" rozeti**; booking/ertele/iptal Completed'da gizli. "Tek ders onayla" butonu + üstte "Bugünün derslerini onayla" aksiyonu (özet: onaylanan/atlanan).
- **Yetki:** Owner/StudioAdmin tümü; Instructor yalnız kendi (planlanan **veya** fiilî) seansları.

### 3) Fiilî hoca (substitution)
- **Yeni komut** `SetActualInstructorCommand(sessionId, actualInstructorProfileId?)` — `ActualInstructorProfileId` set/temizler. **Yalnız Owner/StudioAdmin** (öneri; Instructor kendi yerine başkasını yazamasın). Dersi **iptal etmez, telafi doğurmaz**. Yalnız atıf + UI rozeti.
- **Rapor/UI atıf kuralı:** `effectiveInstructor = ActualInstructorProfileId ?? InstructorProfileId`. `InstructorChanged` zaten DTO'da; schedule kartında "Hoca değişikliği: <fiilî>" rozeti gösterilir.

### 4) Hoca raporu — **parasız & operasyonel**
- **`InstructorsReport` yeniden yazılır:** `EstimatedEarning` **kaldırılır**; `DashboardCalc.EstimateEarning` rapordan çağrılmaz (fonksiyon dormant bırakılabilir). Yeni satır DTO:
  - hoca, **ders tipi × ders sayısı** (held), **kaç öğrenciye** (held derslerde **distinct Present** öğrenci), **kaç grup / kaç bireysel** ders (`ClassSession.IsIndividual`), **planlanan≠fiilî** ders sayısı (substitution).
  - **Atıf:** her ders **fiilî hocaya** (`Actual ?? Planned`) yazılır.
  - **Tarih aralığı filtresi** (mevcut `month` → `from/to`; month geriye-uyumlu kalabilir).
- **InstructorDashboard:** "Tahmini kazanç" + `estimatedEarning`/`note` **kaldırılır**; operasyonel sayılar (held ders, present) kalır.
- **Rate UI kaldırma:** instructors sayfasında "Ücret" sütunu, ücret düzenle, RateOverride modalı, davet Rate alanı **kaldırılır/gizlenir**. `Rate/RateType/RateOverride` + `UpdateInstructorRate` endpoint **dormant** (DB drop YOK; endpoint kalır ama UI çağırmaz — paket deseni). `InviteMember` Rate paramları dormant.

### 5) Devamsızlık → telafi hakkı (sözleşme limitiyle, idempotent, geri-alınabilir)
Yalnız **devamsızlık** kaynağı (closure/iptal kaynaklı telafi = Sprint 20+). Yeni reason: `MakeupCreditReason.AbsentEarned` (enum değeri; string/int map → **migration gerektirmez**).
- **Grant (ilk kez Absent):** booking'in sözleşmesi (`booking.StudentContractId`) için **aktif devamsızlık-telafisi sayısı < `contract.MakeupAllowance`** ise `Grant(+1, reason=AbsentEarned, SourceSession, SourceBooking)`; `Attendance.MakeupCreditTransactionId` ile bağla. Limit doluysa: Absent **kaydedilir, telafi verilmez** (sessiz; UI'da "limit dolu" gösterilebilir).
  - **Limit sayımı:** sözleşmenin tüm booking'leri üzerinden `Reason=AbsentEarned` net-pozitif telafiler (join `SourceBookingId → ClassBooking.StudentContractId`). **Yeni kolon gerekmez** (StudentContractId ledger'a eklemeye gerek yok).
- **Geri alma (Absent→Present/diğer):** o booking için verilmiş devamsızlık-telafisi varsa **ters kayıt** `Revoke(-1, reason=AbsentEarned)`; `Attendance.MakeupCreditTransactionId` temizlenir. (Ledger değişmez/immutable; net 0.)
- **Tekrar Absent (önceden Granted+Revoked döngüsü):** yeni `Granted` yerine `Restore(+1)` (unique `Granted` index'i ihlal etmez — tek `Granted` korunur). İdempotent: booking'in mevcut net devamsızlık-telafisi hedefle aynıysa **no-op**.
- **Çift telafi yok:** booking başına en fazla 1 net devamsızlık-telafisi (`Attendance.MakeupCreditTransactionId` + net-state kontrolü).
- **Not:** Excused/LateCancel + manuel `grantMakeupCredit` yolu **değişmez** (mevcut davranış korunur). Telafi **tüketimi** (Makeup booking) 18b'den beri çalışıyor; bu sprintte revamp YOK.

### 6) Yetkiler (özet)
| Rol | Yoklama/onay | Actual instructor | Rapor |
|---|---|---|---|
| Owner/StudioAdmin | tüm dersler | set/temizle | görür (parasız) |
| Instructor | **yalnız kendi** (planlanan/fiilî) dersleri | **hayır** (öneri) | kendi operasyonel görünümü |
| Student | **yok** (salt-okunur kendi programı/yoklaması) | hayır | hayır |

---

## Migration
**Hedef: migration YOK.** `AbsentEarned` enum değeri (string/int map) + `Completed` mevcut enum + `ActualInstructorProfileId` mevcut alan + limit sayımı join ile → şema değişmez. **Şema gerektiren bir ihtiyaç çıkarsa (ör. ledger'a `StudentContractId` kolonu, ayrı "confirmed" flag) DUR ve raporla; kodlamaya devam etme** (18b kuralı).

## Kapsam dışı (Faz sonrası — Sprint 20+)
Closure→telafi & hoca/stüdyo-kaynaklı iptal→ekstra telafi, telafi dersi **ekleme UI'ı**, telafi tüketimi tam revamp,
drag/drop & zincirleme kaydırma, sözleşme uzatma/borç, paket/Rate **tablolarını DB'den silme**, gelir-paylaşımı modelleri.

## Riskler
- **`Completed` ilk kez aktifleşiyor:** schedule UI aksiyon görünürlüğü `Scheduled`'a kilitliydi → Completed seanslarda yoklama erişimi açılmalı yoksa onaylanan ders "kaybolur". (Tespit edildi; UI güncellemesi kapsamda.)
- **Ledger geri-alma karmaşıklığı:** Absent↔Present döngüsünde Grant/Revoke/Restore net-state mantığı + unique-`Granted` index'i; yarış için `MakeupCreditService` lock'u kullanılmalı. Net-state hesabı dikkatli test ister.
- **"Bugünü onayla" auto-Present kararı:** confirm işaretlenmemişleri Present yapar (öneri) → yanlışlıkla gelmeyen öğrenci Present kalabilir; istisnalar önce Absent girilmeli (UI akışı bunu desteklemeli). *Açık soru #1.*
- **Rate "dormant" vs "drop":** UI kaldırılır, endpoint/alan dormant kalır (öneri). Rapor/dashboard `EstimateEarning`'i çağırmayınca para tamamen kaybolur; fonksiyon dormant.
- **Limit sayımı join'i:** `SourceBookingId → ClassBooking.StudentContractId` join'i; performans için index zaten `(StudioId, StudentContractId)` var. Ledger'a kolon eklemezsek sorgu biraz daha karmaşık (kabul edilebilir).

## Açık sorular (onay bekleyen)
1. **"Bugünü onayla" auto-Present mi, sadece status mı?** Öneri: işaretlenmemiş Booked'ları **Present** yapsın + `Completed`. (Alternatif: yalnız `Completed`, attendance ayrı.)
2. **Fiilî hoca set yetkisi:** yalnız **Owner/StudioAdmin** (öneri) mi, yoksa Instructor kendi dersinde fiilî hocayı (kendisi/başkası) değiştirebilsin mi?
3. **Telafi geri-alma:** **ters kayıt (Revoke/Restore)** (öneri, immutable ledger) mi, yoksa grant'ı **soft-delete** mi? (İkisi de ledger-tutarlı; öneri counter-entry.)
4. **"Kaç öğrenciye ders verdi":** held derslerde **distinct Present** öğrenci (öneri) mi, yoksa distinct **Booked** mı?
5. **Rate endpoint'i:** UI'dan kaldır + endpoint **dormant** (öneri) mi, yoksa `410` mu?

---

## Test / doğrulama planı

### `sprint19-smoke.sh` (yeni)
- Present yoklama → sözleşme dersi tamamlanır; **`StudentPackageTransaction` OLUŞMAZ** (paket consume çağrılmıyor).
- Absent → telafi +1 (sözleşme `MakeupAllowance` içinde).
- `MakeupAllowance` aşımında Absent → **yeni telafi yok** (kayıt var, kredi yok).
- Aynı ders Absent iki kez → **çift telafi yok** (idempotent).
- Absent→Present → telafi **geri alınır** (bakiye eski değerine).
- Present→Absent (tekrar) → telafi **yeniden** (Restore), limit hâlâ korunur.
- `SetActualInstructor` → rapor **fiilî hocaya** yazar; `InstructorChanged=true`.
- ActualInstructor boş → **planlanan** sayılır.
- Hoca raporu **parasız** (earning alanı yok).
- "Bugünü onayla" **idempotent**; iptal/gelecek dersler yanlış işlenmez (atlanır).
- Yetki: Instructor başka hocanın yoklaması/onayı → 403; Student yoklama yazma → 403/yok.

### E2E (Playwright)
- Schedule: ders onayla + "Bugünü onayla" → "Tamamlandı" rozeti; Completed'da yoklama görüntülenir.
- Reports: hoca raporunda **para yok** (Tahmini kazanç sütunu YOK); ders tipi/grup-bireysel/öğrenci sayısı/substitution sütunları var.
- Instructors: **"Ücret" sütunu + ücret düzenle + RateOverride UI YOK**.
- Dashboard: instructor panelinde **"Tahmini kazanç" YOK**.
- Hoca değişikliği rozeti görünür (actual set edilmiş seansta).
- e2e-seed: seed'e fiilî-hoca + bir confirm + bir devamsızlık-telafi örneği eklenir.

### Full compose validation (commit öncesi, gerçek)
`config` dev/prod exit 0 → `up -d --build` → postgres healthy → backend `/health` 200 → frontend 200 → same-origin →
`e2e-seed` → **compose smoke (20+1) yeşil** → **compose E2E yeşil** → `down -v` + ortam restore.

### Audit
git status · yasak desen/secret yok · **migration yok** (varsa DUR) · package/Rate tablolarına **drop yok** (dormant) ·
yoklamada paket consume çağrılmıyor (`StudentPackageConsumer` Regular akışında yok) · 9999 UI gizli (18a/18b korundu) ·
Rate/earning UI'da yok.

---

## İş sırası (onaylanınca)
1. Backend: yoklamadan paket-consume çıkar (komut/DTO/marker) → build 0/0.
2. Backend: `ConfirmSession` + `ConfirmTodaySessions` + `Completed` akışı + markable korunur.
3. Backend: `SetActualInstructor` + rapor atıf (`Actual ?? Planned`).
4. Backend: InstructorsReport/Dashboard parasız yeniden yazımı + Rate UI'sız (endpoint dormant).
5. Backend: devamsızlık→telafi ledger (AbsentEarned, cap, idempotent, reversible) + lock.
6. Frontend: AttendanceModal paketsiz; confirm/bugünü-onayla; Completed görünürlüğü; actual-instructor rozeti/kontrol; reports & instructors & dashboard Rate/earning temizliği.
7. Test: sprint19-smoke + E2E + e2e-seed güncel + tüm suite yeşil + full compose validation + audit.
8. Dürüst rapor. **Commit/push YOK** (ayrı onay).

---

## Sonuç — Uygulama & Doğrulama (UYGULANDI)

**Migration:** YOK — `dotnet ef migrations has-pending-model-changes` → "No changes". `AbsentEarned` string-stored enum;
`Completed`/`ActualInstructorProfileId` zaten vardı; limit sayımı `SourceBookingId → ClassBooking.StudentContractId` join'i.

**Build:** Backend 0/0 (compose image build exit 0). Frontend `tsc` 0 + `next build` exit 0.

**sprint19-smoke:** **27/27** — Present (paket consume YOK), Absent→telafi, çift-telafi yok, Absent→Present geri alma +
restore, MakeupAllowance limiti, ConfirmSession (gelecek 409 / başlamış→Completed+Present / idempotent),
ConfirmTodaySessions, SetActualInstructor + rapor atfı, rapor PARASIZ, yetkiler.

**Smoke suite:** **21/21** (dev + compose). sprint6 (Absent artık telafi kazanır) + sprint8 (parasız dashboard/rapor) güncellendi.

**E2E (compose, :3001):** **48/48** (chromium + mobile). 02-owner (rapor parasız + Ücret sütunu yok), 03-schedule
(Bugünü onayla/Onayla/Fiilî hoca), 05-instructor (dashboard parasız) dâhil.

**Full compose validation:** config dev/prod exit 0 → up --build → postgres healthy → backend/frontend 200 →
same-origin (/api/auth/login 401) → seed → compose smoke 21/21 → compose E2E 48/48 → down -v + restore.

**Audit:** migration yok · package/Rate DROP yok (dormant) · AttendanceMarker'da `StudentPackageConsumer` YOK ·
UI'da Rate/earning yok (`rateOverride: null` yalnız dormant pass-through) · 9999 UI gizli (18a/18b korundu).

**Commit/push:** Sprint 19 onaylandı → commit `feat: add attendance confirmation and instructor operations report (Sprint 19)`, tag `v0.8.0-pilot`.

### Teknik not — devamsızlık telafi idempotency (`SourceBookingId`)
Devamsızlık-telafisi defter kayıtları (`Reason=AbsentEarned`) **booking düzeyinde** `SourceBookingId` ile izlenir;
`SourceClassSessionId` **yazılmaz** (mevcut `(StudentProfileId, SourceClassSessionId) WHERE Type='Granted'` unique
index'inden bağımsız kalmak + manuel Excused/LateCancel yoluyla çakışmamak için). İdempotency + "booking başına net
en fazla 1" kuralı, o booking'in `AbsentEarned` hareketlerinin net toplamı (Granted+Restored−Revoked) üzerinden
hesaplanır; sözleşme limiti ise `SourceBookingId → ClassBooking.StudentContractId` join'iyle sözleşme genelinde
toplanır (ledger'a ayrı `StudentContractId` kolonu eklenmedi → migration yok). **Sprint 20** (telafi dersi ekleme +
tüketim revamp) sırasında bu tasarım yeniden gözden geçirilecek (ör. ledger'a doğrudan `StudentContractId` kolonu,
expiry zorlaması, kazanım kaynaklarının birleştirilmesi).

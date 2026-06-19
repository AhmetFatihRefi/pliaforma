# SPRINT 20 — Makeup Flow, Studio Closure & Instructor-Caused Makeup (SPEC)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md) · **Faz 3.** Sprint 19 devamsızlık→telafi üretti. 20, telafinin **tam ürün
> akışını** kurar: uygun öğrenci listeleme + telafi dersi ekleme (eligibility zorunlu), tüketim/iade netleştirme,
> closure & hoca-kaynaklı işlenmeyen ders → telafi. **Bu tur yalnız spec.** Kod/migration/commit YOK.

## Amaç (neden)
Telafi hakkı üretiliyor ama **kullanım akışı yarım**: "uygun öğrenci listesi" yok, "Telafi dersi ekle" jenerik booking
modalını kullanıyor (eligibility filtresi yok), hoca-kaynaklı işlenmeyen ders için açık domain aksiyonu yok, ve
ledger görünürlüğü kaynak ders adlarını göstermiyor. Bu sprint bunları kapatır — **mevcut altyapının çoğunu yeniden
kullanarak** (riski düşük tutar).

---

## Mevcut durum — analiz (kanıt) + NE ZATEN VAR

### Telafi tüketimi (Used) — **booking anında = rezerve modeli**
- [BookingCreator.cs](../src/Pliaforma.Application/Features/Bookings/BookingCreator.cs) Makeup dalı `ConsumeMakeupCreditAsync`: **rezervasyon anında** `balance>0` ise `Use(-1)`; kredi yoksa **409** (politika `AllowMakeupBookingWithoutCreditByAdmin` + Owner/Admin → kredisiz override istisnası). **Attendance anında değil — booking anında.**
- **İptal'de iade:** [CancelBooking.cs](../src/Pliaforma.Application/Features/Bookings/CancelBooking.cs) + [SessionCancellationService.cs](../src/Pliaforma.Application/Features/Scheduling/SessionCancellationService.cs) `RestoreConsumedMakeupCreditIfAnyAsync`: bu booking için `Used` varsa ve henüz `Restored` yoksa `Restore(+1)`. **İdempotent** (alreadyRestored + `UsedForBookingId` unique index).
- **Sonuç:** Booking = **rezerve** (Used −1) · İptal = **iade** (Restored +1) · Tamamlandı = **kullanılmış kalır** (hareket yok). → Kullanıcının beklentisini (rezerve/iade/kullanıldı/çift-tüketim yok/idempotent) **bugünkü model zaten karşılıyor.** Değiştirmiyoruz; netleştirip test ediyoruz.

### Telafi sözleşme hakkından düşmez — **zaten doğru**
- Makeup booking'lerde `StudentContractId` **set edilmez** (yalnız Regular'da). `AvailableLessonCount = Total − (aktif Booked **Regular** booking)` → Makeup sayılmaz. **Telafi, TotalLessonCount/AvailableLessonCount'u azaltmaz.** Doğrulanacak + test edilecek.

### Closure → telafi — **zaten var**
- [StudioClosure](../src/Pliaforma.Domain/Entities/StudioClosure.cs) + [CreateStudioClosure.cs](../src/Pliaforma.Application/Features/Closures/CreateStudioClosure.cs): `ActionType=CancelAndGrantMakeupCredit` → aralıktaki **Scheduled** dersleri iptal eder + öğrencilere `Grant(+1, reason=HolidayClosure)`. **İdempotent** (`HasGrantForSessionAsync` (StudentProfileId, SourceClassSessionId, Type=Granted)). **MakeupAllowance'tan bağımsız** (doğrudan Grant, AbsentEarned cap yolundan değil). `RescheduleByOffset` → telafi vermez (ders yeni tarihte yapılacak — doğru). **UNDO YOK** (DELETE endpoint yok).
- [CancelClassSession](../src/Pliaforma.Application/Features/Scheduling/SessionMutations.cs) `grantMakeupCredit` → `StudioCancelledClass` reason, idempotent. Reschedule `KeepBookings=true` → telafi yok (ders taşınır).

### Hoca-kaynaklı işlenmeyen ders — **YOK (yeni)**
- `ActualInstructorProfileId` (substitution) → telafi vermez (doğru). `ClassSessionStatus`: Scheduled/Cancelled/Completed — **"işlenmedi" (not-delivered) durumu YOK**. "Gerçekten yapılamadı"yı "yapıldı ama işaretlenmedi"den ayıran açık bir alan/aksiyon yok.

### Uygun-öğrenci listesi & "Telafi dersi ekle" — **YOK (yeni)**
- `MakeupCreditService.GetBalanceAsync` (ledger toplamı; expiry zorlanmaz). **"balance>0 olan öğrenciler" sorgusu/endpoint'i YOK.** Schedule'da Makeup seansı jenerik `CreateSessionModal` (kind=Makeup) + jenerik `StaffBookingModal` ile — **eligibility filtresi yok** (backend yine de `BookingCreator`'da 409 ile zorluyor).

### Ledger görünürlüğü — **var ama kaynak adları eksik**
- Owner/Admin [makeup-credits/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/makeup-credits/page.tsx) (grant/revoke + ledger) + öğrenci [my-makeup-credits/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/my-makeup-credits/page.tsx) (salt-okunur ledger). Ledger `type/reason/amount/notes` gösterir ama **kaynak ders / kullanıldığı telafi dersi adlarını** göstermez; **kazanılan/kullanılan/kullanılabilir özeti** yok.

---

## Sprint 20 kapsamı (NE EKLENİYOR)

### 1) Uygun-öğrenci listesi + eligibility zorunlu telafi dersi ekleme
- **Yeni query/endpoint** `GetMakeupEligibleStudentsQuery` → `GET /api/studio/makeup-eligible-students` (Owner/StudioAdmin): `GetBalanceAsync>0` olan öğrenciler (id, ad, kullanılabilir telafi). (N+1 yerine tek toplu sorgu.)
- **Frontend "Telafi dersi ekle":** Makeup seansının "Öğrenci ekle" akışı **yalnız uygun öğrencileri** listeler (yeni endpoint). Jenerik `StaffBookingModal` Makeup seansta bu kaynağı kullanır (Regular'da eskisi gibi).
- **Backend zorunlu kontrol (zaten var, korunur):** `BookingCreator` Makeup dalı kredi yoksa **409** (UI filtresi tek savunma değil). Strict mod: öneri olarak `AllowMakeupBookingWithoutCreditByAdmin` **varsayılan kapalı** kalır (açık soru #1).
- **Yetki:** telafi dersi ekleme **Owner/StudioAdmin** (Instructor kapsam dışı — açık soru #2).

### 2) Hoca-kaynaklı işlenmeyen ders → ekstra telafi (yeni domain aksiyonu)
- **Yeni komut** `MarkSessionNotDeliveredCommand(sessionId, reason?)` (Owner/StudioAdmin): başlamış/Scheduled dersi **işlenmedi** olarak işaretler → dersi **Cancelled** yapar + aktif Booked öğrencilere `Grant(+1, reason=InstructorNotDelivered)` (yeni reason; **MakeupAllowance'tan bağımsız**, idempotent `HasGrantForSessionAsync`). Mevcut `CancelActiveBookingsAsync(grant=true)` yeniden kullanılır (yalnız reason farklı).
- **Açık domain ayrımı (kullanıcı kuralı):**
  - **Ertelendi** (yeni tarihte yapılacak) → `Reschedule` (KeepBookings=true) → **telafi YOK.**
  - **Başka hoca verdi** → `SetActualInstructor` → **telafi YOK.**
  - **Gerçekten yapılamadı** → `MarkSessionNotDelivered` → **+1 telafi.**
  - Bu üç yol birbirini dışlar; "işlenmedi" yalnız son durumdur. (Substitution + not-delivered birlikte olamaz: fiilî hoca varsa ders verilmiştir.)
- **Yeni reason** `MakeupCreditReason.InstructorNotDelivered` (enum değeri; string-map → **migration gerektirmez**).

### 3) Closure → telafi (mevcut) + tersine alma (dokümante)
- Closure `CancelAndGrantMakeupCredit` **korunur** (zaten idempotent + MakeupAllowance bağımsız). Bu sprintte davranış değişmiyor; **test + doğrulama** eklenir.
- **Closure tersine alma:** Tam UNDO (seans+booking geri yükleme) **Sprint 21+**. Bu sprintte: closure'ın ürettiği telafiler **izlenebilir** (reason=HolidayClosure + SourceClassSessionId) ve **mevcut revoke endpoint'iyle** geri alınabilir. Spec kararı: *"Closure otomatik geri alınamaz; telafi izlenebilir/iptal edilebilir; tam undo Sprint 21."* (Açık soru #3.)

### 4) Telafi ledger görünürlüğü (zenginleştirme)
- Owner/Admin telafi görünümünde **özet:** toplam **kazanılan** (Granted+Restored), **kullanılan** (Used), **kullanılabilir** (= bakiye). Ledger satırlarında **kaynak ders** (SourceClassSessionId → ders tipi + tarih) ve **kullanıldığı telafi dersi** (UsedForBookingId → seans) **adı** gösterilir (reason etiketi zaten var).
- Öğrenci salt-okunur görünümü aynı özet + ledger (kaynak/kullanım adlarıyla). **Öğrenci kendi telafi dersini SEÇEMEZ** (admin-güdümlü — self-booking zaten kapalı, korunur).
- Backend: `GetStudentMakeupCreditsQuery` DTO'su kaynak/kullanım **ad alanlarıyla** zenginleştirilir (ek join; şema değişmez).

---

## Telafi ledger — Sprint 19 (AbsentEarned) ile çakışma analizi
- **AbsentEarned** (Sprint 19): `SourceBookingId` ile izlenir, `SourceClassSessionId=NULL`, net-state idempotency.
- **Closure/Cancel/NotDelivered** grant'ları: `SourceClassSessionId` + `HasGrantForSessionAsync` (Type=Granted) idempotency.
- **Çakışma YOK:** (a) farklı reason'lar; (b) AbsentEarned'in SourceClassSessionId'si NULL → `HasGrantForSessionAsync`
  onu görmez (yanlış engelleme olmaz); (c) iptal/işlenmedi olan ders **Cancelled** → o derse yoklama (Absent) alınamaz
  → aynı ders için hem AbsentEarned hem closure-grant oluşamaz. **İki model güvenle bir arada.**
- **Tüketim (Used):** her iki kaynaktan gelen kredi de aynı bakiyeye girer; Makeup booking `Use(-1)` bakiyeden düşer
  (kaynağı ayırmaz — doğru, kredi krediyemdir). İade `Restore(+1)`.

---

## Endpoint / komut envanteri
**Yeni:** `GET /api/studio/makeup-eligible-students` · `POST /api/studio/class-sessions/{id}/not-delivered` (MarkSessionNotDelivered).
**Yeniden kullanılan (değişmez):** Makeup booking (`POST /api/studio/bookings`), closure (`POST /api/studio/closures`),
cancel/reschedule, makeup grant/revoke/get, student self makeup view.
**Zenginleşen DTO:** `GetStudentMakeupCreditsQuery` (kaynak/kullanım ad alanları + earned/used/available özeti).

## Migration
**Hedef: YOK.** `InstructorNotDelivered` enum değeri (string-map) + yeni query + DTO zenginleştirme (join) + UI →
şema değişmez. **Şema gerektiren ihtiyaç çıkarsa (ör. açık `NotDelivered` ClassSessionStatus, closure-undo için yeni
alan) DUR ve raporla; kodlamaya devam etme** (18b/19 kuralı).

## Kapsam dışı (Sprint 21+)
Drag/drop & zincir kaydırma, köklü grup programı değişikliği, **tek ders erteleme UX** (büyükse), **closure tam UNDO**
(seans+booking geri yükleme), sözleşme uzatma/borç, package/rate **DB drop**, öğrenci self-booking geri getirme,
telafi expiry zorlaması (DefaultCreditValidityDays hâlâ advisory).

## Riskler
- **"İşlenmedi" = Cancelled seçimi:** not-delivered dersi Cancelled yapıyoruz (ayrı status yok → migration yok). Risk:
  rapor/sayımda "iptal" ile "işlenmedi" reason ile ayrılır ama status aynı. Kabul edilebilir; gerekirse Sprint 21'de
  ayrı status. *(Açık soru #4.)*
- **Eligibility iki-savunma:** UI filtreler + backend 409. Override politikası açıksa strict-no-credit bozulur → öneri varsayılan kapalı.
- **Ledger ad join'leri:** SourceClassSessionId/UsedForBookingId → seans adı join'i; silinmiş/iptal seanslar için ad
  null olabilir (graceful gösterim).
- **Closure UNDO eksikliği:** kullanıcı beklentisi "geri alınırsa hak geri gelsin" — bu sprintte **manuel revoke** ile;
  tam otomatik undo Sprint 21. Net dokümante edilmeli (yanlış beklenti riski).

## Açık sorular (onay bekleyen)
1. **Strict eligibility:** `AllowMakeupBookingWithoutCreditByAdmin` override kalsın (varsayılan kapalı) mı, yoksa
   "telafi hakkı olmadan kesinlikle yok" için tamamen kaldırılsın mı? (Öneri: kalsın, varsayılan kapalı = strict.)
2. **Telafi dersi ekleme yetkisi:** yalnız **Owner/StudioAdmin** (öneri) mi, Instructor da kendi telafi dersine ekleyebilsin mi?
3. **Closure tersine alma:** bu sprint **manuel revoke + dokümante** (öneri) mi, yoksa minimal "closure kredilerini topluca revoke et" aksiyonu mu?
4. **"İşlenmedi" durumu:** Cancelled + reason (öneri, migration yok) mi, yoksa ayrı `NotDelivered` ClassSessionStatus (migration) mi?
5. **Hoca-kaynaklı telafi ek alanı:** reason `InstructorNotDelivered` yeterli mi, yoksa "hangi hoca yüzünden" izi de tutulsun mu (şu an gerek yok)?

---

## Test / doğrulama planı

### `sprint20-smoke.sh` (yeni)
- **Eligibility:** balance=0 öğrenci eligible listede **yok**; grant sonrası **var**. Backend: kredisiz Makeup booking → **409**; kredili → **200** + `Use(-1)` (bakiye düşer).
- **Rezerve/iade:** Makeup booking → bakiye −1; booking iptal → bakiye geri (+1); idempotent (tekrar iptal çift iade yok).
- **Sözleşme hakkından düşmez:** Makeup booking sonrası sözleşme `AvailableLessonCount` **değişmez**.
- **NotDelivered:** `not-delivered` → ders Cancelled + öğrenciye +1 (reason InstructorNotDelivered); idempotent (tekrar → çift yok); MakeupAllowance dolu olsa bile üretir (bağımsız).
- **Ayrım:** Reschedule(KeepBookings) → telafi YOK; SetActualInstructor → telafi YOK.
- **Closure:** CancelAndGrantMakeupCredit → etkilenen derslerin öğrencilerine +1 (HolidayClosure), idempotent; RescheduleByOffset → telafi YOK.
- **AbsentEarned coexistence:** sözleşmeli derste Absent (+1 AbsentEarned) + ayrı derste closure (+1 HolidayClosure) → bakiye 2, çakışma yok.
- **Yetki:** Instructor not-delivered/closure → 403; Student makeup salt-okunur (yazma yok); eligible-students yalnız Owner/Admin.
- **Görünürlük:** ledger özet (earned/used/available) + kaynak ders adı doğru.

### E2E (Playwright)
- Makeup seansında "Öğrenci ekle" → yalnız **uygun öğrenciler** listelenir.
- Schedule'da bir derse "İşlenmedi" aksiyonu → öğrenci telafi kazanır (makeup görünümünde +1).
- Owner makeup görünümü: earned/used/available özet + kaynak ders adı.
- Closure oluştur (CancelAndGrant) → etkilenen sayılar; öğrenci ledger'ında HolidayClosure satırı.
- e2e-seed: bir not-delivered + bir closure-grant + bir telafi dersi örneği eklenir.

### Full compose validation (commit öncesi, gerçek)
config dev/prod exit 0 → up --build → postgres healthy → backend/frontend 200 → same-origin → e2e-seed →
**compose smoke (21+1) yeşil** → **compose E2E yeşil** → down -v + ortam restore.

### Audit
git status · secret/yasak desen yok · **migration yok** (varsa DUR) · package/rate DROP yok · 9999 UI gizli ·
Makeup booking sözleşme hakkını düşürmüyor · eligibility backend'de zorunlu (kredisiz 409) · closure/not-delivered
idempotent · AbsentEarned ↔ closure çakışmıyor.

---

## İş sırası (onaylanınca)
1. Backend: `GetMakeupEligibleStudentsQuery` + endpoint.
2. Backend: `MarkSessionNotDeliveredCommand` + `InstructorNotDelivered` reason (CancelActiveBookingsAsync reuse).
3. Backend: `GetStudentMakeupCreditsQuery` DTO zenginleştirme (earned/used/available + kaynak/kullanım adları).
4. Backend build 0/0 (migration yok) + has-pending doğrulama.
5. Frontend: Makeup seansında eligibility-filtreli "Öğrenci ekle"; schedule "İşlenmedi" aksiyonu; makeup görünümü özet + kaynak adları; öğrenci salt-okunur özet.
6. Frontend tsc 0 + next build 0.
7. Test: sprint20-smoke + E2E + e2e-seed güncel + tüm suite yeşil + full compose validation + audit.
8. Dürüst rapor. **Commit/push YOK** (ayrı onay).

---

## Sonuç — Uygulama & Doğrulama (UYGULANDI)

**Kararlar (uygulandı):** 1) Strict eligibility — `AllowMakeupBookingWithoutCreditByAdmin` override yeni akışta
KULLANILMIYOR (dormant); kredisiz Makeup booking herkes için (Owner dâhil) 409. 2) Telafi dersi ekleme yalnız
Owner/StudioAdmin. 3) Closure undo yok (manuel revoke + dokümante). 4) NotDelivered = Cancelled + reason (migration yok).
5) Ek "hangi hoca" alanı yok.

**Migration:** YOK — `has-pending-model-changes` → "No changes". `InstructorNotDelivered` string-map enum; eligible
query + ledger DTO zenginleştirme join'le.

**Build:** Backend 0/0 (compose image exit 0). Frontend tsc 0 + next build 0.

**sprint20-smoke:** **30/30** — eligible listesi, strict 409, Use(-1)/Restore(+1), sözleşme hakkı düşmez (ContractId
NULL), Owner-only makeup add, NotDelivered (+1, allowance bağımsız, idempotent), Reschedule/ActualInstructor telafi
yok, Closure (+1 HolidayClosure, idempotent, RescheduleByOffset yok), AbsentEarned↔closure/not-delivered coexistence,
yetkiler, öğrenci salt-okunur.

**Smoke suite:** **22/22** (dev + compose). **E2E:** **51/51** (compose; 03 İşlenmedi + 16-makeup özet/salt-okunur).

**Full compose validation:** config dev/prod exit 0 → up --build → healthy → backend/frontend 200 → same-origin
(login 401) → seed → compose smoke 22/22 → compose E2E 51/51 → down -v + restore.

**Audit:** migration yok · package/rate DROP yok · BookingCreator strict (override yalnız dormant yorum) · Makeup
add Owner/Admin gate · 9999 UI gizli korundu.

**Commit/push:** Sprint 20 onaylandı → commit `feat: add strict makeup flow and not-delivered credits (Sprint 20)`, tag `v0.9.0-pilot`.

### Teknik borç — Closure UNDO (Sprint 21'de yeniden değerlendirilecek)
Bu sprintte **tam closure undo YAPILMADI** (bilinçli kapsam dışı). Bir kapanış (StudioClosure) uygulandığında
etkilenen seanslar `Cancelled` olur ve öğrencilere `HolidayClosure` reason'lı telafi verilir; **otomatik geri alma
yoktur**. Yanlışlıkla oluşturulan bir kapanış için mevcut yaklaşım:
- Kapanış kredileri **izlenebilir** (`Reason=HolidayClosure` + `SourceClassSessionId`) ve **mevcut revoke akışıyla
  manuel** geri alınabilir;
- İptal edilen seanslar/rezervasyonlar manuel olarak yeniden oluşturulmalıdır.

**Teknik borç:** Toplu/otomatik closure undo (kapanışın ürettiği kredileri topluca revoke + seans/rezervasyon
geri yükleme) yok. **Sprint 21**'de program taşıma / drag-drop / köklü program değişikliği tarafına geçilirken
closure undo ihtiyacı (ve "Cancelled vs NotDelivered ayrı status" kararı) yeniden değerlendirilecek.

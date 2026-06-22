# SPRINT 29C — SPEC: Delivered Consumption + Unconfirm + Planned/Delivered/Remaining

> v1.9.0-pilot üzerine. **Türetilmiş alanlar + session-status davranışı + unconfirm + Completed-cancel guard.**
> **Migration YOK** (yeni kolon/entity/config yok; gerekirse dur-raporla). Tag önerisi: `v1.10.0-pilot`.

## Onaylanan kararlar
- **D1 DTO:** `AvailableLessonCount` mevcut alan kalır (= AvailableForPlanning); **alias eklenmez**. Yeni: `PlannedLessonCount`, `DeliveredLessonCount`, `RemainingLessonRight`. Mevcut `ScheduledLessonCount` = Planned+Delivered (Reserved). UI'da `AvailableLessonCount` etiketi **"Planlanabilir"**.
- **D2 Completed-cancel → 409:** `session.Status==Completed` ise `CancelBooking` **409** ("İşlenmiş dersten öğrenci çıkarılamaz. Önce ders onayını geri alın."). Önce unconfirm → Scheduled → sonra cancel.
- **D3 Geçmiş seansa booking:** "Geçmiş seansa rezervasyon yapılamaz" kuralı korunur; unconfirm yalnız hak/yoklama düzeltme amaçlı.
- **D4 RemainingLessonRight = max(0, LessonCount − DeliveredLessonCount).**

## Semantik
- `ScheduledLessonCount` = Planned + Delivered = Reserved.
- `AvailableLessonCount` = **AvailableForPlanning** = LessonCount − (Planned + Delivered).
- `PlannedLessonCount` = Booked Regular, **session Scheduled**.
- `DeliveredLessonCount` = Booked Regular, **session Completed**.
- `RemainingLessonRight` = `max(0, LessonCount − DeliveredLessonCount)`.

## 1. Backend — ContractLineRules
Mevcut legacy fallback desenini AYNEN kullanan iki yeni metod:
- `PlannedRegularAsync(line)` = `Σ Booking(Booked, Regular, lineId, session.Status==Scheduled)` + (tek-line header ise null-line aynı koşul).
- `DeliveredRegularAsync(line)` = `Σ Booking(Booked, Regular, lineId, session.Status==Completed)` + (tek-line header null-line).
- `ReservedRegularAsync` / `AvailableAsync` **DEĞİŞMEZ** → booking/generate kapısı `AvailableLessonCount`/`AvailableAsync` ile çalışmaya devam. **RemainingLessonRight booking kapısı DEĞİL.**

## 2. Backend — ContractLineQuery.BuildAsync + DTO
- Mevcut `Scheduled`/`LegacyScheduled` projeksiyonunu **Planned/Delivered** (+ Legacy karşılıkları) olarak böl (session.Status'a göre).
- `ScheduledLessonCount` = Planned+Delivered (geriye uyum). `AvailableLessonCount` = LessonCount − (Planned+Delivered) (mevcut mantık). `RemainingLessonRight` = max(0, LessonCount − Delivered).
- `StudentContractLineDto`'ya **ekle:** `PlannedLessonCount`, `DeliveredLessonCount`, `RemainingLessonRight`. **Koru:** `ScheduledLessonCount`, `AvailableLessonCount`.
- Çok-line header'da null-line booking hiçbir line'a sayılmaz (legacy fallback yalnız tek-line header).

## 3. Backend — Unconfirm
- `UnconfirmSessionCommand(SessionId)` → `SessionAttendanceDto`.
  - Auth: `SchedulingAccess.EnsureCanConfirmSessionAsync` (confirm ile **aynı**).
  - `session.Status != Completed` → **409** ("Yalnızca tamamlanmış ders geri alınabilir.").
  - `session.Status = Scheduled`. **Attendance + makeup ledger'a DOKUNMA.** SaveChanges → `AttendanceViews.BuildSessionViewAsync`.
  - Zaman sınırı **yok**. Türetilmiş etki: Delivered−1, Planned+1, Remaining geri.
- Route: `POST /api/studio/class-sessions/{id}/unconfirm` (ClassSessionsController; confirm ile aynı auth attr).

## 4. Backend — CancelBooking guard
- Handler'a: booking'in seansı yüklenir; `session.Status == Completed` → **409** ("İşlenmiş dersten öğrenci çıkarılamaz. Önce ders onayını geri alın.").
- Scheduled cancel davranışı **korunur** (Booked→Cancelled, kapasite iade, makeup restore). Scheduled cancel → Planned−1, AvailableLessonCount geri, RemainingLessonRight değişmez.

## 5. Confirm (değişmez)
`ConfirmSession`/`ConfirmToday` aynı. Completed olunca Planned→Delivered (türetilmiş). Absent booking de Delivered sayılır (booking Booked kalır). Makeup booking regular Planned/Delivered/Remaining'e **girmez** (SessionKind=Makeup; bu sayımlar Regular).

## 6. UI
- **Staff LineCard** (`students/page.tsx`): **Toplam {lessonCount} · Planlanmış {plannedLessonCount} · İşlenmiş {deliveredLessonCount} · Kalan hak {remainingLessonRight} · Planlanabilir {availableLessonCount}**.
- **Student my-contracts** (`my-contracts/page.tsx`): aynı 5 sayı.
- **Tooltip:** "Kalan hak, işlenmiş derslere göre hesaplanır. Planlanabilir sayı, planlanmış ve işlenmiş dersler düşüldükten sonra kalan kapasitedir."
- **Schedule** (`schedule/page.tsx`): Completed seansta **"Onayı geri al"** (confirm yetkisiyle aynı kural — Owner/Admin; Instructor kendi/fiilî seansı) → `classSessionService.unconfirm(id)` → başarılıysa refetch.
- studioApi: `classSessionService.unconfirm(sessionId)`.

## 7. Migration
**YOK.** Hepsi türetilmiş + session.Status flip. `has-pending` "No changes" kalmalı. Yeni kolon gerekirse **dur-raporla** (beklenmiyor).

## 8. Kapsam dışı
payment · preferred slot · candidate selection · makeup candidate · due-date reminder · refund/reversal · migration.

## 9. Riskler
- AvailableForPlanning vs RemainingLessonRight karışıklığı → etiket+tooltip şart.
- Over-planning guard: booking/generate **AvailableLessonCount**'a bağlı kalmalı (RemainingLessonRight'a geçilmemeli).
- **Completed-cancel→409** yeni davranış: cancel eden mevcut smoke/E2E Completed seansta iptal ediyorsa güncellenmeli (çoğu Scheduled'da iptal eder; doğrulanacak).
- DTO additive → consumer'lar kırılmaz; `RegularCandidateDto` ayrı (etkilenmez).

## 10. Test (`sprint29c-smoke.sh`)
booking→planned+1/remaining sabit/AvailableLessonCount−1 · confirm(backdate)→delivered+1/planned−1/remaining−1/AvailableLessonCount aynı · unconfirm→delivered−1/planned+1/remaining+1 · unconfirm non-Completed→409 · **Completed booking cancel→409** · unconfirm sonrası Scheduled cancel→planned−1/remaining sabit · geçmiş unconfirmed seansa yeni booking hâlâ engellenir · over-planning guard (AvailableLessonCount) · Absent Completed→delivered · makeup booking regular sayımları değişmez · legacy tek-line null-line Planned/Delivered'a sayılır · çok-line null-line sayılmaz · staff/student UI counts. Regresyon: v1.9/v1.8/v1.7/v1.6. `run-smokes.sh`'e `sprint29c-smoke`.

## 11. Uygulama sırası
1. `ContractLineRules.Planned/DeliveredRegularAsync`. 2. `BuildAsync` split + DTO 3 alan. 3. Frontend tip + LineCard + my-contracts + tooltip. 4. `UnconfirmSessionCommand` + route + `classSessionService.unconfirm` + schedule butonu. 5. `CancelBooking` Completed→409. 6. `sprint29c-smoke` + kayıt. 7. Doğrulama (migration yok + has-pending temiz).

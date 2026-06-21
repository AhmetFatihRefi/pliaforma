# SPRINT 29 — Strict Line Payments + Preferred Lesson Days/Times + Delivered Lesson Consumption

> Bu epic v1.7.0-pilot (tam line-based) üzerine kurulur. Üç fazlıdır; her faz ayrı onaylanır.
> **Bu dosya kalıcı spec'tir. Aktif uygulama turu: SPRINT 29A.** 29B/29C aşağıda yalnız tasarım olarak yer alır
> (bu turda KODLANMAZ).

## Faz haritası
- **29A — Strict Payments + Preferred Slot data model + UI** ← *aktif tur*
- **29B — Candidate selection (regular gün/saat + nearest sort, makeup ayrı) + booking modal UI** (sonraki tur)
- **29C — Delivered consumption + unconfirm + Planned/Delivered/Remaining ayrımı** (sonraki tur)

## Onaylanan kararlar (epic geneli)
- **D1** Delivered = seans-onaylı (Completed) bazlı; Absent de tüketir; Excused/LateCancel ayrıca telafi kredisi; unconfirm → hak geri gelir. *(29C)*
- **D2** Strict payment: CreatePayment'ta line ZORUNLU (yoksa 400); "Diğer ödeme" UI'dan kalkar; eski unassigned salt-okunur, borca saymaz. *(29A)*
- **D3** Ödeme: edit (tutar/vade/yöntem/not) + cancel korunur; iade/reversal kapsam dışı (ileride ayrı sprint). *(29A kararı)*
- **D4** Preferred slot: slotsuz (eski/backfill) line aday listesine dahil, en sona sıralanır; yeni line ≥1 tercih günü/saat zorunlu; tercihi olan line'da gün = hard filtre, saat = yakınlık sıralaması. *(model 29A, kullanım 29B)*

## Açık soru kararları (kullanıcı)
1. **Reversal/iade:** Sprint 29 kapsam dışı; edit+cancel kalır; ReversePayment/refund ileride ayrı sprint.
2. **Aynı gün+saat slot tekrarı:** aynı line'da aynı `(DayOfWeek, PreferredStartTime)` tekrar EDEMEZ; `PreferredStartTime=NULL` ise aynı gün+NULL da tekrar edemez (saatsiz tercih günde bir kez).
3. **Unconfirm zaman sınırı:** 29A'da yok; 29C'de; şimdilik zaman sınırı yok.
4. **Instructor zorunlu:** 28C-1 resolver kuralı korunur (`line.InstructorProfileId == session.InstructorProfileId`); 29A'da dokunulmaz.
5. **RemainingLessonRight negatif olmasın:** `max(0, LessonCount − Delivered)`; 29C konusu, 29A'da uygulanmaz.

---

# SPRINT 29A — Kapsam (aktif)

## 1. Strict Payment (backend)
- `CreatePaymentValidator`: `StudentContractLineId` **NotEmpty** (zorunlu) → yoksa 400/validation.
- Handler (mevcut davranış korunur, sadece artık line her zaman var):
  - `StudentContractId` line'dan türetilir.
  - line öğrenciye ait olmalı; `Status != Cancelled`; payment currency line currency ile eşleşir.
  - **edit/cancel davranışı korunur** (UpdatePayment/CancelPayment değişmez).
- Geriye uyum: eski `StudentContractLineId=NULL` ödemeler **silinmez**, **borca sayılmaz** (mevcut `UnassignedByCurrency`), **yeni** unassigned **üretilemez**.

## 2. Preferred Slot veri modeli
**Tablo `StudentContractLinePreferredSlot`** (`BaseEntity`, `ITenantScoped`):
`Id, StudioId, StudentContractLineId (FK Cascade), DayOfWeek (System.DayOfWeek), PreferredStartTime (TimeOnly?), IsActive (bool=true), SequenceNo (int), Notes (string?)` + audit.
- DayOfWeek zorunlu; PreferredStartTime opsiyonel; aynı gün birden çok farklı saat olabilir; saatsiz tercih olabilir.
- **Tekillik:** aynı line içinde aynı `(DayOfWeek, PreferredStartTime)` (NULL dahil) tekrar edemez.
- Eski/backfill line slotsuz kalabilir; **yeni line ≥1 slot zorunlu**.
- **EF:** `DayOfWeek` `HasConversion<string>(16)`. Index: `(StudioId, StudentContractLineId)`, `(StudioId, DayOfWeek)`. FK Cascade.
- **Migration:** `Sprint29A_LinePreferredSlots` — additive (yalnız yeni tablo + index); mevcut tablo/kolon **drop yok**.

## 3. Slot CRUD (Owner/StudioAdmin)
- `AddLinePreferredSlotCommand(LineId, DayOfWeek, PreferredStartTime?, Notes?)` — duplicate → 409.
- `RemoveLinePreferredSlotCommand(SlotId)` — soft-delete (BaseEntity.IsDeleted) / kayıt kaldırma.
- Listeleme: `StudentContractLineDto.PreferredSlots` ile döner.
- **Instructor/Student yazamaz** (controller auth `Owner,StudioAdmin`).

## 4. CreateStudentContractLine güncellemesi
- Command'a `IReadOnlyList<PreferredSlotInput> PreferredSlots` eklenir; validator **≥1** ister; her slot `DayOfWeek` zorunlu; **duplicate engellenir** (400 — bu yığında tüm FluentValidation hataları 400 döner; 422 yok).
- İlk line'ı oluşturan akış (Create line / contract→line) da slot almalı.
- **Seed/E2E/smoke** slotsuz line oluşturuyorsa güncellenir.

## 5. DTO
- `PreferredSlotDto(Id, DayOfWeek, PreferredStartTime?, IsActive, SequenceNo, Notes?)`.
- `StudentContractLineDto.PreferredSlots: IReadOnlyList<PreferredSlotDto>` (aktif slotlar, SequenceNo sıralı).

## 6. Frontend
- **StudentFinanceModal:** "Diğer ödeme (borca sayılmaz)" seçeneği **kaldırılır**; line seçimi **zorunlu** (boşsa submit yok); aktif line yoksa "Önce sözleşme satırı ekleyin" mesajı. Eski unassigned ödeme salt-okunur görünebilir, yeni üretilemez.
- **Line kartı:** "Tercih gün/saatleri" bölümü — chip (`Pazartesi 18:00`, `Perşembe 19:00`, `Cumartesi`) + ekle/sil.
- **Line oluşturma formu:** ≥1 tercih gün/saat alanı (gün zorunlu, saat opsiyonel, çoklu); duplicate frontend'de de engellenir.

## 7. Kapsam DIŞI (29A)
regular aday sıralaması · preferred ile aday filtreleme · makeup aday query · delivered consumption · planned/delivered/remaining · unconfirm · candidate modal değişimi · payment reversal/refund · due-date reminder değişikliği. (Hepsi 29B/29C.)

## 8. Test (`sprint29a-smoke.sh`)
- lineId'siz CreatePayment → **400**; lineId ile → **200**.
- eski unassigned payment borca sayılmaz, salt-okunur kalır.
- yeni line slotsuz → **400**; ≥1 slot ile → **200**. (Mimaride FluentValidation hataları 400 döner — 422 yok; smoke 400 doğrular.)
- slot ekle/listele/sil çalışır; aynı line duplicate gün+saat → engellenir; saatsiz slot eklenebilir.
- eski/backfill slotsuz line bozulmaz; `StudentContractLineDto.PreferredSlots` döner.
- Instructor/Student slot yazamaz (403); Owner/StudioAdmin yazabilir.
- Regresyon: v1.7 reminder, v1.6 makeup, v1.5 scheduling, v1.4 payment/debt bozulmaz.

## 9. Doğrulama
backend build 0/0 · frontend tsc 0 · next build 0 · migration apply başarılı · has-pending "No changes" · sprint29a-smoke yeşil · smoke suite yeşil · E2E yeşil · full compose yeşil. Commit/tag yalnız ayrı onayla (öneri `v1.8.0-pilot`).

---

# SPRINT 29B / 29C — Tasarım (bu turda KODLANMAZ)

## 29B — Candidate selection
- `GetRegularBookingCandidatesQuery(ClassSessionId, Level?)`: aktif line + classType + instructor(28C-1) + tarih-aralığı + AvailableForPlanning>0 + **gün kuralı** (line'ın aktif slotları arasında session günü varsa geçer; slotsuz line geçer→en sona; slotu var gün yoksa elenir) + level + aynı-saat çakışma + bireysel-grup kuralı. **Sıralama:** slot eşleşen önce / saat yakınlığı (|Δdk|) / saatsiz sonra / ad. 
- `GetMakeupBookingCandidatesQuery(ClassSessionId, Level?)`: classType server-side, `GetLineAvailableBalanceAsync>0`, gün-bağımsız, çakışma elenir.
- `StaffBookingModal` sunucu candidate listelerini kullanır; regular/makeup UI ayrımı net.

## 29C — Delivered consumption
- `ContractLineRules.DeliveredAsync/PlannedAsync` (session.Status'a göre); `AvailableAsync` (=AvailableForPlanning, booking kapısı) **değişmez**.
- `StudentContractLineDto` + `DeliveredLessonCount/PlannedLessonCount/RemainingLessonRight (=max(0,LessonCount−Delivered))`; `BuildAsync` Completed/Scheduled böler.
- `UnconfirmSessionCommand(SessionId)`: Completed→Scheduled (attendance silinmez); UI "Onayı geri al".
- UI: line kartında Kalan ders hakkı / Planlanmış / İşlenmiş ayrı.

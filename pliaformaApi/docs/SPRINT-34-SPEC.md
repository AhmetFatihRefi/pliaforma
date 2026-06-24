# SPRINT 34 — Sözleşme Satırı Tercih Güncelleme + Kalan Ders Programını Yeniden Planlama

> **Durum:** SPEC — onay bekliyor. **Kod YOK · Migration YOK · Commit/push/tag YOK.**
> **Ön koşul:** Sprint 33 (v1.24.0-pilot) — opsiyonel hoca + kapasite tercihi. HEAD = `26536e9`.
> **Bu sprintte hedef:** Sözleşme satırı oluşturulup programı üretildikten sonra, **geçmişi bozmadan** hoca/kapasite/gün-saat tercihlerini güncellemek ve **kalan dersleri** yeni tercihlere göre güvenle yeniden planlamak.

---

## 0. TL;DR (kararların özeti)

1. **İki ayrı operasyon** (senin ön tercihin doğrulandı):
   - **A) Tercih güncelle** — `PUT …/contract-lines/{lineId}/preferences` → hoca/kapasite/slot kaydeder, **mevcut programa DOKUNMAZ**.
   - **B) Kalan dersleri yeniden planla** — `POST …/contract-lines/{lineId}/replan` → explicit aksiyon; gelecekteki planlı dersleri yeni tercihe göre yeniden üretir.
2. **Replan stratejisi = "detach + regenerate" (MVP, Strategy A).** Gelecekteki `Booked + Regular + Scheduled` booking'ler telafisiz iptal → boşalan seanslar Cancelled → satırın güncel slot/hoca/kapasitesiyle `RunAsync` ile yeniden üret. **Shared-group güvenliği yapısal olarak garanti** (seans taşınmaz; yalnız bu öğrencinin booking'i ayrılır; seans yalnız tamamen boşalırsa iptal).
3. **MIGRATION GEREKMEZ.** Hoca/kapasite zaten line kolonları; slotlar zaten ayrı tabloda; line→seans ilişkisi `ClassBooking.StudentContractLineId` üzerinden mevcut; iptal izi `CancellationReason`/`Notes` alanlarıyla tutuluyor.
4. **Completed/Delivered/geçmiş asla değişmez.** Yalnız `StartAt >= studioNow` ve `Status=Scheduled` olan regular booking'ler etkilenir.
5. Tüm Sprint 32 (equipment guard) + Sprint 33 (effective hoca/kapasite, EnsureFiniteForEquipment) kuralları **olduğu gibi** RunAsync üzerinden uygulanır — **yeni kural yok, mevcut motor yeniden kullanılır.**

---

## 1. Mevcut sistem analizi (kod-temelli — doğrulandı)

### 1.1 Entity'ler ve ilişkiler
- **`StudentContractLine`** (`Domain/Entities/StudentContractLine.cs`): `ClassTypeId`, `InstructorProfileId (Guid?, S33)`, `PreferredMaxCapacity (int?, S33)`, `StartDate/EndDate (DateOnly)`, `LessonCount`, `IsIndividual (S33: PreferredMaxCapacity==1'den türetilir)`, `Status (Active/…)`, `SequenceNo`, `Notes`. **Güncellenebilir alanlar zaten kolon olarak var** → migration gerekmez.
- **`StudentContractLinePreferredSlot`** (ayrı tablo, Sprint 29A): `StudentContractLineId (FK Cascade)`, `DayOfWeek`, `PreferredStartTime (TimeOnly?)`, `IsActive`, `SequenceNo`, `Notes`. **Slot güncelleme için tablo + CRUD hazır.**
- **`ClassSession`**: `InstructorProfileId`, `ClassTypeId`, `IsIndividual`, `Capacity`, `BookedCount`, `Status (Scheduled/Cancelled/Completed)`, `SessionKind (Regular/Makeup/…)`, `StartAt/EndAt`. **Seansta contract-line FK YOK** — seans paylaşılabilir; line ilişkisi yalnız booking üzerinden.
- **`ClassBooking`**: `ClassSessionId`, `StudentProfileId`, `StudentContractId? (S28A)`, **`StudentContractLineId? (S28A)`**, `Status (Booked/CancelledByStudent/CancelledByStudio/…)`, `CancelledByUserId/At/Reason`. **`StudentContractLineId` sayesinde "bu satırın booking'leri/seansları" deterministik bulunur** → **replan için yeterli ilişki MEVCUT.**

> **Kritik cevap (madde 11):** Replan için ek alan/migration GEREKMEZ. "Bu seans bu line'a mı ait?" sorusu `ClassBooking.StudentContractLineId` üzerinden cevaplanır. "Bu seans yalnız bu öğrenciye mi ait (private)?" sorusu ise (yalnız Strategy B için gerekir) `BookedCount==1 && tek booking bu line'ın` ile çıkarılır — ama MVP'de buna gerek yok (aşağı bkz. §6).

### 1.2 Ders tüketimi mantığı (Sprint 29C — `ContractLineRules`, `ContractLineFeature`)
- **Reserved** = `Σ ClassBooking(Booked, Regular, lineId)` (Scheduled + Completed) (+ tek-line header legacy null-line).
- **Planned** = Reserved ∩ `seans.Status=Scheduled`.
- **Delivered** = Reserved ∩ `seans.Status=Completed`.
- **AvailableForPlanning** = `max(0, LessonCount − Reserved)` → **booking/planlama kapısı** (RunAsync bunu üst sınır alır).
- **RemainingLessonRight** = `max(0, LessonCount − Delivered)` → kullanıcıya görünen "kalan hak".
- **Replan matematiği doğal:** gelecekteki Scheduled booking'ler iptal → `Reserved` düşer → `AvailableForPlanning` artar → RunAsync tam o kadar yeni ders üretir. **Delivered değişmez** (yalnız Completed seanstan sayılır, ona dokunulmaz).

### 1.3 Program üretim motoru (`ContractLineScheduleEngine.RunAsync`, Sprint 18/22/32/33)
`RunAsync(db, clock, studioId, userId, line, duration, slots, reuseExistingGroup, generate:true, ct, equipmentGuard, generationInstructorId?, generationCapacity?)`:
- **Effective hoca** = `line.InstructorProfileId ?? generationInstructorId` (ikisi de null → 409 "hoca seçin").
- **Effective kapasite** = `line.PreferredMaxCapacity ?? (ekipman-bağlı → generationCapacity zorunlu : 9999)`.
- **`EquipmentCapacityRules.EnsureFiniteForEquipment`** → ekipman-bağlı + 9999 yasak (32B invariant).
- Reuse yalnız `Capacity <= effectiveCapacity` gruplar + aynı effective hoca; her yeni seans ekipman-bağlıysa `EquipmentCapacityGuard.EnsureCanReserveAsync`.
- Skip: geçmiş (`<= studioNow`), studio closure, hoca çakışması, öğrenci çakışması; üretim `AvailableForPlanning` ile sınırlı.
- **Replan, bu motoru AYNEN kullanır** → tüm §8 guard'ları bedavaya gelir.

### 1.4 Hazır "detach + regenerate" deseni (`GroupScheduleChangeService`, Sprint 22)
`ExecuteAsync(instructorProfileId, classTypeId, effectiveDate, lineIds[], newSlots[])` zaten replan'in çok-satırlı hâli:
- **PHASE 1:** seçili satırların `effectiveDate` ve sonrası `Booked + Regular + seans Scheduled` booking'lerini **telafisiz** `CancelledByStudio` ("…taşındı") yapar.
- **PHASE 2:** etkilenen seansların `BookedCount`'ını yeniden sayar; **yalnız tamamen boşalan** Scheduled seansı Cancelled yapar → **shared-safe.**
- **PHASE 3:** her satır için `BuildCandidates(line, newSlots)` + sıralı `SaveChanges` ile cross-student reuse → yeniden üretir; hoca/öğrenci çakışması → `canApply=false`.
- **Sınırı:** `(instructorProfileId, classTypeId)` ortak grubuna kilitli ve `l.InstructorProfileId != instructorProfileId` ise reddeder → **null-hoca line'ı + tek-satır + farklı tercih senaryosunu karşılamaz.** Sprint 34 bunun **tek-satır + generation-time hoca/kapasite** varyantını yapar.

### 1.5 Mevcut uçlar ve servisler
- `PUT /api/studio/contract-lines/{lineId}` → `UpdateContractLineCommand(LineId, DueDate, Notes)` — **bugün yalnız DueDate + Notes.** (Hoca/kapasite/slot güncellemez.)
- `POST /api/studio/contract-lines/{lineId}/preferred-slots` → `AddLinePreferredSlotCommand`; `DELETE …/preferred-slots/{slotId}` → `RemoveLinePreferredSlotCommand` (tekil slot).
- `POST /api/studio/contract-lines/{lineId}/schedule/preview|generate` → preview/generate (S33 ile hoca/kapasite parametreli).
- `RescheduleClassSessionCommand` (`SessionMutations.cs`) — tek seansı yerinde taşır (Strategy B için potansiyel).
- `CancelBookingCommand` + `IClassSessionCapacityService.ReleaseSeatAsync` — tekil booking iptal + koltuk iadesi.
- **Frontend `contractService`:** `updateLine`, `generateLineSchedule({…instructorProfileId,capacity})`, `previewLineSchedule`, `addPreferredSlot`, `removePreferredSlot`, `cancelLine`. ContractDetailModal (S33) zaten slot ekle/sil + generate + kapasite gösterimi içeriyor.

### 1.6 Audit altyapısı
- Yalnız **`SecurityAuditLog`** (append-only, `ISecurityAuditLogger`) — güvenlik/auth olayları için. **Genel iş/aktivite audit tablosu YOK.**
- Mevcut metinsel iz: `ClassBooking.CancellationReason` + `ClassSession.Notes` (GroupScheduleChange bunları zaten kullanıyor: "Grup program değişikliği nedeniyle taşındı").
- **Öneri:** MVP'de replan/preference izini bu mevcut alanlarla tut (ek migration yok). Adanmış business-audit tablosu = açık soru/sonraki sprint (§12).

---

## 2. Temel ürün kararı

Sözleşme satırı **güncellenebilir** olacak ama **geçmişi bozmayacak**:
- **Completed seans / Delivered booking → asla değişmez.**
- Geçmiş tarihli (`StartAt <= studioNow`) seanslar değiştirilemez (RunAsync zaten geçmişi skip eder; replan yalnız `>= studioNow` Scheduled'ları ayırır).
- Güncelleme **"kalan haklar" (AvailableForPlanning)** üzerinden çalışır; `RemainingLessonRight` türetilmiş kalır.
- Cancelled geçmiş kayıtlar **silinmez** (audit/iz).

---

## 3. Güncellenebilir alanlar (bu sprint kapsamı)

| Alan | Kural |
|---|---|
| **InstructorProfileId** | `null` = herhangi bir hoca; dolu = hard hoca kuralı; dolu ise aktif stüdyoya ait olmalı (NotFound aksi halde). |
| **PreferredMaxCapacity** | `null` = farketmez; `1` = bireysel; `2..50` = en fazla N; **1..50 validasyonu korunur** (0/51 → 400). Güncellemede `IsIndividual = (PreferredMaxCapacity==1)` yeniden türetilir. |
| **Preferred day/time slots** | Kalan dersler için gün/saat tercihleri değiştirilebilir (Pzt 10:00 → Çar 18:00); çoklu tercih korunur; **en az 1 slot zorunlu** (S29A kuralı); aynı (gün,saat) tekrar edemez. |

**Bu sprint KAPSAM DIŞI** (ayrı sprint): fiyat/ödeme/borç · lesson count ↑↓ · classType değiştirme · date range değiştirme · geçmiş kayıt silme · makeup replan.

---

## 4. İki operasyon — tek mi, ayrı mı? → **AYRI** (gerekçeli)

**Karar: ayrı uçlar.** (Senin ön tercihin doğrulandı.)
- **Neden:** Kullanıcı tercihi kaydedip mevcut programı **hemen bozmak istemeyebilir** (örn. "gelecek ay için hocayı değiştireyim ama bu haftaki dersler dursun"). Tek endpoint zorunlu yan etki yaratır.
- **A) Tercih güncelle** idempotent, ucuz, yan etkisiz (yalnız line + slot kaydeder).
- **B) Replan** explicit, pahalı, yıkıcı-yeniden-üretici (booking iptal + üretim) → ayrı onay/aksiyon + preview.
- UI'da B, A'dan sonra "Kalan dersleri yeni tercihlere göre yeniden oluştur" butonuyla tetiklenir (preview + onay modalı).

---

## 5. Replan (kalan dersleri yeniden planla) mantığı

Senaryo: 10 derslik line, 2 Completed.
- Completed 2 ders **aynen kalır** (Delivered=2).
- Kalan 8 (AvailableForPlanning) için replan:
  1. **Detach:** `StudentContractLineId=line && Status=Booked && SessionKind=Regular && seans.Status=Scheduled && seans.StartAt >= studioNow` → `CancelledByStudio`, `CancellationReason="Sözleşme tercih değişikliği — yeniden planlandı"`. (Phase 1, tek satıra scoped.)
  2. **Recompute + emptied-cancel:** etkilenen seansların `BookedCount` yeniden sayılır; tamamen boşalan Scheduled seans → Cancelled, `Notes="Tercih değişikliği — boş kaldı"`. (Phase 2 = shared-safe.) Koltuk iadesi `ReleaseSeatAsync` mantığıyla tutarlı (BookedCount güncel).
  3. **Regenerate:** satırın **güncel** (post-update) slot/hoca/kapasitesiyle `RunAsync(generate:true, generationInstructorId, generationCapacity)`. Detach sonrası `AvailableForPlanning` arttığı için tam kalan kadar üretir; geçmiş/closure/çakışma skip; equipment guard çalışır.
- **Kapsam:** yalnız **regular** kalan dersler. **Makeup booking'ler dokunulmaz** (ayrı kredi/ledger mantığı; bu sprint dışı).
- **mode:** MVP'de tek mod — `future-only` (varsayılan ve tek). İleride `from-date` parametresi açık soru.

---

## 6. Shared group session güvenliği (KRİTİK)

**Kural:** Bir öğrencinin tercih değişikliği, paylaşılan grup seansının saatini/hocasını/kapasitesini **değiştirmemeli** (diğer öğrenciler etkilenmez).

**Çözüm = Strategy A (detach + regenerate) ile YAPISAL GARANTİ:**
- Replan **hiçbir seansı taşımaz/güncellemez.** Yalnız *bu öğrencinin* booking'ini ayırır.
- Seans yalnız **tamamen boşalırsa** Cancelled olur (Phase 2). Başka öğrenci varsa seans aynen kalır, sadece `BookedCount−1`.
- Bu öğrencinin kalan dersi yeni tercihe göre **yeni/uygun başka gruba** alınır (reuse veya yeni seans).
- → **"Private vs shared" ayrımına MVP'de gerek YOK.** Detection kompleksitesi tamamen elenir.

**Private session in-place move (Strategy B) — ertelenir:** Eğer seans yalnız bu line'a aitse (`BookedCount==1 && tek booking bu line`) seansı yerinde `Reschedule` etmek mümkün olurdu (booking iptal etmeden). Ama:
- Bunu kesin bilmek için seans→line "üretim kaynağı" bilgisi gerekir; bugün yalnız `BookedCount==1 + booking.lineId` ile **çıkarım** yapılabilir (kesin değil: 1 kişilik grup da olabilir).
- Kesinlik için `ClassSession.SourceContractLineId (Guid?)` alanı + migration gerekir → **MVP dışı.**
- **Açık soru (§13):** Strategy B'nin değeri (taşımada booking-id/audit sürekliliği) migration maliyetine değer mi? MVP: **hayır, Strategy A yeterli ve en güvenli.**

---

## 7. Replan stratejisi seçimi

| | A. Detach + regenerate | B. In-place move |
|---|---|---|
| Shared group | ✅ güvenli (taşımaz) | ❌ kullanılamaz |
| Private session | ✅ çalışır (yeni seans) | ✅ (booking-id korunur) |
| Migration | ❌ gerekmez | ✅ gerekir (SourceContractLineId) |
| Audit/geri-alma | iptal kayıtları + yeni booking; eski booking `Cancelled` kalır (iz) | seans Reschedule (RescheduledAt/Reason mevcut) |
| Karmaşıklık | düşük (mevcut motorlar) | orta (private detection) |

**Karar: MVP = Strategy A (tüm senaryolarda).** En güvenli, migrationsız, mevcut GroupScheduleChange + RunAsync desenini yeniden kullanır. B, ileride private-session optimizasyonu olarak değerlendirilir.

**Geri alma/audit:** Eski booking'ler hard-delete edilmez; `CancelledByStudio + Reason` ile kalır. Yeni booking'ler yeni kayıt. Tam "undo" bu sprintte yok (açık soru §13) — ama iz tam.

---

## 8. Business guard'lar (hepsi mevcut motorla geliyor)

Replan/preference şunlara uyar — **çoğu RunAsync/BookingCreator'da zaten var, yeniden yazılmaz:**
- `PreferredMaxCapacity` varsa `session.Capacity <= pref` (effective kapasite + reuse cap).
- `InstructorProfileId` varsa aynı hoca; null + generation-time hoca → o hoca.
- Reuse aynı effective hoca.
- **Ekipman-bağlı + 9999 asla** (`EnsureFiniteForEquipment`); ekipman-bağlı + tercih null + replan → generation capacity zorunlu (yoksa 409).
- `EquipmentCapacityGuard.EnsureCanReserveAsync` her yeni ekipman seansında (havuz kilidi + availability; kapasite yetmezse 409).
- Öğrenci çakışması / hoca çakışması → skip (üretilmez) veya conflict raporu.
- Studio closure/tatil → skip.
- Line date range dışına çıkılmaz (`BuildCandidates` `StartDate..EndDate` ile sınırlı).
- Kalan ders hakkından fazla üretilmez (`AvailableForPlanning` üst sınır).
- Completed seans cancellation kuralı bozulmaz (yalnız Scheduled + gelecek ayrılır).
- **Delivered count değişmez** (Completed booking'lere dokunulmaz).
- **Yetki:** Owner/StudioAdmin (mevcut `IStudioWriteRequest` + role guard; GroupScheduleChange deseni).

---

## 9. Endpoint önerileri

```
PUT  /api/studio/contract-lines/{lineId}/preferences
Body: {
  "instructorProfileId": null | "guid",
  "preferredMaxCapacity": null | 1..50,
  "preferredSlots": [ { "dayOfWeek": "Wednesday", "preferredStartTime": "18:00" | null, "notes": null }, ... ]  // ≥1, full replace
}
→ 200 StudentContractLineDto (güncel). Mevcut booking'lere DOKUNMAZ.

POST /api/studio/contract-lines/{lineId}/replan/preview
Body: { "instructorProfileId": null|"guid", "capacity": null|1..50 }   // generation-time override (line null ise)
→ 200 { completedCount, remainingCount, futureBookingsToCancel, plannableCount, perSlotOutcomes[], effectiveInstructor, effectiveCapacity, warnings[] }

POST /api/studio/contract-lines/{lineId}/replan
Body: { "instructorProfileId": null|"guid", "capacity": null|1..50, "mode": "future-only" }
→ 200 { cancelledFutureBookings, emptiedSessionsCancelled, createdCount, newSessionCount, reusedSessionCount, skippedCount, createdStartAts[] }
```

**Mimari notlar:**
- `preferences` = mevcut `UpdateContractLineCommand`'ı genişletmek yerine **ayrı `UpdateContractLinePreferencesCommand`** (sorumluluk ayrımı; DueDate/Notes ayrı kalsın). Slotlar için **bulk-replace** (mevcut tek-tek Add/Remove yerine "verilen liste = yeni durum"); iç implementasyon Add/Remove'i çağırabilir ama tek transaction + S29A duplicate/≥1 validasyonu.
- `replan` yeni handler: GroupScheduleChange Phase 1–2'yi **tek satıra** uygular + `RunAsync` Phase 3. Hoca null ise `generationInstructorId` zorunlu (yoksa 409, RunAsync zaten atıyor).
- `replan/preview` = yıkıcı olmayan hesap: detach edilecek booking sayısı + `PreviewLineScheduleQuery` mantığıyla (detach sonrası available varsayımıyla) plannable sayısı. Önizleme transaction'ı **rollback** (DB değişmez) ya da saf hesap.
- Route ailesi mevcut `ContractsController` (`contract-lines/{lineId}/…`) ile tutarlı.

---

## 10. UI beklentisi (ContractDetailModal genişletme)

**A. Tercihleri düzenle** (mevcut modal içinde, S33 alanlarını editlenebilir yap):
- Hoca (opsiyonel select) · Kapasite tercihi (Farketmez/Bireysel/En fazla N/Diğer 1–50) · Gün/saat tercih chip'leri (ekle/sil; mevcut `addSlot/removeSlot` UI'ı düzenleme moduna alınır) · **Kaydet** → `PUT …/preferences`.

**B. Kalan dersleri yeniden planla** butonu → **Preview modalı**, özet:
- tamamlanan ders sayısı · kalan ders sayısı · iptal/taşınacak future booking sayısı · yeni üretilecek ders sayısı · effective hoca/kapasite/gün-saat.
- Hoca null ise **hoca seçimi zorunlu**; ekipman-bağlı + tercih null ise **seans kapasitesi zorunlu** (S33 generate akışındaki guard'lar yeniden kullanılır).

**C. Onay modalı metni:**
- "Tamamlanan derslere dokunulmaz" · "Yalnız gelecekteki planlı dersler etkilenir" · "Grup derslerinde başka öğrenciler etkilenmez" · "Uygun ders yoksa işlem durur/eksik planlanır".

**D. Sonuç ekranı:** kaç ders yeniden planlandı · hangileri oluşturuldu (gün/saat) · hangileri uygunluk olmadığı için oluşturulamadı (skip nedeni: çakışma/kapanış/kapasite).

---

## 11. Migration ihtiyacı → **YOK** (gerekçeli)

- Tercih update: `InstructorProfileId`, `PreferredMaxCapacity` zaten line kolonları; slotlar zaten `StudentContractLinePreferredSlot` tablosunda + CRUD hazır. ✅
- Replan: saf orkestrasyon (booking iptal + seans recompute + RunAsync). Yeni şema yok. ✅
- Line→seans takibi: `ClassBooking.StudentContractLineId` mevcut. ✅
- Audit izi: `CancellationReason`/`Notes` mevcut alanlar. ✅
- **Yeni alan yalnız Strategy B için gerekir** (`ClassSession.SourceContractLineId`) → MVP dışı; gerekirse ayrı sprint + açık gerekçe.

> **Önce migrationsız çözüm değerlendirildi ve YETERLİ.** Sprint 34 additive-only davranış sprinti.

---

## 12. Audit / log

- Adanmış business-audit tablosu **yok**; bu sprintte eklenmez (MVP).
- **MVP izi:** `ClassBooking.CancellationReason = "Sözleşme tercih değişikliği — yeniden planlandı"` + boşalan seans `ClassSession.Notes`. (GroupScheduleChange ile aynı desen.)
- **Öneri (açık soru):** `SecurityAuditLog`'a iki event tipi (`ContractLinePreferencesUpdated`, `ContractLineSchedulePlanned`) eklemek mantıklı olabilir ama bu log "security" semantiğinde — uygun mu, yoksa ayrı `ActivityLog` tablosu mu? → §13.

---

## 13. Açık sorular (karar/öneri)

1. **Preference update otomatik replan tetiklesin mi?** Öneri: **HAYIR** — ayrı explicit aksiyon (kullanıcı programı hemen bozmak istemeyebilir). UI'da kaydetten sonra "Kalan dersleri yeniden planlamak ister misin?" hatırlatması gösterilebilir.
2. **Replan kapsamı `future-only` mi, `from-date` seçilebilir mi?** Öneri: MVP `future-only` (studioNow). `from-date` (örn. "şu tarihten itibaren") ileride.
3. **Strategy B (private session in-place move) + `SourceContractLineId` migration'ı değer mi?** Öneri: MVP'de hayır; Strategy A yeterli.
4. **Audit:** `SecurityAuditLog`'a iş-eventi mi, yeni `ActivityLog` tablosu mu, yoksa yalnız metinsel iz mi? Öneri: MVP metinsel iz; adanmış tablo ayrı sprint.
5. **"Undo replan"** gerekiyor mu? Öneri: bu sprintte yok; iptal kayıtları iz bırakır, gerekirse manuel.
6. **Slot güncelleme = full replace mı, granular Add/Remove mı?** Öneri: API'de full-replace (`preferences.preferredSlots` = yeni durum); iç implementasyon mevcut komutları kullanabilir.
7. **Hoca/kapasite değişince mevcut Planned booking'ler ihlalliyse (örn. yeni pref=Bireysel ama mevcut grup booking var) preference-update tek başına ne yapsın?** Öneri: preference-update **yalnız kaydeder** (mevcut booking'i ihlal saymaz); tutarlılık replan ile sağlanır. UI uyarı gösterir: "N gelecekteki ders yeni tercihle uyumsuz — yeniden planla".
8. **Pasif/iptal (non-Active) line** preference/replan'a kapalı mı? Öneri: yalnız `Active` (GroupScheduleChange ile tutarlı).

---

## 14. Test planı (spec'e dahil; uygulamada smoke = `sprint34-smoke.sh`)

**A. Preference update**
- hoca null → kaydedilir; hoca dolu → tenant/studio doğrulaması (yabancı hoca → 404).
- `preferredMaxCapacity` 0/51 → 400; 1 → IsIndividual=true; null → IsIndividual=false.
- preferred slots full-replace (Pzt → Çar); ≥1 zorunlu (boş → 400); aynı (gün,saat) tekrar → 400.
- preference update **mevcut booking sayısını DEĞİŞTİRMEZ** (Planned/Delivered sabit).

**B. Candidate/booking after update**
- hoca null→dolu: o seansın regular-candidate listesi daralır (yalnız o hoca).
- kapasite pref 9999→2: cap3 seansa booking 409; cap2 200 (BookingCreator S33 guard).

**C. Replan future-only**
- 10 derslik line, 2 Completed: replan sonrası Delivered=2 değişmez; gelecekteki Scheduled regular booking'ler iptal; kalan 8 yeni tercihe göre planlanır; `RemainingLessonRight` korunur.
- toplam Planned+Delivered ≤ LessonCount (over-plan yok).

**D. Shared group safety**
- Seansta 2 öğrenci (A, B). A'nın line'ı replan edilir → seansın StartAt/Instructor/Capacity **değişmez**; B'nin booking'i **etkilenmez**; yalnız A'nın booking'i ayrılıp yeni plana alınır. Seans boşalmadıysa Cancelled olmaz.

**E. Equipment guard**
- ekipman-bağlı + pref null + replan capacity'siz → 409.
- capacity seçilince 32B guard çalışır; havuz yetmezse 409; üretilen seans `Capacity=seçilen` (9999 değil).

**F. Instructor guard**
- line hoca null + replan'da hoca seçili → o hocaya göre planlar/reuse.
- hoca çakışması olan slot → o slot skip (eksik plan) veya conflict raporu.

**G. Regression**
- backend build 0/0 · tsc 0 · next build 0 · has-pending **clean (migration yok)**.
- sprint32a/b/c · sprint33 · booking/makeup/contract-schedule (sprint18a/22/28c1/28c2/27/29b) yeşil.
- **Özellikle:** GroupScheduleChange (sprint22) ve generate (sprint18a) replan refactor'ından sonra bozulmamalı; 32B invariant korunmalı.

---

## 15. Faz / iş kalemleri (uygulama onaylanırsa — şimdi DEĞİL)

1. `UpdateContractLinePreferencesCommand` + validator + handler (instructor/capacity/slots full-replace; IsIndividual türet).
2. `ReplanLinePreview` query + `ReplanLineScheduleCommand` + handler (detach Phase1-2 tek-satır + RunAsync Phase3; effective hoca/kapasite; sonuç DTO).
3. Controller: `PUT preferences`, `POST replan/preview`, `POST replan`.
4. Frontend: ContractDetailModal "Tercihleri düzenle" (editable hoca/kapasite/slot) + "Kalan dersleri yeniden planla" (preview + onay + sonuç); `studioApi` yeni metotlar; `domain-types`.
5. `sprint34-smoke.sh` (A–F) + regresyon.
6. Doğrulama: build/tsc/next 0 · has-pending clean · smoke + regresyon yeşil.
7. **Migration YOK.** Commit/push/tag yalnız ayrı onayla. Önerilen sonraki tag: `v1.25.0-pilot`.

> **Bu dosya kalıcı spec'tir.** Uygulama yalnız onaylanan kapsamla başlar. **Şu an kod/migration/commit YOK.**

# Sprint 43 — Student Booking Reschedule / Move Students Between Sessions

> Dersin **tamamını** ertelemek yerine, bir dersteki **seçili öğrenci/öğrencileri** başka bir derse veya
> yeni açılan uygun bir boşluğa taşıma. Kaynak ders ertelenmez; kalan öğrenciler etkilenmez.

## Kapsam (v1)

**Var:**
- Owner/StudioAdmin, bir dersin içinden bir veya birden fazla öğrenciyi seçip taşır.
- Hedef: (A) var olan uygun bir ders, ya da (B) yeni açılan uygun bir boşluk (slot).
- İşlem tek transaction içinde atomik (yeni ders + taşıma birlikte ya hep ya hiç).

**Yok (bu sprint dışı):** Instructor taşıma yetkisi · öğrenci self-service reschedule · Makeup booking taşıma ·
`MovedOut` statüsü · rapor/ders-hakkı hesap motoru değişikliği · attendance/takvim redesign · drag-drop ·
notification/SMS/WhatsApp · migration (zorunlu değil — yapılmadı).

## Kararlar (onaylı)

1. **Yetki:** v1 yalnız **Owner / StudioAdmin**. Instructor → 403.
2. **Kaynak booking statüsü:** `CancelledByStudio` + `CancellationReason = "Taşındı"`. Yeni statü yok, migration yok.
   Taşıma olduğu `CancellationReason` ve `BookingMoved` audit ile ayırt edilir.
3. **Makeup booking:** kapsam dışı. Yalnız **Regular** booking taşınır; Makeup/non-Regular kaynak → net hata.

## Backend

### Endpoint
```
POST /api/studio/class-sessions/{sourceSessionId}/bookings/move
Authorize: Owner, StudioAdmin   (Instructor → 403)
```

### Request — `MoveBookingsCommand`
```jsonc
{
  "bookingIds": ["..."],                 // zorunlu, boş olamaz; kaynak dersin Booked rezervasyonları
  "targetSessionId": "...",              // Mod A — var olan derse taşı
  "createTargetSession": {               // Mod B — yeni boşlukta ders aç (XOR targetSessionId)
    "instructorProfileId": "...",
    "classTypeId": "...",
    "startAt": "2026-07-01T10:00:00",
    "capacity": 4                        // ops.
  },
  "allowPastBooking": false              // hedef geçmişse true gerekir (Sprint 42 uyumu)
}
```
Validation: `targetSessionId` **XOR** `createTargetSession` (tam olarak biri) — ikisi de dolu/boş → 400.

### Response — `MoveBookingsResultDto`
```jsonc
{ "targetSessionId": "...", "targetSessionCreated": true, "movedCount": 3, "moved": [ { sourceBookingId, targetBookingId, studentProfileId, studentName } ] }
```

### Handler akışı (`MoveBookingsHandler`, tek transaction — `TransactionBehavior`)
1. Rol guard: Owner/StudioAdmin değilse 403.
2. Kaynak session: aynı stüdyo · `Scheduled` · **geçmiş değil** · `SessionKind == Regular`.
3. `bookingIds` kaynağa ait + hepsi `Booked` · yoklaması (`Attendance`) olan booking → red.
4. **Mod B** ise hedef session'ı inline oluştur (`SessionRules.EnsureValidAsync` + `StudioWorkingHoursService.EnsureWithin`
   + `EquipmentCapacityGuard` — `CreateClassSession` ile aynı guard'lar; `allowPast=false`). **Mod A** ise hedefi yükle:
   aynı stüdyo · `Scheduled` · kaynak ≠ hedef.
5. Her booking için **sırayla**:
   - **Kaynaktan çıkar:** `Status = CancelledByStudio`, `CancellationReason = "Taşındı"`, `ReleaseSeatAsync` (BookedCount−).
     Telafi kredisi **restore/grant edilmez** (bu bir iptal değil, taşıma). `CancelBooking` handler'ı kullanılmaz.
   - `SaveChangesAsync` (transaction içinde — ders hakkı sayımları güncel durumu görsün diye **kritik ordering**).
   - **Hedefe ekle:** `BookingCreator.CreateAsync(target, student, …, studentContractLineId = kaynaktaki line, allowPast)`.
     Mevcut Regular booking motoru tüm hedef guard'larını uygular (kapasite, duplicate, öğrenci çakışması,
     bireysel/grup tercihi, contract line uygunluğu, class type uyumu, pasif öğrenci).
6. **Audit:** taşıma başına **tek aggregate** `BookingMoved` (detail: `source`, `target`, `count`; PII yok).
7. Herhangi bir adım fırlatırsa tüm transaction rollback → kaynak booking'ler eski halinde, hedef booking oluşmaz,
   Mod B'de yeni ders de geri alınır, `BookedCount` tutarsız kalmaz.

### Ders hakkı nötrlüğü
Kaynaktaki `StudentContractLineId` hedef booking'e **aynen** geçirilir. Kaynaktan çıkarma `Reserved`'ı −1,
hedefe ekleme +1 → net etki **nötr**. Hesap motoru (`ContractLineRules`) **değişmez**.

### Audit
`SecurityAuditEventType.BookingMoved` eklendi. Enum kolonda **string** saklandığı için **migration gerekmez**.
Çoklu taşımada **tek aggregate** log seçildi (daha az satır, PII yok).

## Frontend

- **Başlangıç:** `SessionDetailDrawer` → **"Öğrenci Taşı"** aksiyonu (yalnız Owner/StudioAdmin, kaynak Scheduled & gelecek).
- **Modal akışı:** kaynak ders özeti → öğrenci checkbox seçimi (seçili sayaç) → hedef tipi (var olan ders / yeni boşluk).
  - *Var olan derse taşı:* tarih aralığı + (ops.) hoca/ders tipi filtresi → `GET /class-sessions` ile uygun dersler,
    kapasite/doluluk + uygunluk rozeti. Dolu ders seçilemez; hedef seçilmeden submit kapalı.
  - *Yeni boşlukta aç:* mevcut `GET /availability/class-type-slots` (RequiredCapacity = seçili öğrenci sayısı) →
    pencere seç → yeni ders önizleme. **Yeni endpoint gerekmedi.**
- **Confirm:** "Seçili öğrenciler taşınsın mı?" / "Seçili öğrenciler bu dersten çıkarılıp hedef derse taşınacak.
  Kaynak dersteki diğer öğrenciler etkilenmez. Bu işlem ders hakkı, katılım ve raporları etkileyebilir." /
  Onay "Evet, taşı" · Vazgeç "Vazgeç".
- **Toast:** tek "Öğrenci yeni derse taşındı." · çoklu "N öğrenci yeni derse taşındı."
- **UI sınırları:** yoklaması alınmış booking / Makeup booking seçilemez veya backend net reddeder; kaynak geçmişse
  aksiyon pasif; seçili öğrenci yoksa submit kapalı; "kalan öğrenciler etkilenmez" mesajı görünür.

## İş kuralları (özet)

| Alan | Kural |
|------|-------|
| Kaynak ders | aynı stüdyo · Scheduled · geçmiş değil · Regular · Completed/Cancelled değil |
| Seçili booking | kaynağa ait · Booked · yoklama yok |
| Hedef ders | aynı stüdyo · Scheduled · kaynak ≠ hedef · geçmiş ise yalnız `allowPastBooking=true` |
| Booking motoru | kapasite · duplicate · öğrenci zaman çakışması · contract line uygunluğu · class type uyumu · bireysel/grup tercihi · pasif öğrenci |

## Test / Validation
Backend: `dotnet build` 0 · `has-pending` temiz (migration yok) · tek/çoklu var olan derse taşıma · yeni boşlukta
ders açıp taşıma · Instructor 403 · geçmiş/Completed/Cancelled kaynak red · yoklama red · Makeup red · kapasite
yetersiz red · duplicate red · öğrenci çakışma red · contract line uygunsuz red · bireysel hedefe 2 öğrenci red ·
yeni ders create OK + move fail → rollback · kalan öğrenciler etkilenmez · tüm öğrenciler taşınınca kaynak Scheduled+0 ·
`BookingMoved` audit (PII yok). Frontend: `tsc --noEmit` 0 · `next build` 0 · drawer butonu · seçim · hedef seçimi ·
slot listesi/önizleme · confirm iptal → API yok · confirm onay → endpoint çağrısı · success/err toast.

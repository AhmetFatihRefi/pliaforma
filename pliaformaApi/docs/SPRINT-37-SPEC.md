# SPRINT 37 — SPEC & ANALİZ: Stüdyo Çalışma Saatleri (Haftalık Açılış/Kapanış) + Tüm Akışlarda Saat Sınırı

> **Durum:** SPEC turu — onay bekliyor. Kod YOK, migration YOK, commit/push/tag YOK.
> **Hedef:** Her stüdyo kendi haftalık çalışma saatlerini ayarlayabilsin; sistem bu saatler dışında boşluk göstermesin, ders oluşturmasın, program üretmesin, replan ile ders yerleştirmesin.

---

## 0. Özet karar tablosu (öneriler — onay bekliyor)

| # | Konu | Öneri |
|---|---|---|
| D1 | Model | **Ayrı haftalık tablo** `StudioWorkingHour` (kullanıcı ön tercihi). `Studio`'ya tek open/close kolonu DEĞİL. |
| D2 | Backfill | Migration **yalnız tabloyu ekler**; backfill YOK. Kayıt yoksa **runtime fallback 07:00–23:00**. İlk kaydet/seed 7 gün üretir. |
| D3 | Tek kaynak | Tek servis `StudioWorkingHoursService` (open/close/closed + içinde mi + pencere kırpma). Her akış bunu kullanır. |
| D4 | Gece aşan saat | Kapsam **dışı** (OpensAt < ClosesAt zorunlu). |
| D5 | Manuel create/update/reschedule | Saat çalışma penceresi dışındaysa → **409** (start VE end tamamen içeride olmalı). |
| D6 | Generate/replan | Çalışma saati dışı aday **skip** (mevcut closure/past skip ile aynı yer). Tüm slotlar dışıysa **eksik plan raporu**. |
| D7 | Kapalı güne preferred slot | Slot kaydedilebilir; generate/replan **skip** + UI uyarısı. Kayıt engellenmez. |
| D8 | Çalışma saati değişince mevcut gelecek dersler | Otomatik iptal/taşıma YOK. Yalnız bilgilendirme (UI uyarısı, opsiyonel sayım). |
| D9 | Schedule grid | Grid min/max = haftadaki en erken açılış / en geç kapanış. Kapalı gün "Kapalı" etiketi. Çalışma dışı mevcut dersler GİZLENMEZ. |
| D10 | Yetki | Yazma `Owner,StudioAdmin`. Okuma tüm authenticated rollere açık (availability/grid için instructor da okur). |

---

## 1. Mevcut sistem analizi (kod-temelli — doğrulandı)

### 1.1 Hardcoded çalışma saati NEREDE?

**Tek bir yer var:** `src/Pliaforma.Application/Features/Availability/FindClassTypeSlots.cs`

```csharp
// satır 43-44 (FindClassTypeSlotsHandler)
private static readonly TimeOnly WorkStart = new(7, 0);   // 07:00 (32C sabit; stüdyo WorkingHours ayarı post-MVP)
private static readonly TimeOnly WorkEnd = new(23, 0);    // 23:00
```

Kullanımı (Boşluk Bul / sweep-line motoru):
- satır 151-152: `dayWorkStart = date.ToDateTime(WorkStart)`, `dayWorkEnd = date.ToDateTime(WorkEnd)`
- satır 155-158: başlangıç-zamanı alanı `[domainStart, domainEnd]`; `domainEnd = dayWorkEnd − süre`
- satır 159: güne 1 ders sığmıyorsa o günü atla
- satır 164: olay noktaları `domainStart`/`domainEnd` ile başlar

> Sınır **per-handler statik sabit**; gün ayrımı yok (her güne aynı 07:00–23:00). Yorumun kendisi "post-MVP" diyor.

**Frontend'de hardcoded metin/varsayım:**
- `find-availability/page.tsx`: "Çalışma penceresi 07:00–23:00" etiketi (sadece bilgi metni).
- `schedule/page.tsx`: yorum "çalışma saatleri 07:00–23:00 olduğundan".
- **Schedule grid'inin kendisi saat penceresini HARDCODE ETMİYOR.** Sprint 37'de (frontend) grid oransal/absolute modeli terk edip **zaman-sıralı akış** (gün=sütun, kartlar içeriğe göre sığar) kullanıyor: `timetable.tsx`. Yani grid "07:00 satırı / 23:00 satırı" çizmiyor; sadece var olan öğeleri zaman sırasına diziyor. → Grid'e açılış/kapanış görseli **eklenecek yeni bir şey** (bugün yok).

### 1.2 Seans create/update/reschedule guard'ları çalışma saati kontrol ediyor mu? → **HAYIR**

- `CreateClassSession.cs`: `SessionRules.EnsureValidAsync` (past + hoca çakışma + pending telafi) + `EquipmentCapacityGuard`. **Saat penceresi kontrolü YOK.** `endAt = startAt + classType.DefaultDurationMinutes`.
- `SessionMutations.cs` `UpdateClassSessionHandler` / `RescheduleClassSessionHandler`: `SessionRules.EnsureValidAsync` / `EnsureMovableAsync` + line tarih aralığı + equipment guard. **Saat penceresi kontrolü YOK.**
- `BulkGenerateClassSessions.cs`: per-slot past-skip + çakışma-skip + equipment. **Saat penceresi kontrolü YOK** (00:00–06:00 bile üretebilir).
- `SessionRules.cs`: `EnsureValidAsync`/`EnsureMovableAsync` içinde **hiçbir saat-of-day kontrolü yok** (yorum: availability ADVISORY, Sprint 26 kararı).
- `StartAt`/`EndAt` → `DateTimeKind.Unspecified` (stüdyo-yerel duvar saati). Süre `ClassType.DefaultDurationMinutes`.

### 1.3 ContractLineScheduleEngine çalışma saatini nereden alıyor? → **HİÇBİR YERDEN**

`src/Pliaforma.Application/Features/Contracts/ContractLineScheduling.cs`:
- `BuildCandidates` (174-186): `line.StartDate→EndDate` her gün, o günkü slotlar, `slot.StartTime` → candidate. **Saat penceresi yok.**
- `RunAsync` (201-282) skip zinciri:
  - 239: `created.Count >= available` → dur (LessonCount cap)
  - 240: `c.StartAt <= nowLocal` → skip (past)
  - 241: `InAnyClosure(c.StartAt, closures)` → skip (kapanış)
  - 247-253: hoca/öğrenci çakışma → skip
  - **Çalışma saati skip'i YOK** → eklenecek nokta tam burası (240-241 hizası).

### 1.4 Sprint 34 replan çalışma saatinden otomatik etkilenir mi? → **EVET (RunAsync'i yeniden kullanıyor)**

`ContractLineReplan.cs` `ReplanLineScheduleHandler` Phase 3:
```csharp
var gen = await ContractLineScheduleEngine.RunAsync(
    _db, _clock, studioId, userId, line, duration, slots, reuseExistingGroup: true, generate: true, ct,
    _equipmentGuard, request.InstructorProfileId, request.Capacity);
```
→ `RunAsync`'e çalışma-saati skip'i eklenirse **replan otomatik uyar** (ayrı kod gerekmez). Phase 1/2 (cancel future + emptied) zaten saatten bağımsız. **Completed/geçmiş korunur** (Phase 1 yalnız `≥ nowLocal` Scheduled bookings'i detach eder).

### 1.5 StudioClosure / tatil yapısı → **VAR**

`src/Pliaforma.Domain/Entities/StudioClosure.cs`: `StartAt/EndAt` (duvar saati), `Title`, `Reason`, `ActionType`, `AffectedInstructorProfileId?`, `AffectedClassTypeId?`. Config: `StudioClosureConfiguration` (`timestamp without time zone`, index `(StudioId, StartAt)`).
Tüketim: `ContractScheduleEngine.InAnyClosure(startAt, closures)` (yarı-açık `[StartAt, EndAt)`); availability'de de `closures` yüklenip segment elenir.
→ **Çalışma saati, closure'dan AYRI bir katman:** closure = tarih-bazlı tek-seferlik kapanış; working hours = haftalık tekrarlayan açık/kapalı pencere. İkisi de skip üretir; sıralama: önce çalışma-saati (gün/pencere), sonra closure.

### 1.6 Studio entity'de çalışma saati alanı → **YOK** (ama `TimeZoneId` VAR)

`Studio.cs`: `Name, Slug, TimeZoneId(="Europe/Istanbul"), Currency, Contact*, Address*, City, Status, JoinCode*…`. **Working-hours alanı yok.** `TimeZoneId` mevcut (yerel "şu an" hesabı `SessionRules.StudioNowAsync` ile zaten yapılıyor).

### 1.7 InstructorAvailability — birebir precedent

`InstructorAvailability.cs`: `StudioId, InstructorProfileId, DayOfWeek (string), StartTime/EndTime (TimeOnly), IsActive`. Config: `DayOfWeek` `HasConversion<string>().HasMaxLength(12)`; `TimeOnly` native (`time without time zone`, conversion yok). **Yeni tablo bunu birebir izleyecek** (sadece InstructorProfile FK'sı yok). Not: instructor availability bugün **advisory** (enforce edilmiyor, Sprint 26).

### 1.8 Migration / EF konvansiyonu

- Config: `IEntityTypeConfiguration<T>` sealed, `ApplyConfigurationsFromAssembly` (AppDbContext.OnModelCreating). Global soft-delete + `Id ValueGeneratedNever`.
- DbSet: `AppDbContext` + `IAppDbContext` arayüzüne eklenir.
- Migration: `dotnet ef migrations add NAME --project src/Pliaforma.Persistence --startup-project src/Pliaforma.Persistence -o Migrations`. Startup'ta auto-migrate (`DbInitializer.MigrateAndSeedAsync` → `db.Database.MigrateAsync()`).

---

## 2. Model gerekiyor mu? → EVET, tek yeni tablo (additive)

### Yeni entity: `StudioWorkingHour` (`BaseEntity, ITenantScoped`)

```
Id            Guid       (BaseEntity)
StudioId      Guid       (FK Studio, Cascade)
DayOfWeek     DayOfWeek  (string, HasMaxLength(12)) — Sunday=0..Saturday=6
OpensAt       TimeOnly?  (IsClosed=false ise zorunlu; time without time zone)
ClosesAt      TimeOnly?  (IsClosed=false ise zorunlu)
IsClosed      bool       (true → o gün kapalı; OpensAt/ClosesAt null)
IsActive      bool = true
(+ BaseEntity: CreatedAt/UpdatedAt/IsDeleted; + CreatedByUserId opsiyonel)
```

**EF config (`StudioWorkingHourConfiguration`):**
- `ToTable("StudioWorkingHours")`, `DayOfWeek` `HasConversion<string>().HasMaxLength(12)`.
- FK `Studio` Cascade.
- Index `(StudioId, DayOfWeek)` **unique** (her gün için tek kayıt).
- `OpensAt`/`ClosesAt` `TimeOnly?` — native (conversion yok).

**Kurallar (domain/validator):**
- Studio başına 7 gün kaydı (Pzt–Paz).
- `IsClosed=true` → `OpensAt`/`ClosesAt` null; o gün üretim/boşluk yok.
- `IsClosed=false` → `OpensAt` ve `ClosesAt` zorunlu, `OpensAt < ClosesAt`.
- Gece aşan (örn. 22:00–02:00) **kapsam dışı** → `OpensAt < ClosesAt` reddeder.

> **Studio tablosunda tek open/close kolonu vs ayrı tablo analizi:** Tek kolon basit ama (a) gün-bazlı farklılık (Cmt 10–18, Paz kapalı) imkânsız; (b) "kapalı gün" ifade edilemez; (c) ürün açıkça haftalık düzen istiyor. → **Ayrı tablo doğru** (kullanıcı ön tercihi ile uyumlu; `InstructorAvailability` precedent'i hazır).

### Migration: `Sprint37_StudioWorkingHours`
- **Yalnız tabloyu + index'i ekler.** Drop/alter yok. **Backfill YOK** (D2).
- `has-pending` migration sonrası "No changes" olmalı.

---

## 3. Fallback stratejisi (D2 — kritik, geriye uyum)

Mevcut tüm stüdyolarda 0 kayıt olacak → davranışı bozmamak için:

- **Runtime fallback:** Bir gün için `StudioWorkingHour` kaydı yoksa (veya hiç kayıt yoksa) → **07:00–23:00 açık** varsayılır (bugünkü hardcoded davranış).
- **İlk yazımda materyalize:** Kullanıcı "Çalışma Saatleri"ni ilk kez kaydederse 7 günün tamamı yazılır (PUT full-replace).
- **Demo seed:** `DbInitializer` demo studio seed'i 7 gün 07:00–23:00 (Paz dahil veya örn. Cmt 10–18/Paz kapalı — seed tercihi açık soru §10/S1) üretebilir; **mevcut studio'lara dokunmaz**.
- → Migration basit kalır; regresyon/smoke'lar fallback sayesinde bozulmaz.

**Servis sözleşmesi:** "kayıt yok" = "07:00–23:00 açık". "kayıt var + IsClosed=true" = "kapalı". "kayıt var + açık" = o pencere. (Yani kapalı günü ifade etmenin tek yolu açık kayıt + IsClosed; kayıt yokluğu asla "kapalı" demek değil.)

---

## 4. Tek business guard servisi (D3 — tek kaynak)

### `StudioWorkingHoursService` (Application/Features/Studios veya .../WorkingHours)

Sorumluluk (tek kaynak; kural hiçbir yerde tekrar yazılmaz):

```
Task<StudioWorkingHoursMap> LoadAsync(studioId, ct)
    → 7 günü (kayıt yoksa fallback) tek sözlükte döner; akışlar tek sorguyla yükler.

bool IsOpenAt(map, DateTime startLocal)           // gün kapalı değil + start o günün penceresinde
bool FitsWithin(map, DateTime startLocal, DateTime endLocal)
    // start VE end aynı günün [OpensAt, ClosesAt] aralığında (end == ClosesAt tam sınır KABUL)
(TimeOnly Open, TimeOnly Close)? WindowFor(map, DayOfWeek)   // kapalıysa null
```

**Önemli davranış:** `FitsWithin` — başlangıç ve bitiş **tamamen** aynı çalışma günü penceresinde olmalı.
- Örn. Studio 09:00–22:00, 21:30 + 60dk → end 22:30 > 22:00 → **reddet**.
- `end == ClosesAt` (örn. 21:00 + 60dk = 22:00) → **kabul** (yarı-açık değil; tam kapanışta biten ders geçerli).
- Gece aşan ders zaten OpensAt<ClosesAt kuralıyla tek gün içinde kaldığından "ertesi güne taşan ders" guard ile reddedilir.

Bu servis **availability pencere kırpmasında, session guard'ında ve generate/replan skip'inde** ortak kullanılır.

---

## 5. Akış bazında etki

### A. Availability / Boşluk Bul (`FindClassTypeSlots.cs`)

- Statik `WorkStart/WorkEnd` (43-44) **kaldırılır**; her gün için servis penceresi kullanılır.
- Günlük döngüde (147-159):
  - Gün **kapalı** → `continue` (o gün hiç boşluk yok).
  - Açık → `dayWorkStart = OpensAt`, `dayWorkEnd = ClosesAt` (gün-bazlı, statik değil).
  - `domainStart/domainEnd` mantığı aynen korunur (past + süre çıkarımı).
- Closure/equipment/hoca/öğrenci/past mantığı **aynen** (sadece pencere kaynağı değişir).
- Sonuç: kapalı günde boşluk dönmez; 09:00 açılışta 07:00 boşluğu dönmez; 22:00 kapanışta 21:30+60dk dönmez.

### B. Ders oluşturma (`CreateClassSession`, `BulkGenerateClassSessions`)

- `SessionRules.EnsureValidAsync`'e (veya handler'da guard çağrısına) **çalışma-saati kontrolü** eklenir: `FitsWithin(map, startAt, endAt)` değilse **409/400** (`ConflictException` — "Seans saati stüdyo çalışma saatleri dışında." / kapalı gün için "Bu gün stüdyo kapalı.").
- `BulkGenerate`: per-slot skip listesine yeni neden `"outside-working-hours"` (past/conflict skip deseni ile aynı).
- Geçmiş ders kuralları (past guard) korunur.

### C. Güncelleme / erteleme / reschedule (`SessionMutations.cs`)

- `StartAt` değişiyorsa yeni `start/end` için `FitsWithin` → değilse 409.
- Mevcut ders geçmişteyse mevcut kurallar korunur (zaten Scheduled değilse update/reschedule reddediliyor; geçmişe taşıma past-guard ile engelli).

### D. Contract generation (`ContractLineScheduleEngine.RunAsync`)

- `RunAsync` skip zincirine (240-241 hizası) **çalışma-saati skip** eklenir: aday `c` için `FitsWithin(map, c.StartAt, c.EndAt)` değilse `skipped++; continue`.
- Sonuç DTO'su zaten `skipped` döndürüyor; "çalışma saati dışı" sayısı buraya dahil olur. İstersek ayrı sayaç/uyarı (örn. `skippedOutsideHours`) ile UI'da "N ders çalışma saati dışında kaldı" gösterilebilir (opsiyonel iyileştirme — §10/S4).
- **Preferred slot çalışma saati dışındaysa:** generate o adayı skip eder → eksik plan. UI önceden uyarır (D7).
- **Tüm slotlar çalışma saati dışındaysa:** hiçbir ders üretilmez → `created=0`, `skipped=N`. Kullanıcı-dostu mesaj/uyarı UI'da (D6).

### E. Sprint 34 replan (`ContractLineReplan.cs`)

- RunAsync'i kullandığından **otomatik uyumlu** (ayrı kod yok). Yeni çalışma saatlerine göre yerleşir; çalışma-dışı slotlar skip.
- **Completed/geçmiş dokunulmaz** (Phase 1 yalnız `≥ nowLocal` future Scheduled detach). Bu kural **bozulmaz**.
- **Replan preview** (`ReplanLinePreviewQuery`): preview'da da aynı `FitsWithin` ile "plannable/notPlannable" sayımı çalışma saatini hesaba katmalı (preview ile apply tutarlı olsun). → `ReplanLinePreviewDto.Warnings`'e "N slot çalışma saati dışında" eklenebilir.

### F. Schedule UI (grid)

- Grid bugün saat penceresini çizmiyor (zaman-sıralı akış). MVP:
  - Haftalık çalışma saatleri okunur (yeni `GET`).
  - Grid bilgi şeridi / gün başlıkları: her gün için açılış–kapanış etiketi; **kapalı gün → "Kapalı"** rozeti.
  - **Çalışma dışı mevcut dersler GİZLENMEZ** (eski veri olabilir); istenirse soluk/uyarı rengi + tooltip "çalışma saati dışında".
  - Availability overlay (Sprint 36) zaten backend'den geliyor → backend pencere değişince overlay otomatik doğru (kapalı günde boşluk gelmez).
- Farklı günlerin farklı saatleri: MVP'de grid bir global min/max kullanıyorsa → **min = haftadaki en erken açılış, max = en geç kapanış**; gün başlığında o güne özel pencere yazılır.

---

## 6. API önerisi

Mevcut `StudioController` (`api/studio`, `[Authorize]`; `Update` → `Owner,StudioAdmin`) deseni izlenir.

```
GET  /api/studio/working-hours      → StudioWorkingHoursDto (7 gün; kayıt yoksa fallback 7×07:00–23:00 döner)
PUT  /api/studio/working-hours      → full-replace 7 gün; Owner,StudioAdmin
```

**PUT body:**
```json
{ "days": [
  { "dayOfWeek": "Monday",   "isClosed": false, "opensAt": "09:00", "closesAt": "22:00" },
  { "dayOfWeek": "Sunday",   "isClosed": true,  "opensAt": null,    "closesAt": null }
] }
```

- **Yetki:** yazma `Owner,StudioAdmin` (UpdateStudio ile aynı). Okuma **tüm authenticated roller** (instructor grid/availability için okumalı; ayrıca availability query'si zaten studio-scoped çalışıyor, working-hours okuması role engeli olmadan yapılmalı).
- **Konum:** `StudioController` altına 2 endpoint; feature `Features/Studios/StudioWorkingHours*.cs` (veya yeni `Features/WorkingHours`).
- **Komut/Query:** `GetStudioWorkingHoursQuery`, `UpdateStudioWorkingHoursCommand(IReadOnlyList<WorkingHourDayInput> Days)` (full-replace: mevcut 7 kaydı RemoveRange + 7 yeni — `StudentContractLinePreferredSlot` full-replace deseni gibi).
- **Validator:** 7 gün hepsi present + tekrarsız; `IsClosed=false` günlerde `OpensAt`/`ClosesAt` zorunlu + `OpensAt < ClosesAt`; `IsClosed=true` günlerde saat null/ignored.

---

## 7. Business guard yaklaşımı (özet)

- **Tek servis** `StudioWorkingHoursService` (§4). DI ile `CreateClassSession`, `SessionMutations`, `BulkGenerate`, `FindClassTypeSlots`, `ContractLineScheduleEngine.RunAsync` (parametre veya servis enjeksiyonu) ve replan preview'a verilir.
- `RunAsync` statik bir engine metodu; servisi doğrudan enjekte edemez → ya (a) çağıran handler `map`'i yükleyip parametre olarak geçirir (`StudioWorkingHoursMap? workingHours = null`, null → fallback), ya da (b) `db`'den içeride yükler. **Öneri (a):** handler yükler, RunAsync'e opsiyonel param geçer (preview/generate/replan üçü de tutarlı). Param null ise fallback (07:00–23:00) → eski testler bozulmaz.
- Kural **tek yerde**: "içeride mi" mantığı yalnız `FitsWithin`/`IsOpenAt`'te.

---

## 8. UI önerisi

### 8.1 Ayarlar → "Çalışma Saatleri" bölümü (`settings/page.tsx`)

- 7 gün listesi; her gün: **Açık/Kapalı toggle**, **Açılış** (time input), **Kapanış** (time input).
- Kısayollar: "Tüm hafta içi aynı yap", "Cumartesi/Pazar kapalı".
- Kaydet → `PUT /api/studio/working-hours` (full-replace).
- Validation (frontend + backend): kapalı günde saat zorunlu değil; açık günde açılış/kapanış zorunlu + açılış<kapanış; saat adımı 5 veya 15 dk (`step`).
- Precedent: contracts `new/page.tsx` gün/saat editörü (Select gün + `<input type="time">` + chip) — aynı bileşen dili.

### 8.2 Schedule grid

- Gün başlıklarında pencere/"Kapalı" etiketi; global min/max = en erken açılış / en geç kapanış (D9).
- Çalışma-dışı mevcut dersler gizlenmez; soluk/uyarı.

### 8.3 Generate/replan/contract UI

- Line oluşturma/düzenleme: preferred slot çalışma saati dışındaysa **uyarı rozeti** ("Bu slot Pazartesi çalışma saatleri dışında — program üretiminde atlanır").
- Replan preview özetinde "N slot çalışma saati dışında, planlanamadı" uyarısı.

### 8.4 Tipler / servis (`domain-types.ts`, `studioApi.ts`)

- `StudioWorkingHoursDto { days: WorkingHourDayDto[] }`, `WorkingHourDayDto { dayOfWeek, isClosed, opensAt?, closesAt? }`, `WorkingHourDayInput`.
- `studioService.getWorkingHours()`, `studioService.updateWorkingHours(input)` (mevcut `studioService` deseni).

---

## 9. Edge case kararları

| Durum | Karar |
|---|---|
| Kapalı güne preferred slot eklenir | Slot kaydedilebilir; generate/replan **skip**; UI **uyarı** (D7). Kayıt engellenmez (esneklik + ileride saat değişebilir). |
| Çalışma saati değişince mevcut gelecek dersler | Otomatik iptal/taşıma **YOK**; yalnız bilgilendirme (D8). İsteyen replan/closure ile düzeltir. |
| Replan sonrası | Yeni çalışma saatlerine göre yerleşir; dışı slotlar skip (E). |
| Gece aşan saat (22:00–02:00) | **Kapsam dışı**; `OpensAt < ClosesAt` reddeder (D4). |
| Farklı gün farklı saat (grid) | Global min/max + gün-bazlı etiket (D9). |
| Pazar kapalı | Availability `continue` (boşluk yok); create/generate/replan o günde reddeder/skip eder. |
| Instructor availability × studio working hours | **Kesişim** kullanılır: studio working hours **hard** (enforce). Instructor availability bugün **advisory** (Sprint 26) → enforce edilmez; ama availability sorgusu zaten studio penceresiyle sınırlanınca instructor advisory'nin kapsamı doğal daralır. Bu sprint instructor availability'yi hard yapmaz (kapsam dışı). |
| `end == ClosesAt` | Geçerli (tam kapanışta biten ders kabul). |
| Fallback (kayıt yok) | 07:00–23:00 açık (D2/D3). |

---

## 10. Test planı (spec'e dahil)

### A. Working hours CRUD
- 7 gün kaydedilir (PUT) → GET 7 gün döner.
- Kapalı gün kaydedilir → IsClosed=true, saat null.
- Açılış ≥ kapanış → **422/400** reddedilir.
- Açık günde saat eksik → **422/400**.
- Kayıt yokken GET → fallback 7×07:00–23:00.

### B. Availability
- Studio 09:00–22:00 → 07:00'de boşluk dönmez.
- 21:30 60dk → boşluk dönmez (end 22:30 > 22:00).
- Kapalı gün → boşluk dönmez.
- Closure + working hours birlikte (her ikisi de eler).

### C. Manuel session create/update/reschedule
- Çalışma saati içinde create → 200/201.
- Çalışma saati dışında create → **409**.
- Bitiş kapanışı aşıyor (21:30+60dk, kapanış 22:00) → **409**.
- Kapalı günde create → **409**.
- Reschedule çalışma saati dışına → **409**.
- `end == ClosesAt` tam sınır → 200.

### D. Generate / replan
- Preferred slot çalışma saati içinde → üretir.
- Preferred slot çalışma saati dışında → skip / eksik plan (created az, skipped artar).
- Tüm slotlar dışı → created=0.
- Replan yeni çalışma saatlerine uyar.
- **Completed/geçmiş korunur** (replan Phase 1 yalnız future Scheduled).

### E. UI (manuel/checklist)
- Settings working hours ekranı (toggle + time + kısayol + kaydet).
- Schedule grid açılış/kapanış etiketi + "Kapalı".
- Availability overlay çalışma saatine göre.

### F. Regresyon
- `dotnet build` 0/0 · `tsc` 0 · `next build` 0 · `has-pending` clean.
- sprint32c (availability), sprint36, sprint35/34/33, schedule create/update/reschedule, booking/makeup/contract-schedule smoke'ları **yeşil** (fallback sayesinde working-hours kaydı olmayan smoke'lar 07:00–23:00 davranışını korur → kırılmamalı).
- **Risk:** Mevcut smoke'lar 07:00 öncesi / 23:00 sonrası saat üretiyorsa kırılır → kontrol et; gerekirse smoke saatlerini pencere içine çek (yalnız test verisi).

---

## 11. Migration ihtiyacı — özet

- **Tek yeni tablo** `StudioWorkingHours` (+ unique index `(StudioId, DayOfWeek)`). Migration adı: **`Sprint37_StudioWorkingHours`**. Additive; drop/alter yok; **backfill yok**.
- Diğer her şey şema gerektirmez (servis + guard + DTO + UI).
- `DbSet<StudioWorkingHour>` → `AppDbContext` + `IAppDbContext`.

---

## 12. Açık sorular (karar/öneri)

1. **Demo seed pencere:** seed 7 gün ne olsun? (Öneri: Pzt–Cmt 09:00–22:00, Paz kapalı — ya da sade 7×07:00–23:00. Mevcut studio'lara dokunmaz.)
2. **Generate/replan ayrı sayaç:** "çalışma saati dışı skip" ayrı sayaç (`skippedOutsideHours`) + UI uyarısı eklensin mi, yoksa mevcut `skipped`'a karışsın mı? (Öneri: ayrı sayaç — daha iyi UX, küçük DTO eki.)
3. **Instructor availability hard-enforce:** bu sprintte instructor availability advisory kalsın mı? (Öneri: kalsın — yalnız studio working hours hard; kesişim doğal.)
4. **Faz bölme:** tek sprint mi, yoksa (37A = tablo+servis+guard+API+backend akışlar, 37B = settings UI + grid göstergesi) mi? (Öneri: backend ağırlıklı tek sprint; UI aynı sprintte — tek migration.)
5. **Kapalı güne create kullanıcı mesajı:** "Bu gün stüdyo kapalı." ayrı mı, "çalışma saati dışında" tek mesaj mı? (Öneri: kapalı gün için ayrı, net mesaj.)
6. **Bulk generate skip nedeni etiketi:** `"outside-working-hours"` string yeterli mi (mevcut "past"/"conflict" deseni)? (Öneri: evet.)

---

## 13. Önerilen ilk uygulama kapsamı (onay sonrası, ayrı turda)

Backend: `StudioWorkingHour` entity + config + **migration `Sprint37_StudioWorkingHours`** (additive); `DbSet` (AppDbContext+IAppDbContext); `StudioWorkingHoursService` (`LoadAsync/IsOpenAt/FitsWithin/WindowFor`, fallback 07:00–23:00); `GetStudioWorkingHoursQuery` + `UpdateStudioWorkingHoursCommand` (full-replace, validator) + 2 controller endpoint; guard entegrasyonu: `FindClassTypeSlots` (statik sabit → servis penceresi), `CreateClassSession`/`SessionMutations`/`BulkGenerate` (FitsWithin → 409/skip), `ContractLineScheduleEngine.RunAsync` (map param + skip), replan preview tutarlılığı.
Frontend: settings "Çalışma Saatleri" bölümü (7 gün toggle+time+kısayol+kaydet); `studioService.get/updateWorkingHours` + tipler; schedule grid gün etiket/"Kapalı"; generate/replan/line slot uyarıları.
Doğrulama: build 0/0 · tsc/next 0 · migration apply + `has-pending` clean · `sprint37-smoke` (A–D) · regresyon (32c/36/35/34/33 + schedule/booking/makeup/contract-schedule). Commit/push/tag yalnız ayrı onayla.

> **Bu spec analiz turudur. Şu an kod/migration/commit YOK.** Uygulama yalnız onaylanan kararlarla başlar.

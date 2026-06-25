# SPRINT 36 — Ders Programında Dersler + Boş Zamanları Birlikte Gösterme

> **Durum:** UYGULANDI (frontend) — final doğrulama yapıldı; **commit/push/tag bekliyor (henüz YOK).** **Backend değişikliği YOK · Migration YOK.**
> **Ön koşul:** Sprint 35 (`cc3ae98`). HEAD = `cc3ae98`.
> **İhtiyaç:** Sprint 32C "Boşluk Bul" (`/dashboard/find-availability`) ayrı sayfada uygun boşlukları gösteriyor. Kullanıcı bu boşlukları **Ders Programı (`/dashboard/schedule`)** ekranında, mevcut derslerle **aynı takvim görünümünde** görüp kıyaslayabilmek ve boş slot'a tıklayıp ders oluşturabilmek istiyor.
>
> **NOT:** §0–§11 ilk overlay tasarımının spec'idir (referans). Uygulama sırasında kapsam UX iyileştirmeleriyle genişledi — **bağlayıcı final kapsam için bkz. §12.** Çelişki olursa **§12 geçerlidir** (ör. takvim artık saat-kovası değil **oransal blok**; "Adım" alanı kaldırıldı; "Bu saatte ders aç" aynı sayfada modal prefill ile çalışır).

---

## 0. TL;DR — En kritik bulgu

**Bu sprint %100 FRONTEND. Backend değişikliği YOK, migration YOK, yeni endpoint YOK.**

| Soru | Cevap |
|---|---|
| Mevcut availability endpoint schedule overlay'i için yeterli mi? | ✅ **EVET, birebir yeterli.** Aynı `GET /api/studio/availability/class-type-slots` ve aynı `AvailabilityWindowDto` schedule için de uygun. |
| Backend gerekiyor mu? | ❌ **HAYIR.** |
| Migration gerekiyor mu? | ❌ **HAYIR.** |
| "Bu saatte ders aç" derin bağlantısı zaten var mı? | ✅ **EVET.** Schedule sayfası `newAt/classTypeId/instructorId/capacity` URL paramlarını okuyup `CreateSessionModal`'ı ön-dolduruyor ([schedule/page.tsx:142-155](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/page.tsx#L142-L155)). Sprint 32C `openCreate` bunu kullanıyor. |
| Tek-takvim overlay için ne gerekiyor? | Frontend'de: (1) "Boşlukları Göster" panel + toggle, (2) `WeekTimetable`'a `availabilityWindows` prop'u + dashed "Boşluk" kartı, (3) slot kartından `CreateSessionModal`'ı doğrudan (URL round-trip'siz) ön-dolu açma. |

**Sonuç:** İş, mevcut iki parçayı (availability servisi + create prefill) **aynı ekranda birleştirmek**. Her ikisi de zaten production'da çalışıyor; yeni hiçbir veri/algoritma yok.

---

## 1. Mevcut yapı analizi (senin 1. başlığın — kod temelli)

### S1. Ders Programı sayfası / route
[`src/app/(dashboard)/dashboard/schedule/page.tsx`](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/page.tsx) — `SchedulePage` ("use client"). Üst filtre Card'ı + masaüstü `WeekTimetable` + mobil `MobileAgenda` + ~10 inline modal. Stil: **Tailwind v4** (CSS module/styled yok).

> ⚠️ `pliaformafrontend/AGENTS.md`: "This is NOT the Next.js you know" — kod yazılırken `node_modules/next/dist/docs/` okunmalı. (Bu sprint spec; kod uygulama aşamasında geçerli.)

### S2. ClassSession listesi nasıl çekiliyor?
`classSessionService.list({ from, to, instructorProfileId, classTypeId })` → `GET /api/studio/class-sessions` ([studioApi.ts:224-232](../../pliaformafrontend/src/lib/studioApi.ts#L224-L232)). React-query/SWR YOK — **düz fetch + manuel state**. `load()` `Promise.all` ile sessions + pending makeup talepleri + bookings çeker ([page.tsx:84-107](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/page.tsx#L84-L107)); tetik bağımlılığı `[from, to, instructorFilter, classTypeFilter]`.

### S3. Günlük/haftalık görünüm nasıl render ediliyor?
[`_components/timetable.tsx`](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx). `WeekTimetable` (satır 122-239): CSS grid, sütun = gün (`DAY_W=158px`), satır = **saat** (`HOUR_W=60px`). Saat aralığı **otomatik ölçekleniyor** — `DEFAULT_FROM=8`, `DEFAULT_TO=21`; sessions/requests dışına taşarsa genişliyor ([timetable.tsx:126-131](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L126-L131)). Hücreler `(gün, saat)` anahtarıyla map'leniyor — **o saatte BAŞLAYAN** kayıtlar ([timetable.tsx:136-144](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L136-L144)). Mobil = günlük dikey ajanda. Gerçek "aylık" görünüm yok; `days` = `from..to` arası tüm günler, **max 62 gün** ([page.tsx:124-130](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/page.tsx#L124-L130)).

### S4. CreateSessionModal nasıl açılıyor?
page.tsx içinde inline `CreateSessionModal`. Açılış yolları:
1. **"+ Yeni seans"** butonu → `createOpen=true`.
2. **Boş hücre tıklama** → `onEmptyClick(date, hour)` → `quickAt` ([timetable.tsx:184](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L184)).
3. **Derin bağlantı** → `prefill` state ([page.tsx:142-155](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/page.tsx#L142-L155)).

Prop/prefill: `initialStartAt` (datetime-local, `slice(0,16)`), `initialClassTypeId`, `initialInstructorId`, `initialCapacity`. Submit → `classSessionService.create(...)`. Yetki: **create yalnız Owner/StudioAdmin**.

### S5. Sprint 32C availability endpoint'i nasıl çalışıyor?
`GET /api/studio/availability/class-type-slots` ([AvailabilityController.cs](../src/Pliaforma.Api/Controllers/AvailabilityController.cs)) → `FindClassTypeSlotsHandler` ([FindClassTypeSlots.cs](../src/Pliaforma.Application/Features/Availability/FindClassTypeSlots.cs)).
Query: `classTypeId` (zorunlu), `date` **veya** `from`/`to`, `durationMinutes` (zorunlu), `requiredCapacity` (zorunlu), `instructorId?`, `incrementMinutes?` (default 15, min 5).
İçeride dikkate alınanlar: **studio closures**, **hoca çakışması** (Scheduled seans + Pending telafi talebi), **ekipman kapasitesi** (sweep-line peak), **geçmiş saat**, **çalışma saatleri 07:00–23:00**. **Max aralık 31 gün** (`MaxRangeDays=31`, aşılırsa `BusinessRuleException`).
Yetki: **Owner/StudioAdmin/Instructor** (Instructor görebilir; ama create edemez).

### S6. find-availability sayfası service & UI
[`find-availability/page.tsx`](../../pliaformafrontend/src/app/(dashboard)/dashboard/find-availability/page.tsx). Servis: `availabilityService.classTypeSlots(q: AvailabilityQuery)` → `AvailabilityWindowDto[]` ([studioApi.ts:112-125](../../pliaformafrontend/src/lib/studioApi.ts#L112-L125)). UI = filtre formu + sonuç **tablosu** (Tarih / Başlangıç aralığı / Süre / Ekipman kapasitesi / Müsait hocalar / İşlem). "İşlem" = `openCreate(w, w.availableInstructors[0]?.id)` → `router.push("/dashboard/schedule?newAt=...&classTypeId=...&instructorId=...&capacity=...")`.

### S7. Schedule deep-link prefill şu an nasıl çalışıyor?
Mount'ta `URLSearchParams` okunur; `newAt` varsa `prefill` set edilir ve `history.replaceState` ile URL temizlenir (yenilemede tekrar açılmasın). **Bu mekanizma Sprint 36'da slot kartı için yeniden kullanılacak — ama aynı sayfa içinde olduğumuzdan URL round-trip'e gerek yok; doğrudan state set edilecek.**

### S8. Equipment guard / instructor availability / closure logic nerede?
- **Availability tarafı** (okuma): hepsi `FindClassTypeSlots.cs` içinde (closures, IsFree, PeakUsed).
- **Create tarafı** (yazma/guard): `CreateClassSession.cs` → `SessionRules.EnsureValidAsync` (geçmiş saat + hoca çakışması) + `EquipmentCapacityGuard.EnsureCanReserveAsync` ([EquipmentCapacityGuard.cs](../src/Pliaforma.Application/Features/Equipments/EquipmentCapacityGuard.cs)). Çakışma → **HTTP 409 `ConflictException`** (TR mesaj). Equipment satırı `FOR UPDATE` kilitli (atomik).

### S9. Availability window output formatı schedule görünümüne uygun mu?
**Evet, ama ufak bir uyum gerekiyor.** Window bir **aralık**: `startWindowFrom`–`startWindowTo` (başlangıç saatleri aralığı), sabit `durationMinutes`. Schedule grid'i ise kayıtları **başlangıç saatinin saat-kovasına** koyuyor. Dolayısıyla bir window doğal olarak `hourOf(startWindowFrom)` kovasına yerleşir — **makeup talepleri için zaten kullanılan dashed kart deseniyle birebir aynı** ([timetable.tsx:219-230](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L219-L230)). Tek dikkat: autoscale (`fromH/toH`) şu an window'ları saymıyor → window 07:00 gibi 8'in altındaysa satırı olmaz; **autoscale'e window'lar da eklenmeli** (bkz. §3.3).

**`AvailabilityWindowDto` ([domain-types.ts:62-76](../../pliaformafrontend/src/lib/domain-types.ts#L62-L76)):**
```ts
interface AvailabilityWindowDto {
  date: string;                       // YYYY-MM-DD
  startWindowFrom: string;            // ISO (en erken başlangıç)
  startWindowTo: string;              // ISO (en geç başlangıç)
  durationMinutes: number;
  requiredCapacity: number;
  availableEquipmentCapacity?: number | null;  // null = ekipman kısıtı yok
  availableInstructors: { id: string; name: string }[];
}
```

---

## 2. Ürün davranışı

Schedule üst filtre alanına **"Boşlukları Göster"** toggle + (açıkken görünen) küçük bir kriter paneli eklenir.

**Panel alanları (toggle açıkken):**
| Alan | Zorunlu | Not |
|---|---|---|
| Ders tipi (boşluk için) | ✅ Evet | Session filtresindeki `classTypeFilter`'dan **ayrı**; varsa ondan ön-seed edilir (bkz. §8) |
| Süre (dk) | ✅ Evet | Default = ders tipinin `defaultDurationMinutes` |
| Gerekli kapasite | ✅ Evet | Default 1 |
| Hoca | ❌ Opsiyonel | Boş = tüm uygun hocalar |
| Adım (incrementMinutes) | ❌ Opsiyonel | Default 15 (MVP'de gizli/sabit tutulabilir) |
| "Boşlukları yenile" butonu | — | Manuel tetik (debounce'a ek güvenlik) |

**Toggle davranışı:**
- **Kapalı** → availability **hiç çağrılmaz**, takvim bugünküyle birebir aynı (sıfır regresyon).
- **Açık** → mevcut dersler **değişmez**; uygun boşluklar takvimde **farklı bir dashed kart** olarak görünür.
- Boşluk kartı **ders kartına benzemez**; "Boşluk" etiketi + dashed kenarlık ile ayrışır.

**Boşluk kartı içeriği:**
- Saat aralığı: `10:00–12:00` (başlangıç penceresi) + `· 60 dk`
- "Boşluk" rozeti
- Uygun ekipman kapasitesi (varsa): `5 kişi`
- Uygun hoca sayısı/adı (kalabalıksa "+N")
- Kart tıklama veya "Ders oluştur" → `CreateSessionModal` (yalnız Owner/StudioAdmin; Instructor için buton gizli/pasif).

---

## 3. Görsel / UX — window'u takvimde nasıl göstermeli?

### Karar: **C) İlk MVP'de tek pencere kartı göster, tıklayınca saat seçtir** ✅ (senin önerin)

| Seçenek | Değerlendirme |
|---|---|
| A) Tek "uygun aralık" kartı | Basit ama saat seçimi belirsiz |
| B) Increment slotlara böl | Bir window'u 15 dk'lık 8+ karta böler → grid'i boğar, görsel gürültü, performans |
| **C) Tek pencere kartı + tıkla→saat seç** | ✅ **MVP.** Bir window = bir dashed kart; karta tıklayınca modalda `startWindowFrom..startWindowTo` arası başlangıç seçilir. B'nin netliğini A'nın sadeliğiyle verir. |

### 3.1 Kart tasarımı (makeup-request desenini taklit et)
Mevcut dashed makeup kartı ([timetable.tsx:219-230](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L219-L230)) tam referans. Boşluk kartı için **farklı renk tonu** (örn. `success`/`info` dashed) — makeup `warning`'den ve ders `primary`'den ayrışsın:
```
border-dashed border-success/60 bg-success/8   →  "Boşluk" overlay
border-dashed border-warning/70 bg-warning/10  →  makeup talebi (mevcut)
border-primary/40 bg-primary-soft/40           →  ders (mevcut)
```
İçerik: `10:00–12:00 · 60 dk` / "BOŞLUK" rozeti / `🪑 5 kişi` / `2 hoca uygun`.

### 3.2 Yerleşim
Window kartı, `hourOf(startWindowFrom)` saat-kovasına, o gün sütununa konur (sessions/makeup ile aynı hücrede **altlı-üstlü stack**). Aynı saatte birden çok window (farklı kapasite/hoca seti → backend ayrı window döndürür) doğal olarak stack'lenir.

### 3.3 Gerekli `WeekTimetable` değişiklikleri (kod uygulama notu)
- Yeni prop: `availabilityWindows?: AvailabilityWindowDto[]` + `onSelectWindow?(w)`.
- `fromH/toH` autoscale'ine window saatlerini ekle ([timetable.tsx:126-131](../../pliaformafrontend/src/app/(dashboard)/dashboard/schedule/_components/timetable.tsx#L126-L131)) — yoksa erken/geç pencere satırsız kalır.
- `cell` map'ine window'ları `(date, hourOf(startWindowFrom))` ile ekle.
- Render: sessions → reqs → **windows** sırası.
- Mobil `MobileAgenda`'ya da paralel ekleme (MVP'de masaüstü öncelik; mobil en azından bozulmamalı).

### 3.4 Saat seçimi (window içinde)
`startWindowFrom == startWindowTo` ise tek başlangıç → doğrudan prefill.
`startWindowFrom != startWindowTo` ise modalda başlangıç seçilir:
- **MVP:** modal `initialStartAt = startWindowFrom` ile açılır; mevcut `datetime-local` input kullanıcının pencere içinde elle ayarlamasına izin verir (min/max ipucu metni gösterilir).
- **İyileştirme (opsiyonel):** `incrementMinutes` adımlı, `[from..to]` ile sınırlı bir **başlangıç saati dropdown'u** (10:00 / 10:15 / 10:30…). MVP'de C şıkkı için datetime-local yeterli; constrained select fast-follow.

---

## 4. Create session entegrasyonu

Slot kartı → "Ders oluştur" akışı **mevcut prefill altyapısını** yeniden kullanır, ama **aynı sayfada olduğumuz için URL'e gitmeden** doğrudan state set edilir:

```
onSelectWindow(w) → setPrefill({
  startAt: w.startWindowFrom.slice(0,16),
  classTypeId: <panel.classTypeId>,
  instructorId: <seçili veya w.availableInstructors[0]?.id>,
  capacity: String(<panel.requiredCapacity>),   // bkz. ⚠️ kapasite kararı
}) → CreateSessionModal açılır (initial* prop'larıyla)
```

- **classTypeId**: panelden ön-dolu.
- **capacity**: ⚠️ **Karar gerek.** find-availability `openCreate` `availableEquipmentCapacity ?? requiredCapacity` gönderiyor (yani **uygun max kapasite**). Schedule overlay için **kullanıcının istediği `requiredCapacity`** ile ön-doldurmak daha doğru (niyet = aradığı kapasite, mevcut max değil). **Öneri: `requiredCapacity`.** (İki ekran arası tutarsızlığı not düş; istenirse find-availability de aynı hizaya çekilir — ayrı küçük iş.)
- **instructorId**: Tek uygun hoca → otomatik. Çok hoca → (a) kartta küçük dropdown ile seç, **veya** (b) ilkini prefill edip modalda değiştir. **Öneri MVP:** ilkini prefill et; modaldaki hoca select'inde değiştirilebilir. ⚠️ **Uyarı:** Modalın hoca select'i **TÜM** hocaları listeler, yalnız uygun olanları değil — kullanıcı uygun-olmayan bir hocayı seçerse create-time guard 409 verir (kabul edilebilir; §7).
- **duration**: `CreateSessionModal` süreyi şu an `startAt`'tan bağımsız almıyor — seans süresi ders tipinden/backend'den türetiliyor. Panel süresi ile ders tipi default süresi farklıysa bunu netleştir (bkz. §10 Açık sorular).

---

## 5. API ihtiyacı

**Mevcut endpoint yeterli — yeni endpoint AÇILMAYACAK.** Gerekçe:
- Schedule overlay'in ihtiyacı olan tüm alanlar (`date`, `startWindowFrom/To`, `durationMinutes`, `availableEquipmentCapacity`, `availableInstructors`) `AvailabilityWindowDto`'da zaten var.
- Tarih aralığı `from`/`to` ile geçilebiliyor (schedule view aralığıyla birebir).
- Closure/conflict/equipment/past hepsi backend'de hesaplanıyor → frontend ham window'ları map'lemekten başka bir şey yapmaz.

**Frontend mapper (tek gerçek "yeni" kod):** `AvailabilityWindowDto[]` → grid hücre modeli. Backend dönüşümü gerekmez.

**Neden backend-friendly endpoint gerekmiyor:** Window formatı zaten takvim-dostu (tarih + saat aralığı). Tek farklılık grid'in saat-kovası yerleşimi, o da saf frontend.

---

## 6. Performans

| Kural | Uygulama |
|---|---|
| Toggle kapalı → çağrı yok | Availability fetch yalnız `showAvailability === true` iken |
| Filtre değişince debounce | classType/duration/capacity/instructor değişiminde ~400ms debounce |
| Takvim aralığı değişince yeniden çağır | `from`/`to` değişiminde (week nav) toggle açıksa yeniden fetch |
| Max 31 gün kuralı korunsun | Availability backend zaten 31 gün sınırlı. Schedule `days` 62 güne kadar çıkabiliyor → **toggle açıkken aralık >31 ise overlay'i kapat + uyarı göster** ("Boşluk gösterimi en fazla 31 günlük aralıkta çalışır") |
| Haftalık görünüm 7 gün ideal | Schedule default `from..from+7` (8 gün) → tek çağrı, ucuz |
| Aylık dikkat | Gerçek aylık görünüm yok; 31 gün clamp'i bunu zaten karşılar |

**Ek:** Availability fetch'i sessions `load()`'undan **ayrı** bir effect'te tut (sessions her filtre değişiminde yeniden çekiliyor; availability'yi gereksiz tetikleme). Eski isteği iptal etmek için basit bir "son istek kazanır" (request id / AbortController) koruması iyi olur.

---

## 7. Edge case'ler

| Durum | Davranış |
|---|---|
| Studio closure | Backend zaten kapanış saatini window'dan çıkarıyor → overlay'de görünmez. Ek iş yok. |
| Geçmiş saatler | Backend geçmişi elemiş → görünmez. (Bugünün geçmiş saatleri de.) |
| Hoca çakışması | Backend çakışan hocayı o window'un `availableInstructors`'ından çıkarır → kart yalnız gerçekten uygun hocaları gösterir. |
| Equipment-bound kapasite | `availableEquipmentCapacity` window'da geliyor → kartta "N kişi" gösterilir; create'te 32B guard tekrar doğrular. |
| Create anında dolma (yarış) | Overlay'de uygun görünen slot create anında dolmuş olabilir → create **409** döner. UI bunu toast ile anlatmalı: *"Bu boşluk az önce doldu. Boşlukları yenileyin."* + otomatik `refreshAvailability()` öner. |
| Boşluk yok | "Bu kriterlere uygun boşluk bulunamadı." banner'ı (panelde, takvim üstünde). |
| Instructor rolü | Availability'yi **görebilir** (endpoint izin veriyor) ama create **edemez** → "Ders oluştur" butonu `isManager` değilse gizli/pasif. |
| Aralık >31 gün | Overlay devre dışı + uyarı (§6). |
| Süre 0/negatif, kapasite <1 | Panel client-side validation; geçersizse fetch yapma. |

---

## 8. UI konumu — senior önerisi

**Öneri: Üst filtre Card'ı içinde, açılır kriter satırı (inline panel).** Drawer DEĞİL.

**Gerekçe:**
1. Kıyaslama **takvimin üzerinde** oluyor; sağ drawer ekranı daraltıp takvimi küçültür ve mevcut `SessionDetailDrawer` ile **çakışır** (ikisi aynı anda sağda açık olamaz).
2. Mevcut filtreler (tarih nav, hoca, ders tipi) zaten üstteki Card'da — "Boşlukları Göster" doğal olarak oraya komşu.
3. Toggle kapalıyken panel collapse → görsel sadelik korunur.

**Yerleşim:**
```
[ ‹ Hafta ›  Bu hafta ]  [from] [to]  [Hoca▾] [Ders tipi▾]  [⛶]  [+ Yeni seans]
─────────────────────────────────────────────────────────────────────────────
[ ◻ Boşlukları Göster ]   ← toggle (kapalı: sadece bu satır)
   ↓ (açık)
[ Boşluk ders tipi▾ ] [Süre dk] [Kapasite] [Hoca▾ (ops)] [Adım▾] [ Yenile ]
   → "Boşluk: dashed yeşil kart" legend'i + (boşsa) "bulunamadı" mesajı
```

**classType ayrımı:** Üstteki `classTypeFilter` mevcut **dersleri süzer**; panel "Boşluk ders tipi" **boşluk arar** — kavramsal olarak ayrı, bu yüzden ayrı alan. Ama UX kolaylığı için panel açılırken `classTypeFilter` doluysa ondan **ön-seed** et (kullanıcı değiştirebilir).

**Legend zorunlu:** Üç kart tipini ayırt eden mini açıklama (Ders / Telafi talebi / Boşluk) — renk körlüğü için yalnız renge güvenme, etiket+dashed kullan.

---

## 9. Test planı

### A. Availability overlay
- Toggle **kapalı** → `availabilityService.classTypeSlots` **hiç çağrılmaz** (network assert).
- Toggle **açık** + classType/duration/capacity → boşluklar gelir, dashed kart render olur.
- Dersler ve boşluklar **aynı takvimde birlikte** görünür.

### B. Existing sessions (regresyon)
- Mevcut `ClassSession` kartları bozulmaz (içerik, renk, DnD, seçim).
- Boşluk kartı ders kartıyla **karışmaz** (ayrı stil + "Boşluk" etiketi).
- Toggle kapalıyken ekran bugünküyle pixel-eşdeğer.

### C. Create from slot
- Boşluk kartına tıkla → `CreateSessionModal` doğru prefill (`startAt`=startWindowFrom, classType, capacity=requiredCapacity, instructor).
- Çok hoca → kartta/modalda hoca seçilebilir.
- `from != to` window → modalda pencere içinde başlangıç ayarlanabilir; pencere dışı değer engellenir/uyarılır.

### D. Equipment
- Equipment-bound ders tipinde kartta `availableEquipmentCapacity` doğru gösterilir.
- Create anında 32B guard korunur (kapasite > uygun → 409, TR mesaj).

### E. Closure / past
- Tatil/kapanış zamanı boşluk olarak **gösterilmez** (backend filtresi; smoke).
- Geçmiş saatler **gösterilmez** (bugünün geçmiş saatleri dahil).

### F. Regression / build gates
- `dotnet build` **0/0** (backend değişmediğinden değişiklik beklenmez — yine de yeşil).
- `tsc` **0 hata**.
- `next build` **0 hata** (AGENTS.md uyarısı: Next sürümü farklı — build doğrulaması kritik).
- `has-pending` **temiz**.
- **Sprint 32C availability smoke PASS** (endpoint cevap formatı değişmedi).
- **Sprint 35 / 34 / 33 regresyon PASS**.
- **Schedule create/update regression PASS** (prefill + 409 yolu dahil).

---

## 10. Açık sorular

1. **Capacity prefill kaynağı:** `requiredCapacity` (öneri) mi, find-availability'deki gibi `availableEquipmentCapacity ?? requiredCapacity` mi? İki ekranı hizalayalım mı?
2. **Süre kaynağı:** Panel "Süre" alanı yalnız availability sorgusu için mi, yoksa create edilen seansın süresini de mi belirlemeli? `CreateSessionModal` şu an süreyi ders tipinden türetiyor; panel süresi farklıysa hangisi kazanır?
3. **Saat seçimi MVP derinliği:** datetime-local (elle, MVP) yeterli mi, yoksa ilk sürümde `incrementMinutes` adımlı constrained dropdown şart mı?
4. **classType ön-seed:** Panel "Boşluk ders tipi", üstteki `classTypeFilter`'dan otomatik seed edilsin mi, yoksa hep boş mu başlasın?
5. **Mobil kapsam:** Boşluk overlay MVP'de yalnız masaüstü `WeekTimetable`'da mı, yoksa `MobileAgenda` da ilk sürümde mi? (Öneri: masaüstü MVP, mobil fast-follow.)
6. **Instructor rolü görünürlüğü:** Instructor boşlukları görebilsin ama create butonu olmasın — onay?
7. **31 gün üstü aralık:** Overlay'i sessizce kapatmak mı (öneri), yoksa kullanıcıyı haftalık görünüme zorlamak mı?

---

## 11. Uygulama dokunulacak dosyalar (onay sonrası — referans)

| Dosya | Değişiklik |
|---|---|
| `schedule/page.tsx` | "Boşlukları Göster" state + panel, ayrı availability effect (debounce + abort + 31g clamp), `onSelectWindow`→prefill, modal entegrasyonu |
| `schedule/_components/timetable.tsx` | `availabilityWindows`/`onSelectWindow` prop, autoscale'e window, `cell` map'e window, dashed "Boşluk" kartı |
| `lib/studioApi.ts` / `lib/domain-types.ts` | **Değişiklik yok** (servis + tip zaten var) |
| Backend | **Değişiklik yok** |

**Toplam:** ~2 dosya frontend. Migration yok. Yeni endpoint yok.

---

## 12. UI Refinement & Final Scope (UYGULANDI — bağlayıcı)

İlk overlay (§0–§11) uygulanırken kapsam, kullanıcı geri bildirimiyle aşağıdaki UX iyileştirmelerini de kapsadı. **Bu bölüm Sprint 36'nın bağlayıcı final kapsamıdır.** Backend/migration hâlâ YOK; tümü frontend.

### 12.1 Sözleşme ekleme: modal → ayrı sayfa
- Yeni rota: **`/dashboard/students/{id}/contracts/new`** ([students/[id]/contracts/new/page.tsx](../../pliaformafrontend/src/app/(dashboard)/dashboard/students/[id]/contracts/new/page.tsx)).
- Öğrenci detayındaki **"+ Yeni sözleşme"** artık modal açmaz; bu sayfaya **yönlendirir** (`<Link>`).
- **Kaydet sonrası** öğrenci detayına (`/dashboard/students/{id}`) `router.push` ile döner + başarı toast'u.
- `AddContractLineModal` ve `addOpen` state'i **kaldırıldı** (öğrenci detay sayfasından + `_components.tsx`'ten).
- **Focus bug fix:** Eski modalde `Section` bileşeni **render içinde** tanımlıydı → her tuş vuruşunda yeni fonksiyon kimliği → React alt ağacı remount ediyor → numeric input focus'u kaçıyor / ekran kayıyordu. Yeni sayfada **`Section` modül seviyesinde** tanımlı; sorun çözüldü. (Kök neden index-key değildi.)
- Form alanları (ders tipi/hoca/kapasite/tarih/ders sayısı/ücret/haftalık slotlar) ve `contractService.addLine` çağrısı **birebir korundu**; yalnız kabuk modal→sayfa değişti.

### 12.2 Ders Programı takvimi: saat-kovası → ORANSAL blok
- `WeekTimetable` **baştan yazıldı**: artık satır-başı-saat ızgarası değil, **oransal (proportional) blok** sistemi.
- **Sol saat sütunu kaldırıldı.** Her blok kendi saatini yazar; arka planda **etiketsiz ince saat çizgileri** hizalama için kalır.
- Ders blokları **süresine orantılı yükseklik** alır (`HOUR_PX=52px`, `MIN_BLOCK_PX=26px` taban). 50/60/90 dk bloklar görsel olarak orantılı.
- **Çakışan bloklar yan yana şeritlenir** (`assignLanes` — standart takvim interval-partitioning; cluster içinde lane genişliği `100/lanes`).
- **Boşluk blokları görsel aralığı kaplar:** `startWindowFrom` → `startWindowTo + durationMinutes` (örn. 07:00–16:00 tek uzun blok). Etiket: `availRangeLabel`.
- **Boş alana tıklayınca hızlı oluşturma korunur:** sütun arka planına tıklama Y konumundan saati hesaplar → `onEmptyClick(date, hour)` → `CreateSessionModal` prefill.
- **Border/kontrast iyileştirildi:** dış çerçeve `border-2`, başlık altı `border-b-2`, gün ayraçları tam opak `border-border`, saat çizgileri ince (`border-border/50`).

### 12.3 Sürükle-bırak kaldırıldı → taşıma offcanvas'tan
- DnD taşıma tamamen **kaldırıldı** (`moves`/`movedIds`/`displaySessions`/`onDropSession`/`saveMoves`/`discardMoves` ve "Kaydet/Vazgeç" taşıma çubuğu temizlendi).
- Derse **sol tık VEYA sağ tık** (`onContextMenu`, tarayıcı menüsü engellenir) → **offcanvas** (`SessionDetailDrawer`).
- Ders taşıma/erteleme offcanvas içindeki **"Ertele"** aksiyonuyla (mevcut `RescheduleModal`) yapılır.

### 12.4 Boşluk paneli: "Adım" alanı kaldırıldı, süre = blok
- "Adım (dk)" seçicisi **hem Schedule panelinden hem find-availability'den kaldırıldı.**
- `incrementMinutes = ders süresi` (mola dahil blok) sabitlendi → boşluklar **ders-bloğu hizasında, temiz saatlerde** (15:05 gibi kırık dakikalar yok).
- find-availability tablosu artık **"Uygun aralık"** = `startWindowFrom – (startWindowTo + süre)` gösterir ("en geç başlangıç" ibaresi kaldırıldı).
- Schedule panelindeki "Süre" alanı **read-only** (ders tipi default süresinden); hizalama için hint kaldırıldı.

### 12.5 Boşluk süre tutarsızlığı (stale fetch) giderildi
- Ders tipi/süre değişince **eski boşluklar anında temizlenir** (`lastDurationSig` ref) → yanlış süreli kart takılı kalmaz.
- Debounce/fetch sırasında panelde **"Boşluklar yükleniyor…"** gösterilir; "bulunamadı" yalnız `!loading` iken çıkar.
- **"Boşlukları yenile"** (nonce) mevcut sonucu koruyup arkada günceller (boş-flash yok); kriter değişince temiz çekim yapılır.
- Son-istek-kazanır (`cancelled` bayrağı) korunur.

### 12.6 Değişen dosyalar (final)
| Dosya | Değişiklik |
|---|---|
| `schedule/page.tsx` | Overlay paneli + oransal render bağlama, DnD kaldırma, stale-fetch fix, slot create |
| `schedule/_components/timetable.tsx` | Oransal `WeekTimetable` (lane layout), boşluk bloğu, MobileAgenda boşluk kartı |
| `find-availability/page.tsx` | "Adım" kaldırma, "Uygun aralık" gösterimi, increment=süre |
| `students/[id]/contracts/new/page.tsx` | **YENİ** — sözleşme ekleme sayfası (focus-safe `Section`) |
| `students/[id]/page.tsx` | "+ Yeni sözleşme" → yeni sayfaya link; modal/`addOpen` kaldırıldı |
| `students/_components.tsx` | `AddContractLineModal` + kullanılmayan `plus` helper kaldırıldı |
| **Backend** | **Değişiklik YOK** · **Migration YOK** |

### 12.7 Önerilen commit & tag (henüz YAPILMADI)
- Commit: `feat: add schedule availability overlay and contract creation UX (Sprint 36)`
- Tag: `v1.27.0-pilot`
</content>
</invoke>

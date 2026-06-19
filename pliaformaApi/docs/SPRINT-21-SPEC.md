# SPRINT 21 — Calendar Reschedule, Chain-Shift & Schedule Change UX (SPEC)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md) · **Faz 4 (ilk dilim).** Sözleşme/booking/yoklama/telafi oturdu. Bu sprint
> stüdyo sahibinin **günlük programı yönetmesini** hedefler: tek ders taşıma (çakışma kontrolü tam) + **zincirleme
> kaydırma** (kullanıcı seçimiyle). Gerçek drag/drop, köklü grup değişikliği ve closure undo **bilinçli olarak
> bölünür** (aşağıda gerekçeli). **Bu tur yalnız spec.** Kod/migration/commit YOK.

## Amaç (neden)
Tek ders taşıma kısmen var (instructor çakışması kontrol ediliyor, KeepBookings telafi üretmiyor) ama **öğrenci
çakışması taşımada kontrol EDİLMİYOR** (gap) ve **zincirleme kaydırma yok**. Bu sprint bu ikisini güvenli +
migration'sız kapatır; büyük/riskli parçaları (grid drag/drop, köklü grup düzeni, closure full-undo) net tasarlayıp
sonraki sprintlere bırakır.

---

## Mevcut durum — analiz (kanıt) + NE ZATEN VAR

### Tek ders taşıma (Reschedule) — büyük ölçüde var
[SessionMutations.cs](../src/Pliaforma.Application/Features/Scheduling/SessionMutations.cs) `RescheduleClassSessionCommand(Id, NewStartAt, Reason, KeepBookings, GrantMakeupCredit)`:
- **EndAt = NewStartAt + ClassType.DefaultDurationMinutes** (otomatik). `RescheduledAt`/`RescheduleReason` set.
- **Instructor çakışması: ✓** `SessionRules.EnsureValidAsync(..., excludeId: session.Id)` → `HasConflictAsync` (aynı hoca, örtüşen Scheduled, **kendini hariç**). Geçmiş-zaman engeli + müsaitlik **HARD gate** (hoca müsaitliği varsa içine sığmalı; yoksa Owner/Admin serbest, Instructor engelli).
- **KeepBookings=true → telafi YOK** (ders taşınır, öğrenciler kalır; iptal bloğu atlanır). **Zaten garanti.**
- **KeepBookings=false → bookings iptal** + opsiyonel telafi (`StudioCancelledClass`).
- **Kapasite:** taşımada `BookedCount` dokunulmaz (re-check yok).

### **GAP — öğrenci çakışması taşımada kontrol EDİLMİYOR** ⚠️
Öğrenci çakışma kontrolü yalnız [BookingCreator.cs](../src/Pliaforma.Application/Features/Bookings/BookingCreator.cs) (yeni booking) içinde (`studentBusy`, exclude parametresi YOK). **Reschedule KeepBookings=true ile bir dersi, öğrencilerinden birinin başka dersi olan saate taşıyabilir → çakışma.** Sprint 21 bunu kapatır.

### Zincirleme kaydırma referansı (closure RescheduleByOffset) — yeniden kullanılabilir
[CreateStudioClosure.cs](../src/Pliaforma.Application/Features/Closures/CreateStudioClosure.cs) `ResolveRescheduleSkipAsync`: **batch-aware** çakışma (`!batchIds.Contains(s.Id)` → birlikte kayan seanslar kendi aralarında çakışmaz), past + outside-availability skip. Sprint 21 chain-shift bu deseni kullanır (ofset gün yerine **dakika delta** + zincir batch'i dinamik hesaplanır).

### Çakışma yardımcıları
[SessionRules.cs](../src/Pliaforma.Application/Features/Scheduling/SessionRules.cs) `HasConflictAsync(..., Guid? excludeId)` (instructor, self-exclusion **var**) + `FitsAvailabilityAsync` + `StudioNowAsync`. **Öğrenci çakışması için exclude'lu yardımcı YOK** (eklenecek).

### Frontend — kart-listesi, grid DEĞİL
[schedule/page.tsx](../../pliaformaFrontend/src/app/(dashboard)/dashboard/schedule/page.tsx): seanslar **tarihe göre gruplu Card listesi** (zaman-eksenli grid yok). `RescheduleModal` var (datetime-local + keepBookings + grant). **Drag/drop için zaman-grid takvim YOK** → gerçek dnd yeni bir grid komponenti + kütüphane ister.
- **package.json:** Next **16.2.9** + React **19.2.4**, **hiçbir dnd kütüphanesi yok**. AGENTS.md: "This is NOT the Next.js you know — breaking changes" (Next 16 riski).

### Köklü grup değişikliği referansı
[GenerateContractSchedule.cs](../src/Pliaforma.Application/Features/Contracts/GenerateContractSchedule.cs) `GenerateContractScheduleCommand(ContractId, Slots[], ReuseExistingGroup, Individual?)`: sözleşme StartDate–EndDate arası slot'lardan, kapanış/çakışma atlayarak `available` ders dolana kadar üretir. Köklü grup-düzeni değişiminin temeli (öğrenci-bazlı yeniden üretim).

---

## Sprint 21 kapsamı (UYGULANACAK)

### 1) Öğrenci çakışmasını taşımaya ekle (gap kapatma)
- **Yeni yardımcı** `SessionRules.StudentConflictAsync(db, studioId, studentProfileIds, startAt, endAt, excludeSessionIds, ct)` → verilen öğrencilerden herhangi biri, **exclude dışı** örtüşen Scheduled derste Booked mu? (BookingCreator'daki sorgu exclude'lu hale getirilip ortaklaştırılır; BookingCreator de bunu kullanır — davranış aynı.)
- **Reschedule** (KeepBookings=true): yeni slotta o dersin **booked öğrencilerinin** başka dersi varsa → **409** (`excludeSessionIds = {session.Id}`). KeepBookings=false'da bookings iptal edildiğinden öğrenci çakışması aranmaz.

### 2) Tek ders taşıma UX (modal — "Tarih/Saat değiştir") + mod A/B
- `RescheduleModal` netleşir: **mod seçimi** — **A) Sadece bu dersi taşı** (çakışma → 409) · **B) Etkilenen dersleri zincirleme kaydır**. KeepBookings varsayılan true (taşıma; telafi yok). KeepBookings=false (iptal + opsiyonel telafi) ayrı/altta kalır.
- **Drag/drop YOK** (bu sprint modal-tabanlı; gerekçe aşağıda). Bu, kullanıcının önerdiği "daha güvenli ilk adım modal" yaklaşımıdır.

### 3) Zincirleme kaydırma (B) — yeni komut
- **Yeni komut** `ChainShiftSessionCommand(SessionId, NewStartAt)` (Owner/StudioAdmin):
  1. `delta = NewStartAt − session.StartAt`.
  2. **Zincir kur:** [S]'ten başla. S yeni konuma gider. **Aynı hocanın** sonraki Scheduled seanslarını StartAt'a göre sırala; bir önceki zincir üyesinin **kaymış bitişi** sonraki seansın **orijinal başlangıcını** geçiyorsa (örtüşme) o seans da `delta` kadar kayar; geçmiyorsa (boşluk) **zincir durur**. (Closure batch deseni; batch = zincir.)
  3. **Doğrulama (her üye, batch-aware):** kaymış konum, **zincir DIŞI** instructor Scheduled seanslarıyla çakışmamalı (`HasConflictAsync` exclude=zincir) + her üyenin **booked öğrencileri** zincir dışı derslerle çakışmamalı (`StudentConflictAsync` exclude=zincir) + past-time + availability (single ile tutarlı **HARD gate**). Herhangi biri başarısızsa → **409** (atomik; hiçbir şey kaymaz).
  4. **Uygula:** tüm üyeleri tek transaction'da kaydır (`StartAt/EndAt/RescheduledAt/RescheduleReason`). **Telafi YOK** (KeepBookings; ders yapılacak). Sonuç: kaydırılan seans listesi (id, eski/yeni saat).
- **Önizleme (öneri):** `PreviewChainShiftQuery(SessionId, NewStartAt)` → zincir + çakışma raporu (UI "şu N ders kayacak" gösterir). *(Açık soru #2.)*

### 4) Ayrımı koru + test et (kullanıcı kuralı)
Ertelendi/taşındı → **Reschedule/ChainShift → telafi YOK** · Başka hoca verdi → **ActualInstructor → telafi YOK** ·
Gerçekten işlenmedi → **NotDelivered → telafi VAR** · Closure/tatil → **closure telafi VAR**. Dördü de test edilir.

### 5) Yetkiler
- **Tek ders taşıma (single reschedule):** mevcut davranış — Owner/StudioAdmin tümü; Instructor **kendi** dersi (RequireManageableInstructor). Korunur.
- **Zincirleme kaydırma:** **yalnız Owner/StudioAdmin** (öneri — zincir birden çok seansı etkiler; daha güvenli). *(Açık soru #3.)*
- **Student:** yalnız programı görür; taşıma yok.

---

## Tasarlanan ama BU SPRINTTE UYGULANMAYAN (gerekçeli bölme)

### A) Gerçek drag/drop + zaman-grid takvim → **Sprint 21b/22**
**Gerekçe:** mevcut schedule **kart-listesi** (zaman-grid yok) → drag/drop için **yeni haftalık/günlük grid komponenti** + bir **dnd kütüphanesi** (@dnd-kit öneri, React 19 uyumlu) gerekir; Next 16 "breaking changes" (AGENTS.md) → kütüphane Next 16'da test edilmeli. Bu, tek-ders-taşıma + zincir-kaydırma çekirdeğinden bağımsız ve risklidir. **Çekirdek mantık (komutlar/çakışma/zincir) bu sprintte tamam olur; grid+dnd 21b'de aynı komutları çağırır** (backend yeniden kullanılır).

### B) Köklü grup programı değişikliği → **Sprint 22** (tasarım burada)
"Bu grubun bundan sonraki tüm dersleri Salı/Cuma olsun" — **körlemesine tüm gruba uygulanmaz**. Tasarım:
- Owner bir grubu (ortak seans dizisi) + **öğrencileri tek tek** seçer.
- Her seçili öğrenci için: o sözleşmenin **gelecekteki Booked Regular** derslerini iptal et (telafi YOK — taşıma niyeti) → kalan ders hakkı + sözleşme tarih aralığıyla **yeni slot deseninden** yeniden üret (`GenerateContractSchedule` yeniden kullanılır). Her öğrencinin tarih aralığı/kalan hakkı **farklı** olabilir → öğrenci-bazlı.
- **Neden Sprint 22:** çok-öğrencili toplu iptal+yeniden-üretim, kısmi başarısızlık/rollback, önizleme/onay; ayrıca "gelecek Booked iptali telafi üretmemeli" için cancel yolunun telafisiz varyantı netleştirilmeli. Büyük + dikkatli test ister.

### C) Closure full-undo → **Sprint 22+ (migration gerekir)**
**Analiz sonucu:** `StudioClosure` yalnız **sayaç** tutuyor; `ClassSession`/`ClassBooking`/`MakeupCreditTransaction` üzerinde **ClosureId YOK**, rescheduled seansların **orijinal saatleri saklanmıyor**. Güvenli otomatik undo (iptal seansları geri yükle + ertelenenleri eski saate al + kredileri topluca revoke) **mevcut şemayla yapılamaz** → **migration şart** (ClosureId bağları + orijinal-saat audit). Migration hedefimiz olmadığından **bu sprintte YOK**. **Karar:** manuel revoke + dokümante (Sprint 20'deki gibi) korunur; full-undo Sprint 22+'da migration ile. *(Açık soru #4.)*

---

## Migration
**Hedef: YOK** (uygulanan kapsam için). Tek-ders öğrenci-çakışması + chain-shift mevcut alanlarla çözülür
(`StartAt/EndAt/RescheduledAt/RescheduleReason`, `Status`, `HasConflictAsync`). **Köklü grup değişikliği ve closure
full-undo migration gerektirdiği için zaten bu sprint dışı.** Şema gerektiren başka bir ihtiyaç çıkarsa **DUR ve
raporla** (18b/19/20 kuralı).

## Kapsam dışı (sonraki sprintler)
Gerçek drag/drop + grid takvim (21b/22) · köklü grup programı değişikliği (22) · closure full-undo (22+, migration) ·
sözleşme uzatma/borç · package/rate DB drop · öğrenci self-booking · telafi expiry zorlaması.

## Riskler
- **Chain-shift algoritması + batch-aware çakışma** dikkatli test ister (zincir doğru kesilmeli; zincir dışı
  instructor/öğrenci çakışması doğru yakalanmalı; atomik abort). Closure deseni referans olduğundan risk sınırlı.
- **Availability HARD gate** chain-shift'te bir üyeyi engelleyip tüm zinciri abort edebilir → kullanıcıya net hata.
  *(Açık soru #5: chain-shift'te availability advisory mi olsun?)*
- **Öğrenci-çakışması yardımcısı** BookingCreator ile ortaklaştırılırken mevcut booking davranışı bozulmamalı (aynı sorgu, exclude eklenir).
- **Drag/drop ertelendi** — kullanıcı beklentisi netleşmeli (bu sprint modal; grid+dnd sonraki).

## Açık sorular (onay bekleyen)
1. **Drag/drop:** bu sprint **modal-tabanlı taşıma** (öneri) → gerçek drag/drop + grid takvim **21b/22**? Yoksa drag/drop'u bu sprinte mi zorlayalım (yüksek risk)?
2. **Chain-shift önizleme:** `PreviewChainShift` query'si (UI "N ders kayacak" gösterir) **eklensin** (öneri) mi, yoksa doğrudan apply + sonuç mu?
3. **Chain-shift yetkisi:** yalnız **Owner/StudioAdmin** (öneri) mi, Instructor kendi (aynı hoca) zincirini kaydırabilsin mi?
4. **Closure undo:** **Sprint 22+'a ertele** (migration gerek — öneri) mi, yoksa migration'a razı olup bu sprintte mi?
5. **Chain-shift'te availability:** **HARD gate** (single ile tutarlı — öneri) mi, **advisory** (engellemez, uyarır) mı?
6. **Köklü grup değişikliği:** **Sprint 22** (öneri, tasarım burada) mı, yoksa bu sprintte mi?

---

## Test / doğrulama planı

### `sprint21-smoke.sh` (yeni)
- **Tek taşıma instructor çakışması → 409** (aynı hoca yeni saatte başka ders); **farklı hoca aynı saat → 200**.
- **Tek taşıma ÖĞRENCİ çakışması → 409** (taşınan dersin öğrencisinin yeni saatte başka dersi var) — *yeni gap testi*.
- **Taşıma telafi ÜRETMEZ** (KeepBookings=true → bakiye değişmez); **booking korunur** (öğrenci aynı, yeni saatte Booked).
- **EndAt** yeni StartAt + süreden üretiliyor; **geçmiş zamana taşıma → engel**.
- **Chain-shift A vs B:** A (single) çakışınca 409; B zincir → ardışık örtüşen dersler `delta` kadar kayar, **boşlukta durur**; zincir DIŞI çakışma → 409 (atomik abort, hiçbir şey kaymaz); telafi YOK.
- **Ayrım:** Reschedule/ChainShift telafi yok · NotDelivered +1 · ActualInstructor telafi yok · Closure +1 (regresyon).
- **Yetki:** Instructor chain-shift → 403 (Owner-only ise); Student taşıma uçları → erişemez/yok.

### E2E (Playwright)
- Reschedule modalında **mod A/B** görünür; tek taşıma sonrası ders yeni tarihte; öğrenci korunmuş.
- (varsa) chain-shift önizleme "N ders kayacak" gösterir.
- e2e-seed: chain-shift için aynı hocaya ardışık 2-3 ders eklenebilir (opsiyonel).

### Full compose validation (commit öncesi, gerçek)
config dev/prod exit 0 → up --build → healthy → backend/frontend 200 → same-origin → e2e-seed → **compose smoke
(22+1) yeşil** → **compose E2E yeşil** → down -v + ortam restore.

### Audit
git status · secret/yasak desen yok · **migration yok** (varsa DUR) · package/rate DROP yok · 9999 UI gizli ·
taşıma telafi üretmiyor · öğrenci çakışması taşımada yakalanıyor · chain-shift atomik + batch-aware · ayrım korunuyor.

---

## İş sırası (onaylanınca)
1. Backend: `SessionRules.StudentConflictAsync` (exclude'lu) + BookingCreator ortaklaştırma (davranış aynı).
2. Backend: Reschedule'a öğrenci-çakışması kontrolü (KeepBookings=true).
3. Backend: `ChainShiftSessionCommand` (+ opsiyonel `PreviewChainShift`) — zincir + batch-aware doğrulama + atomik apply, telafisiz.
4. Backend build 0/0 (migration yok) + has-pending doğrulama.
5. Frontend: RescheduleModal mod A/B + chain-shift çağrısı (+ önizleme); öğrenci-çakışması hata gösterimi.
6. Frontend tsc 0 + next build 0.
7. Test: sprint21-smoke + E2E + e2e-seed + tüm suite yeşil + full compose validation + audit.
8. Dürüst rapor. **Commit/push YOK** (ayrı onay).

---

## Sonuç — Uygulama & Doğrulama (UYGULANDI)

**Kararlar (uygulandı):** 1) Drag/drop bu sprint YOK (modal-tabanlı taşıma; grid+dnd 21b/22). 2) PreviewChainShift eklendi.
3) Chain-shift yalnız Owner/StudioAdmin. 4) Closure undo Sprint 22+ (migration gerek — dokümante). 5) **Conflict HARD
(hoca/öğrenci/geçmiş), availability ADVISORY** (taşımada engellemez; Create hâlâ hard). 6) Köklü grup değişikliği Sprint 22 (tasarım spec'te).

**Migration:** YOK — `has-pending-model-changes` → "No changes". Mevcut alanlarla (`StartAt/EndAt/RescheduledAt/
RescheduleReason`, `Status`, çakışma helper'ları) çözüldü.

**Build:** Backend 0/0 (compose image exit 0). Frontend tsc 0 + next build 0.

**sprint21-smoke:** **26/26** — tek taşıma öğrenci/hoca çakışması 409, farklı hoca 200, telafi yok, booking korundu,
EndAt, geçmiş engeli; chain-shift preview (gap-stop), apply kaskad, **atomik abort** (zincir-dışı öğrenci çakışması),
telafi yok; ayrım (reschedule yok / not-delivered var); yetki (Instructor 403).

**Smoke suite:** **23/23** (dev + compose). **E2E:** **52/52** (compose; 03 mod A/B + Önizle dâhil).

**Full compose validation:** config dev/prod exit 0 → up --build → healthy → backend/frontend 200 → same-origin
(login 401) → seed → compose smoke 23/23 → compose E2E 52/52 → down -v + restore.

**Audit:** migration yok · package/rate DROP yok · reschedule `EnsureMovableAsync` (availability advisory; Create
`EnsureValidAsync` hard — korundu) · chain-shift Owner/Admin gate · `StudentConflictAsync` ortak (BookingCreator
davranışı aynı) · 9999 UI gizli korundu.

**Commit/push:** Sprint 21 onaylandı → commit `feat: add reschedule and chain-shift scheduling controls (Sprint 21)`, tag `v0.10.0-pilot`.

### Teknik borç (Sprint 22 planında karar verilecek)
Aşağıdakiler bilinçli olarak ertelendi; Sprint 22 planında hangisine geçileceğine karar verilecek:
- **Gerçek drag/drop + zaman-grid takvim:** mevcut schedule kart-listesi; grid komponenti + dnd kütüphanesi (@dnd-kit,
  Next 16 testi) gerekir. Çekirdek komutlar (`reschedule`, `chain-shift`, `preview`) hazır → grid aynı backend'i çağıracak.
- **Köklü grup programı değişikliği:** öğrenci-bazlı gelecek-ders iptali (telafisiz) + `GenerateContractSchedule` ile
  yeniden üretim; çok-öğrencili rollback/preview. (Tasarım bu spec'te.)
- **Closure full-undo:** **migration şart** (`ClosureId` bağları: ClassSession/ClassBooking/MakeupCreditTransaction +
  rescheduled seansların orijinal-saat audit'i). Şu an manuel revoke + dokümante yaklaşımı korunuyor.

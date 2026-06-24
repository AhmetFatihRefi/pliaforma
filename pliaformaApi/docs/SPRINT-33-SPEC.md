# Sprint 33 — Sözleşmede Opsiyonel Hoca + Kapasite Tercihi

> **Durum:** Onaylı spec (kararlar alındı). Kod YOK · migration YOK · commit/push/tag YOK.
> Bu doküman onaylandıktan sonra implementasyon başlar (migration dahil, ayrı onayla commit).
> **Önceki:** Sprint 32A/B/C (ekipman envanteri + kapasite guard + boşluk bulma). `v1.23.0-pilot`.
> **Sonraki tag önerisi (33 yeşil olursa):** `v1.24.0-pilot`.

---

## 0. Amaç

Bir öğrenci, sözleşmedeki dersi **belirli bir hocadan** veya **herhangi bir hocadan** almak isteyebilir; ayrıca dersi **bireysel** ya da **en fazla N kişilik** almak isteyebilir. Bu iki tercih **opsiyoneldir**: belirtilmezse hiçbir kurala tabi değildir; **belirtilirse HARD kuraldır** (uymayan derse ekleme / üretim engellenir).

---

## 1. Model değişiklikleri (`StudentContractLine`) — migration gerektirir

| Alan | Değişiklik |
|---|---|
| `InstructorProfileId` | **`Guid?` (nullable)** yapılır. `null` = "herhangi bir hoca"; dolu = belirtilen hoca (HARD). FK opsiyonel olur (delete davranışı korunur). |
| **`PreferredMaxCapacity` `int?`** (YENİ) | `null` = Farketmez · `1` = Bireysel · `2` = en fazla 2 · `N` = en fazla N. **`>= 1` zorunlu** (0/negatif **reddedilir**, 400). |
| `IsIndividual` (mevcut) | **Korunur** ama **türetilir/senkron**: kaynak alan artık `PreferredMaxCapacity`. |

### IsIndividual ↔ PreferredMaxCapacity senkron kuralı
- `PreferredMaxCapacity == 1` → `IsIndividual = true`
- `PreferredMaxCapacity == null` **veya** `> 1` → `IsIndividual = false`
- Create/update akışında ikisi **çelişemez**; `IsIndividual` her zaman `PreferredMaxCapacity`'den türetilerek yazılır.

### Migration backfill
- Eski `IsIndividual = true` → `PreferredMaxCapacity = 1`
- Eski `IsIndividual = false` → `PreferredMaxCapacity = NULL`
- `InstructorProfileId` mevcut değerler korunur (hepsi dolu kalır; yeni satırlarda null olabilir).
- Migration adı (öneri): `Sprint33_LineInstructorOptionalAndCapacityPref`. **Additive** (kolon ekleme + nullable yapma); veri kaybı yok.

---

## 2. InstructorProfileId — opsiyonel hoca davranışı

### Aday eşleştirme / booking (öğrenciyi derse ekleme)
- `line.InstructorProfileId == null` → seans hocası için **filtre yok** (her hocanın o ders tipindeki seansı uygun).
- `line.InstructorProfileId` dolu → yalnız **aynı hoca** kabul; **farklı hocaya ekleme → 409**.

### Program oluşturma
- `line.InstructorProfileId` dolu → o hoca kullanılır.
- `line.InstructorProfileId == null` → **program oluşturma ekranında hoca seçimi ZORUNLU**.
  - Seçilen hoca **yalnız o üretim işlemi** için kullanılır; **line'a kalıcı yazılmaz** (line null kalır).
  - **Reuse**'da da seçilen hoca filtresi geçerli: kullanıcı A hocasını seçtiyse **B hocasının mevcut grubuna otomatik bağlamaz**.

> **Effective instructor:** `effectiveInstructor = line.InstructorProfileId ?? generationInstructorId`. İkisi de null ise üretim **reddedilir** ("Bu sözleşmenin hocası belirtilmemiş; program için bir hoca seçin.").

---

## 3. PreferredMaxCapacity — kapasite tercihi (HARD)

### Aday eşleştirme / booking
- `PreferredMaxCapacity == null` → kapasite filtresi yok.
- `PreferredMaxCapacity` dolu → yalnız **`session.Capacity <= PreferredMaxCapacity`** olan derslere aday; **aşan derse ekleme → 409**.

**Örnek (`PreferredMaxCapacity = 2`):** Capacity 1 ✅ · 2 ✅ · 3 ❌ · 9999 ❌.

### `BookingCreator` guard değişimi
- Mevcut: `if (line.IsIndividual && !session.IsIndividual) throw …`
- Yeni: `if (line.PreferredMaxCapacity is { } cap && session.Capacity > cap) throw new ConflictException("Bu sözleşmenin kapasite tercihi en fazla {cap} kişi; daha büyük bir derse eklenemez.")`
  - `PreferredMaxCapacity == 1` (bireysel) bu kuralın özel hâli.

---

## 4. Program oluşturma — kapasite kuralı (KRİTİK)

Plandaki "Capacity = PreferredMaxCapacity ?? 9999" **yanlıştır**; doğru kural:

| Durum | Üretilen seans kapasitesi |
|---|---|
| **A.** `PreferredMaxCapacity` dolu | `Capacity = PreferredMaxCapacity` |
| **B.** `PreferredMaxCapacity == null` **ve** `ClassType.EquipmentId == null` | Eski davranış: grup-sınırsız `9999` üretilebilir |
| **C.** `PreferredMaxCapacity == null` **ve** `ClassType.EquipmentId != null` | `9999` **YASAK** (32B invariant). Üretim ekranında **"Seans kapasitesi" seçtirilir** (sonlu, `EquipmentCapacityGuard`'dan geçer). Seçilmeden → **reddedilir**. |

> **Effective capacity:** `PreferredMaxCapacity ?? (equipmentBound ? generationCapacity : 9999)`. `equipmentBound && generationCapacity == null` → reddet.

### 32B etkileşimi (refinement)
Sprint 32B'deki `EquipmentCapacityRules.EnsureAutoGenAllowed(classType, individual)` kuralı **güncellenir**:
- Eski: "ekipman-bağlı + GRUP (individual=false) → 409".
- Yeni: "ekipman-bağlı + **effective capacity sınırsız (9999)** → 409"; **sonlu** effective capacity (PreferredMaxCapacity'den veya generation-time'dan) → **izinli** + `EquipmentCapacityGuard.EnsureCanReserveAsync` ile her seans doğrulanır.
- `RunAsync` içindeki per-seans guard çağrısı `individual` koşulundan çıkıp **effective capacity sonlu olan tüm ekipman-bağlı seanslar** için çalışır.
- **Invariant korunur:** `EquipmentId bağlı ClassType + Scheduled session + Capacity=9999` asla oluşmaz.

---

## 5. Grup reuse kuralı

Mevcut gruba bağlama (reuse):
- `PreferredMaxCapacity == null` → kapasite tercihi yok.
- `PreferredMaxCapacity` dolu → yalnız **`session.Capacity <= PreferredMaxCapacity`** olan mevcut gruplar.
- `line.InstructorProfileId` dolu → yalnız o hoca.
- `line.InstructorProfileId == null` ama generate'te hoca seçildiyse → **seçilen hoca** filtresi (başka hocanın grubuna bağlamaz).

---

## 6. Frontend

### `AddContractLineModal`
- **Hoca alanı opsiyonel** ("Hoca (opsiyonel)"); seçilmezse açıklama: *"Hoca seçilmezse program oluştururken seçilir."*
- "Bireysel mi?" checkbox → **"Kapasite tercihi"** alanı:
  - **Farketmez** (null)
  - **Bireysel** (1)
  - **En fazla 2** (2)
  - **En fazla 3** (3)
  - **En fazla 4** (4)
  - **Diğer (sayı gir)** → number input (≥1) — custom değerler için.

### `ContractDetailModal`
- Hoca boşsa → **"Herhangi bir hoca"**.
- Kapasite tercihi: null → **"Farketmez"** · `1` → **"Bireysel"** · `>1` → **"En fazla X kişilik ders"**.

### Programı oluştur (line "Yönet" modalındaki üretim akışı)
- Line hoca `null` ise → **hoca seçimi zorunlu** (seçilen yalnız bu üretim için).
- Line `PreferredMaxCapacity == null` **ve** ders tipi ekipman-bağlı ise → **"Seans kapasitesi" seçimi zorunlu** (sonlu); aksi halde üretim butonu engellenir / 409.
- Seçilen kapasite `EquipmentCapacityGuard`'dan geçer (yetersizse 409, kullanıcı-dostu mesaj).

---

## 7. Sonradan düzenleme — KAPSAM DIŞI

Bu sprintte line **oluşturulduktan sonra** `InstructorProfileId` / `PreferredMaxCapacity` **düzenlenmez**.
Sebep: üretilmiş seanslar/booking'ler varken değiştirmek karmaşık (tutarlılık riskleri). Ayrı sprint.

---

## 8. Etkilenen alanlar

**Backend:**
- Migration (`Sprint33_LineInstructorOptionalAndCapacityPref`).
- `StudentContractLine` entity + EF config (nullable FK + yeni kolon).
- `StudentContractLineDto` (+ `preferredMaxCapacity`, `instructorProfileId?`/`instructorName?` nullable).
- `CreateStudentContractLine` (command/validator/handler): hoca opsiyonel; `PreferredMaxCapacity` opsiyonel (≥1); `IsIndividual` türetilir.
- Aday eşleştirme: `BookingCandidates` (`GetRegularBookingCandidatesQuery`), `ContractLineScheduling` (`ContractLineEligibilityChecker` / `ContractLineResolver`).
- `BookingCreator` guard (IsIndividual → PreferredMaxCapacity).
- `ContractLineScheduleEngine.RunAsync` + `GroupScheduleChange.ExecuteAsync` (effective instructor + effective capacity + reuse filtreleri + 32B guard refinement).
- `EquipmentCapacityRules.EnsureAutoGenAllowed` (sınırsız-yasak kuralı).
- Generate request DTO'ları: `GenerateLineScheduleCommand` (+ opsiyonel `InstructorProfileId`, `Capacity`); gerekirse `GenerateContractScheduleCommand`.

**Frontend:**
- `students/_components.tsx`: `AddContractLineModal`, `ContractDetailModal`, üretim akışı.
- `lib/domain-types.ts` (+ `preferredMaxCapacity`, nullable instructor); `lib/studioApi.ts` (generate çağrısına opsiyonel instructor/capacity).

---

## 9. Test planı

### A. Migration / backfill
- `IsIndividual=true` → `PreferredMaxCapacity=1`; `IsIndividual=false` → `PreferredMaxCapacity=null`.
- `InstructorProfileId` nullable çalışır (null line oluşturulabilir).
- `has-pending-model-changes` clean.

### B. Candidate / booking
- line hoca **null** → farklı hocalı dersler aday olabilir.
- line hoca **dolu** → yalnız aynı hoca; farklı hoca → **409**.
- `PreferredMaxCapacity=2` → Capacity 1/2 kabul, 3/9999 **red (409)**.
- `PreferredMaxCapacity=null` → kapasite filtresi yok.

### C. Program oluşturma
- hoca **null** → generate'te hoca seçimi zorunlu (seçilmeden 400/uyarı); seçilince o hocayla üretir, line null kalır.
- hoca **dolu** → otomatik o hoca.
- `PreferredMaxCapacity=1` → seans `Capacity=1` + `IsIndividual=true`.
- `PreferredMaxCapacity=2` → seans `Capacity=2`.
- `PreferredMaxCapacity=null` + **ekipman yok** → eski `9999` davranışı korunur.
- `PreferredMaxCapacity=null` + **ekipman var** → generate kapasite seçilmeden **red**.
- generate kapasitesi seçilince **32B guard** çalışır (yetersizse 409).

### D. Group reuse
- tercih varsa yalnız `session.Capacity <= tercih` gruplar reuse edilir; aşan gruplar kullanılmaz.
- generate-time hoca seçildiyse reuse **aynı hocadan** (başka hocanın grubuna bağlamaz).

### E. Regresyon
- backend build **0/0** · `tsc` **0** · `next build` **0**.
- migration apply + `has-pending` **clean**.
- **sprint32a / 32b / 32c** smoke PASS.
- booking / makeup / contract-schedule (28c1, 22, 18a, 27, 29c) smoke PASS.
- Yeni `sprint33-smoke.sh` (B/C/D senaryoları).

---

## 10. Açık sorular (öneriyle)

1. **Custom kapasite üst sınırı:** "Diğer (sayı gir)" için makul bir tavan? *Öneri:* `1..50` (grup pratiği). Spec'e yazılı; UI'da min 1.
2. **GenerateContractScheduleCommand (header-bazlı):** line-bazlı generate ana akış; header-bazlı generate'i de aynı kurallarla mı güncelleyelim yoksa yalnız line-bazlı mı? *Öneri:* **line-bazlı** (`GenerateLineSchedule`) güncellenir; header-bazlı akış zaten geçiş/legacy — gerekirse aynı effective-capacity/instructor mantığı uygulanır, ek karar gerekmez.

---

## 11. Süreç

1. **Bu spec** (`SPRINT-33-SPEC.md`) hazır → **onay bekleniyor**.
2. Onaydan sonra: migration + backend + frontend implementasyonu, faz faz; her şey yeşil bırakılır.
3. Validate: build 0/0 · tsc/next 0 · migration apply + has-pending clean · sprint33 smoke + 32a/b/c + booking/makeup/contract regresyon.
4. **Commit/push/tag yalnız ayrı onayla** (öneri: `v1.24.0-pilot`).

> Şu an kod/migration/commit **YOK**. Onayını bekliyorum.

# Sprint 14 — Paket Planı V2: Ders Grubu Kapsamı ve Fiyat Geçerlilikleri

> **Durum:** ✅ UYGULANDI (REV2 onaylandı). Tüm kod/migration/test yazıldı ve doğrulandı.
> sprint14-smoke 37✓ · 15/15 smoke · 31/31 E2E (2x) · same-origin · backend 0/0. Detay: [sprints.md](sprints.md#sprint-14--tamamlanan-kapsam).

---

## 0. Context

Pliaforma pilates SaaS'ında paket planları bugün yalnızca **kredi sayısı + tek fiyat** biliyor; hangi
ders gruplarında (ClassType) geçerli olduğunu bilmiyor ve fiyat değişince geçmişe dönük takip yok.

Sprint 14:
- Paket planlarına **ders grubu (ClassType) kapsamı** ekler.
- Paket fiyatlarını **tarih-bazlı versiyonlar** (mevcut öğrenci eski fiyatla devam; yeni alan/yenileyen geçerli fiyattan).
- Öğrenci self-booking'de **paket uygunluğu + kullanılabilir hak** kontrol eder.

**Bu sprintte gerçek online ödeme YOK** — yalnızca "hangi paket hangi ders için, hangi fiyattan ve kaç
hak ile geçerli" bilgisi sisteme girer.

### REV2 düzeltmeleri
1. **Booking artık "sadece kontrol" değil** — `ExpectedStudentPackageId` + **kullanılabilir hak (available credit)** mantığı eklendi; hak sayısından fazla rezervasyon engellenir.
2. **Satış fiyatı daima `PackagePlanPriceVersion`'dan hesaplanan effective price**'tan alınır; `PackagePlan.Price` source-of-truth değil, yalnız legacy/display cache.

---

## 1. Mevcut Durum (kanıtlanmış kod gerçekleri — referans)

| Alan | Gerçek |
|---|---|
| `PackagePlan` | `Id, StudioId, Name, Description?, CreditCount, Price(numeric12,2), ValidityDays?, IsActive` + audit. CRUD: `Features/Packages/PackagePlanFeature.cs`, controller `api/studio/package-plans` (Owner,StudioAdmin). |
| `StudentPackage` | `Id, StudioId, StudentProfileId, PackagePlanId?(SetNull), Name, InitialCreditCount, CurrentCreditBalance, Price, ValidFrom(DateOnly), ValidUntil(DateOnly?), Status(Active/UsedUp/Expired/Cancelled), CreatedByUserId`. Satış: `SellStudentPackageCommand` (fiyatı Price'a snapshot'lıyor). Ledger: `StudentPackageTransaction`. |
| `ClassType` | `Id, StudioId, Name, Equipment, CapacityType, MaxCapacity, Level, Purpose, DefaultDurationMinutes, IsActive`. |
| `ClassSession` | `ClassTypeId` **var** (FK Restrict). `StartAt` studio-local. |
| `ClassBooking` | `Id, StudioId, ClassSessionId, StudentProfileId, Status(Booked/CancelledByStudent/CancelledByStudio), BookedByUserId, Cancelled*`. Unique filtreli index `(ClassSessionId,StudentProfileId)` Booked+IsDeleted=false. |
| Self-booking | `StudentBookCommand(ClassSessionId)` → `BookingCreator`. **Bugün paket uygunluk/hak kontrolü YOK.** Mevcut 409: zaten rezervasyon var / seans dolu / telafi kredisi yok. |
| Staff booking | `StaffCreateBookingCommand(ClassSessionId, StudentProfileId)`. Instructor yalnız kendi seansı + kendi oluşturduğu öğrenci. |
| Yoklama tüketimi | `MarkBookingAttendanceCommand(... ConsumePackageCredit, StudentPackageId?)` → `StudentPackageConsumer.ConsumeForAttendanceAsync`. **Kredi düşümü burada** (rezervasyonda değil). Idempotent. Auto-seçim: Active + balance>0 + ValidUntil>=bugün, en erken bitenden. |
| Pipeline | `... → StudioStatus → SubscriptionLicense → MembershipGuard → Transaction → Handler`. Yazma komutları `IStudioWriteRequest`. |
| Altyapı | DbContext `Persistence/Contexts/AppDbContext.cs`; arayüz `Application/Common/Interfaces/IAppDbContext.cs`. Son migration: `20260617195305_Sprint13_Subscriptions`. |

---

## 2. Locked Decisions (kullanıcı onayı · REV2)

1. **Kapsam yokluğu anlamı:** `PackagePlan.AllowAllClassTypes` bool. `true` → tüm ClassType. `false` → yalnız `PackagePlanClassType` kümesi. Yeni planlar default `true`; kimse yanlışlıkla kilitlenmez.
2. **Booking-time davranışı:** Kredi **yine yoklamada düşülür** AMA booking sırasında **kullanılabilir hak** hesaplanır ve booking uygun pakete **ExpectedStudentPackageId** ile bağlanır.
   **Kullanılabilir hak = `CurrentCreditBalance − aynı ExpectedStudentPackageId'ye bağlı, iptal edilmemiş VE henüz paket tüketimiyle kapanmamış booking sayısı`** (gelecek Booked + geçmiş ama yoklaması/consume yapılmamış Booked dahil; consume olmuş booking ayrıca sayılmaz çünkü zaten balance düşmüştür; Cancelled sayılmaz). Hak ≤ 0 ise self-booking **409**. Yoklaması unutulmuş geçmiş booking'ler yapay yeni hak kazandırmaz. Kredi düşmediği için iptalde ayrı iade gerekmez (booking iptal → o paketin available hakkı tekrar artar).
3. **Fiyat kaynağı:** Satış fiyatı **daima `PackagePlanPriceVersion`'dan hesaplanan effective price** (`EffectiveFrom <= today` en güncel version). `StudentPackage.Price` bu computed price'tan snapshot'lanır. `PackagePlan.Price` **source-of-truth DEĞİL** — yalnız legacy/display cache; stale olsa bile satış etkilenmez. Gelecek-tarihli zam, tarihi gelince **job olmadan** otomatik doğru fiyat verir (query hesaplar).
4. **Snapshot:** Satışta hem fiyat (computed effective) hem **ders grubu kapsamı** snapshot'lanır; plan sonradan değişse de mevcut paket etkilenmez.
5. **Staff paketsiz/yetersiz booking:** Owner/StudioAdmin paketsiz veya **kullanılabilir hakkı yetersiz** öğrenciyi yazabilir → `ClassBooking.RequiresPayment=true`. **Instructor paket/hak kontrolünü bypass EDEMEZ** (uygun değilse 409). Self-booking uygun değil/hak yok → 409.

---

## 3. Kapsam (uygulama maddeleri)

### 3.1 PackagePlan ClassType Kapsamı

**Yeni entity:** `PackagePlanClassType`
- Alanlar: `Id, PackagePlanId, ClassTypeId, IsActive, CreatedAt, IsDeleted` (+ `StudioId` denormalize).
- FK: Restrict ClassType / Cascade PackagePlan.
- Config: unique filtreli `(PackagePlanId, ClassTypeId)` `IsDeleted=false`.

**PackagePlan'a eklenen:** `AllowAllClassTypes (bool, default true)`.

**Kural:** `true` → kapsam tüm aktif ClassType. `false` → kapsam `PackagePlanClassType` (IsActive, IsDeleted=false). `false` + hiç ClassType yoksa: plan hiçbir derste geçerli değil (UI uyarısı: "Bu plan hiçbir ders grubu için seçilmemiş, kullanılamaz").

### 3.2 StudentPackage Fiyat + Kapsam Snapshot

**Yeni entity:** `StudentPackageAllowedClassType`
- Alanlar: `Id, StudentPackageId, ClassTypeId` (+ `StudioId` denormalize, `IsDeleted, CreatedAt`).
- FK: Cascade StudentPackage / Restrict ClassType.

**StudentPackage'a eklenen:** `AllowAllClassTypesSnapshot (bool)`.

**Kural (satışta — `SellStudentPackageCommand`):**
- **Fiyat:** plandan satış günündeki **computed effective price** (§3.5) hesaplanır, `StudentPackage.Price`'a snapshot'lanır. (Plan custom/plansız satışta verilen fiyat aynen kullanılır.)
- **Kapsam:** plan `AllowAllClassTypes=true` ise `AllowAllClassTypesSnapshot=true` (allowed liste boş). `false` ise o anki `PackagePlanClassType` kümesi `StudentPackageAllowedClassType`'a kopyalanır.
- Plan sonradan değişse bile **mevcut StudentPackage etkilenmez**.

### 3.3 Ders Rezervasyonunda Paket Uygunluğu + Kullanılabilir Hak

**ClassBooking'a eklenen alanlar:**
- `ExpectedStudentPackageId (Guid?)` — booking'in kullanmayı planladığı paket.
- `RequiresPayment (bool, default false)` — paketsiz/yetersiz hak ile staff rezervasyonu.
- `PackageEligibilityStatus (string/enum?, nullable)` — booking anındaki değerlendirme (Eligible / RequiresPayment / NoBalance / WrongClassType / NoPackageAtAll / Resolved). Görünürlük için saklanır.

**Yeni yardımcı:** `PackageEligibilityChecker.EvaluateAsync(studioId, studentProfileId, classTypeId, today)` → `(EligibilityStatus, ExpectedStudentPackageId?)`. Mantık:
1. Öğrencinin Active + (ValidUntil null || >=today) + ClassType uygun (AllowAllClassTypesSnapshot || allowed listede classTypeId) paketleri bulunur.
2. Her aday paket için **kullanılabilir hak** = `CurrentCreditBalance − (o paketle ExpectedStudentPackageId eşleşen, iptal edilmemiş [Status=Booked] VE henüz paket tüketimiyle kapanmamış [bu booking'in attendance'ında consume gerçekleşmemiş] booking sayısı)`. **Geçmiş/gelecek ayrımı yok** — kapanmamış her Booked booking hak rezerve eder; consume olmuş booking (zaten balance düşmüş) tekrar sayılmaz; Cancelled sayılmaz.
3. Kullanılabilir hak > 0 olan adaylardan en erken biten seçilir → `Eligible` + o paketin Id'si `ExpectedStudentPackageId`.
4. ClassType uygun paket var ama hepsinin kullanılabilir hakkı ≤ 0 → `NoBalance`.
5. Sadece başka ClassType paketi var → `WrongClassType`.
6. Hiç aktif/geçerli paket yok → `NoPackageAtAll`.

**Self-booking (`StudentBookCommand` / `BookingCreator`):** kapasite reserve'den **ÖNCE** eligibility. `Eligible` değilse **409**:
- `NoPackageAtAll` → "Bu ders için geçerli paketiniz veya kalan hakkınız yok."
- `NoBalance` → "Paket hakkınız bitmiş. Yeni paket satın almanız gerekiyor."
- `WrongClassType` → "Mevcut paketiniz bu ders grubu için geçerli değil."
`Eligible` ise booking yaratılır + `ExpectedStudentPackageId` yazılır + `PackageEligibilityStatus=Eligible`.

**Staff booking (`StaffCreateBookingCommand`):**
- Owner/StudioAdmin: `Eligible` ise `ExpectedStudentPackageId` yazılır. Değilse **booking yine yaratılır** ama `RequiresPayment=true` + `PackageEligibilityStatus` = ilgili sebep, `ExpectedStudentPackageId=null`.
- Instructor: `Eligible` değilse **409** (öğrenci ile aynı mesajlar) — bypass yok.

**İptal davranışı:** Booking iptal edilince (Status=Cancelled*) kredi düşmediği için **iade yok**; o paketin "kullanılabilir hak" hesabı iptal edilen booking'i artık saymaz → hak otomatik geri gelir. `ExpectedStudentPackageId` booking üzerinde kalır (tarihsel iz).

**Eşzamanlılık:** Hak hesabı + booking yaratma **aynı transaction** içinde, ilgili StudentPackage satırı **row-lock** ile (mevcut makeup-credit row-lock deseni) yapılır — iki eşzamanlı self-booking yarışını engeller.

### 3.4 Attendance Package Consume Güncellemesi (ExpectedStudentPackageId bağlantısı)

`StudentPackageConsumer.ConsumeForAttendanceAsync` güncellenir; `ClassSession.ClassTypeId` + booking'in `ExpectedStudentPackageId`'si dikkate alınır:
- **Öncelik:** Komutta açık `StudentPackageId` verilmediyse ve booking'de `ExpectedStudentPackageId` varsa **önce o paket** kullanılır.
- Seçilen/expected paket bu ClassType için geçerli değilse → **409** "Seçili paket bu ders grubu için geçerli değil."
- Expected paket artık geçersiz (Cancelled/Expired/ValidUntil geçmiş) veya balance=0 ise → **409** "Beklenen paket artık geçerli değil veya hakkı kalmamış; lütfen geçerli bir paket seçin." Staff açık `StudentPackageId` vererek override edebilir (o da ClassType+balance kontrolünden geçer). **Auto-fallback YAPILMAZ** — sessizce başka paketten düşmek yanlış paketi tüketebilir; staff bilinçli seçsin.
- Açık `StudentPackageId` verildiyse o kullanılır (yine ClassType + balance kontrolü).
- Expected/explicit yoksa auto-seçim yalnız ClassType kapsamına uyan + balance>0 + ValidUntil geçerli paketlerden (en erken biten).
- Aynı attendance için **ikinci düşüm yine engelli** (mevcut idempotency korunur).
- **RequiresPayment temizleme:** Booking `RequiresPayment=true` iken consume başarılı olursa → `RequiresPayment=false` yapılır, `ExpectedStudentPackageId` = fiilen kullanılan paket olarak set edilir, `PackageEligibilityStatus = "Resolved"`. Böylece öğrenci detayında "Ödeme gerekli" uyarısı çözülen booking'lerde gereksiz kalmaz.

### 3.5 PackagePlan Fiyat Versiyonlama (version = source of truth)

**Yeni entity:** `PackagePlanPriceVersion`
- Alanlar: `Id, PackagePlanId, Price(numeric12,2), Currency, EffectiveFrom(DateOnly), CreatedByUserId, CreatedAt, IsDeleted` (+ `StudioId` denormalize).
- Index: `(PackagePlanId, EffectiveFrom)`.

**Kurallar:**
- **Effective price = `EffectiveFrom <= today` olan en güncel (max EffectiveFrom) version.Price** — her okuma/satışta query ile hesaplanır.
- **Satış daima bu computed effective price'ı kullanır.** `PackagePlan.Price` stale olsa bile satış etkilenmez (source of truth = version tablosu).
- `PackagePlan.Price` yalnız legacy/display cache; best-effort senkronlanabilir ama hiçbir karar buna dayanmaz.
- Gelecek-tarihli version, tarihi gelince **job olmadan** otomatik effective olur (query `today`'e bakar).
- `StudentPackage.Price` snapshot **değişmez** (mevcut öğrenci eski fiyatıyla kalır).

**DTO alanları (plan listele/detay):** `currentEffectivePrice`, `currentEffectivePriceCurrency`, `nextEffectivePrice?`, `nextEffectiveFrom?` (gelecek-tarihli en yakın version varsa).

**Endpoint (Owner/StudioAdmin):** `GET/POST api/studio/package-plans/{id}/price-versions`. Plan create/update'te de "mevcut fiyat" + opsiyonel "gelecek fiyat + EffectiveFrom".

### 3.6 PackagePlan UI (Studio — Owner/StudioAdmin)

Plan oluştur/düzenle: ClassType multi-select + `AllowAllClassTypes` toggle; mevcut fiyat + (opsiyonel) gelecek fiyat & EffectiveFrom; CreditCount/ValidityDays. Satışta: `currentEffectivePrice` gösterilir; `nextEffectivePrice/nextEffectiveFrom` varsa "Yeni fiyat {tarih} tarihinde geçerli olacak" bilgisi; paketin geçerli ders grupları gösterilir.

### 3.7 Student UI

Öğrenci rezervasyon ekranı: uygun dersler normal; uygun olmayanlar **disabled** + neden:
- "Paketiniz bu ders için geçerli değil" (WrongClassType)
- "Paket hakkınız bitmiş" (NoBalance)
- "Yeni paket almanız gerekiyor" (NoPackageAtAll)

Uygunsa "Bu derste kullanılacak paket: {ad}". `GetStudentSessionsQuery` DTO'suna ders-başına eligibility (status + neden + beklenen paket adı + kalan kullanılabilir hak) eklenir.

### 3.8 Payment/Package İlişkisi (gerçek ödeme YOK)

Paket/hak bitince self-booking devam edemez. Owner/StudioAdmin yazarsa `RequiresPayment=true` → dashboard/students ve öğrenci detayında "Ödeme gerekli / paketsiz rezervasyon" raporlanır. Bu sprintte yalnız işaretleme + raporlama; tahsilat ileride.

---

## 4. Migration & Backfill (`Sprint14_PackagePlanV2`)

- **Yeni tablolar:** `PackagePlanClassTypes`, `StudentPackageAllowedClassTypes`, `PackagePlanPriceVersions`.
- **Yeni kolonlar:** `PackagePlans.AllowAllClassTypes`, `StudentPackages.AllowAllClassTypesSnapshot`, `ClassBookings.{ExpectedStudentPackageId, RequiresPayment, PackageEligibilityStatus}`.
- **Backfill (raw SQL, kimseyi kilitlemez):**
  - `PackagePlans.AllowAllClassTypes = true`; `StudentPackages.AllowAllClassTypesSnapshot = true`; `ClassBookings.RequiresPayment = false`, `ExpectedStudentPackageId = NULL`, `PackageEligibilityStatus = NULL`.
  - Her mevcut `PackagePlan` için initial `PackagePlanPriceVersion` (`Price = plan.Price`, `EffectiveFrom = CURRENT_DATE`, `Currency = stüdyo currency || 'TRY'`).
- **Doğrulama:** backfill sonrası 0 plan/paket kullanılamaz; mevcut self-booking 409 üretmez; mevcut booking'ler `ExpectedStudentPackageId=NULL` ile çalışmaya devam eder.

---

## 5. Testler (sprint14-smoke + E2E)

### Kapsam / snapshot / fiyat
- PackagePlan ClassType scope oluşturulur (AllowAll=false + N ClassType).
- StudentPackage satışında scope snapshot'lanır; plan scope sonra değişince eski paket etkilenmez.
- Self-booking uygun ClassType paketi varsa 200; hiç paket yok → 409 (NoPackageAtAll); yanlış ClassType → 409 (WrongClassType).
- Attendance consume yanlış ClassType paketinden düşmez (409); auto-seçim doğru ClassType paketini seçer.
- Bugün 2000 effective version varken satış 2000 snapshot alır.
- Gelecek-tarihli 2500 version eklenince bugün satış hâlâ 2000 alır.
- EffectiveFrom geçmişe/bugüne çekilince satış 2500 alır.
- PackagePlan.Price cache stale olsa bile satış computed effective price'ı kullanır.
- Mevcut StudentPackage fiyatı plan fiyat güncellemesinden etkilenmez; future price DTO'da görünür.

### Kullanılabilir hak (REV2)
- 1 hakkı olan öğrenci aynı paketten **1 gelecek derse** self-booking yapabilir (200, ExpectedStudentPackageId set).
- Aynı öğrenci 2. gelecek derse self-booking yapınca 409 (NoBalance).
- Booking iptal edilince hak tekrar available → öğrenci yeniden self-booking yapabilir (200).
- Owner/StudioAdmin 2. booking'i yapınca RequiresPayment=true (booking yaratılır).
- Instructor yetersiz hakla booking → 409.
- Attendance consume ExpectedStudentPackageId paketinden düşer.
- ExpectedStudentPackageId yanlış ClassType/geçersiz ise consume 409.
- **Yoklaması yapılmamış geçmiş Booked booking hak rezerve eder** (yapay yeni hak vermez): 1 hakla geçmiş kapanmamış booking varken yeni self-booking → 409.
- **RequiresPayment=true booking'de consume başarılı olunca RequiresPayment=false + PackageEligibilityStatus=Resolved.**

### Regresyon
Backfill sonrası eski paketler çalışır; **mevcut 14 smoke + security + 28 E2E bozulmaz** (özellikle Sprint 7 paket, Sprint 4 booking, Sprint 6 attendance).

---

## 6. Sprint 14 Rapor Başlıkları (bittiğinde verilecek)

1. Genel Özet
2. Oluşturulan/Değiştirilen Dosyalar
3. Yeni Entity & Migration (+backfill)
4. Paket Kapsam Modeli (AllowAllClassTypes + PackagePlanClassType)
5. StudentPackage Scope Snapshot
6. Fiyat Versiyonlama (version source-of-truth + effective/next price)
7. Booking Eligibility + Kullanılabilir Hak (ExpectedStudentPackageId, RequiresPayment)
8. Attendance Consume (expected package bağlantısı)
9. Frontend: PackagePlan UI
10. Frontend: Öğrenci Rezervasyon Deneyimi
11. Yetki & Tenant
12. Test/Doğrulama Sonuçları
13. Bilinçli Yapılmayanlar
14. Riskler/Borçlar
15. Sonraki Sprint Önerisi

---

## 7. Karar Gerekçeleri (REV2 raporu)

- **ExpectedStudentPackageId / available credit nasıl çalışır:** Booking yaratılırken `PackageEligibilityChecker` öğrencinin ClassType-uygun, geçerli paketleri içinden kullanılabilir hakkı > 0 olan en erken biten paketi seçer ve `ExpectedStudentPackageId`'ye yazar. Kredi hâlâ yoklamada düşer; açık/kapanmamış booking'ler "rezerve edilmiş hak" gibi sayıldığından fazla rezervasyon engellenir.
- **Available credit kapsamı (netleştirme):** Hak hesabı yalnız gelecek değil, **kapanmamış tüm Booked booking'leri** sayar — yoklaması/consume'u unutulmuş geçmiş booking'ler de hak rezerve eder; bu yüzden geçmiş ihmal yapay yeni hak doğurmaz. Consume olmuş booking balance'tan zaten düştüğü için ayrıca sayılmaz.
- **Self-booking neden artık fazla rezervasyon yapamaz:** Available credit hesabı açık booking'leri düştüğü için, 1 hakkı olan öğrencinin 1 açık booking'i varsa available=0 → 2. denemede 409.
- **Staff RequiresPayment davranışı:** Owner/StudioAdmin eligibility/hak olmasa da yazabilir; booking `RequiresPayment=true` + `PackageEligibilityStatus` sebebiyle işaretlenir, raporlanır. Instructor bypass edemez (409). Sonradan geçerli paketle consume olursa `RequiresPayment=false` + `Resolved`.
- **Attendance consume ↔ expected package:** Açık `StudentPackageId` yoksa booking'in `ExpectedStudentPackageId`'si önceliklidir. Expected paket geçersiz/balance 0/ClassType uyumsuz ise sessiz fallback yok → 409 + staff'a açık seçim isteği. Idempotency korunur.
- **Effective price source of truth:** Satış fiyatı `PackagePlanPriceVersion` üzerinden `EffectiveFrom <= today` max version ile hesaplanır; `PackagePlan.Price` cache olarak kalır ama hiçbir karar buna bağlı değil → stale cache satışı bozmaz; gelecek zam tarihi gelince job'sız otomatik geçer.

### Bilinçli bırakılan riskler
- (a) Kredi hâlâ yoklamada düşer; geçmişte yoklaması girilmemiş "Booked" dersler available hesabını meşgul tutar — staff yoklama/iptal girerse normalize olur.
- (b) Available credit hesabı eşzamanlı iki self-booking'de yarış yaşayabilir → çözüm: hesap + booking aynı transaction'da, paket satırı **row-lock** (mevcut makeup-credit deseni). Spec bunu zorunlu kılar.
- (c) Bir paket birden çok ClassType kapsıyorsa available hak ClassType'a göre değil **pakete göre** sayılır (bilinçli; paket tek havuz).

---

## 8. Bu sprintte YAPILMAYACAKLAR

- Gerçek online/kart ödeme, recurring, gateway.
- Rezervasyonda kredi fiilen düşürme/iade (karar: kredi yoklamada düşer; booking yalnız hak rezerve eder — Locked #2).
- Plan kredi/limit dışı zorlamalar; otomatik paket-yenileme; effective-price geçişi için background job (gereksiz — query hesaplar).
- Büyük refactor; pipeline sırası / tenant izolasyonu değişmez.
- Commit/push (onaysız).

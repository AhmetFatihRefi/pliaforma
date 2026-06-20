# SPRINT 27 — SPEC: Instructor Makeup Request & Pending Schedule Blocks

> **Durum:** SPEC turu — onay bekliyor. Kod YOK, migration YOK, commit/push YOK.
> **Kaynak ürün notu:** `docs/SPRINT-27-NOTES.md`.
> **Önemli:** Bu iş, v1.1.x role-hardening kapsamına GİRMEZ (ayrı Sprint 27). Bir DB migration gerektirir
> (aşağıda gerekçeli; **tek yeni tablo, additive, low-risk**).

---

## 1. Amaç

Hoca (Instructor), **kendi ders bağlamındaki** ve **telafi kredisi olan** öğrenci için bir **telafi dersi
talebi** oluşturabilsin. Talep, Owner/StudioAdmin **onaylamadan aktif ders sayılmaz**; ama oluşturulduğu anda
programda **"Onay bekliyor"** etiketiyle görünür ve **hoca + öğrenci saatini bloklar**, **kredisini rezerve
eder**. Owner onaylayınca gerçek bir telafi dersine (ClassSession `SessionKind=Makeup` + ClassBooking) dönüşür
ve kredi gerçekten tüketilir; reddedilince/geri çekilince saat boşalır ve rezerve serbest kalır.

---

## 2. Veri modeli analizi — Yeni entity mi, ClassSession `Pending` status mı?

İki seçenek değerlendirildi. Mevcut kod kanıtları:

- `ClassSessionStatus` = `{ Scheduled, Cancelled, Completed }`, **string** olarak saklanıyor
  (`HasConversion<string>().HasMaxLength(16)`) → yeni bir enum değeri eklemek **tek başına şema migration
  gerektirmez** (DB check-constraint yok).
- AMA `ClassSession`/`ClassBooking` çekirdeği "gerçek ders" varsayımına gömülü: `BookedCount`/kapasite atomik
  rezervasyonu, `BookingCreator` yalnız `Status==Scheduled` seansa booking yapar (satır 50), attendance/confirm,
  sözleşme `AvailableLesson` düşümü, iptal→telafi grant, raporlar… hepsi seansı "gerçek" kabul eder.

### Seçenek A — Ayrı `MakeupRequest` tablosu **(ÖNERİLEN)**
- **Artı:** Pending kayıt kendi tablosunda yaşar; `ClassSession`/`ClassBooking`/attendance/kapasite/sözleşme
  invariant'larına **sıfır risk**. Reject/withdraw sadece bir satır durumu. Approve = mevcut `BookingCreator`
  makeup yolu üzerinden **gerçek** Makeup `ClassSession`+`ClassBooking` materyalize eder (zaten 1 kredi tüketir,
  strict).
- **Eksi:** Çakışma kontrolleri (hoca/öğrenci) ve schedule görünümü artık **iki kaynağı** (ClassSessions +
  pending MakeupRequests) birlikte taramalı. Ama bu **açık ve kapsamı belli** bir genişletme — kazara değil.
- **Migration:** EVET ama **additive** (tek yeni tablo; mevcut sütun/veri değişmez; backfill yok). Rollback =
  tabloyu düşür (kayıt yoksa risksiz).

### Seçenek B — `ClassSession.Status = PendingApproval` + `SessionKind=Makeup` + `RequestedByUserId`/`ApprovalStatus`
- **Artı:** Tek kaynak; çakışma/schedule filtresine `Pending` eklemek yeterli.
- **Eksi (ağır):** "Pending bir ClassSession" → seat tüketir mi, sözleşme hakkı düşer mi, attendance açılır mı?
  Kodda onlarca `Status == Scheduled` varsayımı pending'i **dışlamak** zorunda kalır; `BookingCreator` pending
  seansa booking'i reddeder (satır 50) → öğrenciyi booking'siz iliştirmek gerekir. Pending semantiği çekirdek
  invariant'lara **sızar** → yüksek regresyon riski. Ayrıca yeni sütunlar (`RequestedByUserId`/`ApprovalStatus`)
  → **yine migration**.

### Karar
**Seçenek A.** Daha güvenli ve daha az riskli: pending kendi tablosunda izole; çekirdek davranış bozulmaz;
approve mevcut, test edilmiş makeup-booking yolunu kullanır. Çapraz-kesen tek değişiklik conflict + schedule
union — bilinçli ve kapsamı belli.

---

## 3. Önerilen veri modeli

### Yeni entity: `MakeupRequest` (tenant-scoped, `BaseEntity : ITenantScoped`)

| Alan | Tip | Açıklama |
|---|---|---|
| `Id` | Guid | PK |
| `StudioId` | Guid | tenant |
| `StudentProfileId` | Guid | telafi yapılacak öğrenci |
| `RequestedInstructorProfileId` | Guid | talebi açan hocanın profili (= planlanan hoca) |
| `ClassTypeId` | Guid | ders tipi (süre kaynağı → `EndAt = StartAt + DefaultDurationMinutes`) |
| `StartAt` | DateTime (Unspecified, wall-clock) | önerilen başlangıç (duvar saati; ClassSession ile aynı eksen) |
| `EndAt` | DateTime (Unspecified) | `StartAt + ClassType.DefaultDurationMinutes` (türetilip yazılır) |
| `Status` | `MakeupRequestStatus` enum (string) | `Pending / Approved / Rejected / Withdrawn` |
| `Description` | string? (max 1000) | hocanın açıklaması/notu |
| `RequestedByUserId` | Guid | talebi açan kullanıcı |
| `DecisionByUserId` | Guid? | onay/red kararını veren manager |
| `DecidedAt` | DateTime? (UTC) | karar zamanı |
| `DecisionNote` | string? (max 1000) | manager notu (red gerekçesi vb.) |
| `ResultingClassSessionId` | Guid? | approve sonrası materyalize edilen seans (audit izi) |
| `ResultingBookingId` | Guid? | approve sonrası materyalize edilen booking |
| (audit) | | `CreatedAt/CreatedByUserId/UpdatedAt` BaseEntity'den |

**Enum (yeni):** `MakeupRequestStatus { Pending, Approved, Rejected, Withdrawn }` — string saklama
(`HasConversion<string>().HasMaxLength(16)`), mevcut konvansiyonla aynı.

**Index'ler:**
- `(StudioId, Status)` — owner liste ekranı + conflict taramaları.
- `(StudioId, RequestedInstructorProfileId, Status)` — hoca-çakışması + "kendi talepleri".
- `(StudioId, StudentProfileId, Status)` — öğrenci-çakışması + öğrenci programı + reserve sayımı.

**EF eşleme:** `StartAt`/`EndAt` → `timestamp without time zone` (wall-clock, `Kind=Unspecified`); `DecidedAt`
→ `timestamp with time zone` (UTC instant). (ClassSession ve ledger ile aynı iki-eksen ayrımı.) Plain referans
alanları için FK YOK (ikinci cascade yollarından kaçınma, mevcut `ActualInstructorProfileId` deseni gibi).

### Mevcut entity değişikliği: YOK
`ClassSession`, `ClassBooking`, `MakeupCreditTransaction` **şema olarak değişmez**. Sadece davranış (conflict +
schedule + booking-create) bunları okur.

---

## 4. Conflict modeli (pending bloklar)

Mevcut (yalnız `Scheduled` ClassSession/Booked ClassBooking'e bakar):
- `SessionRules.HasConflictAsync` → aynı hoca örtüşme.
- `SessionRules.StudentConflictAsync` → aynı öğrenci örtüşme.

**Yeni:** pending talepler de bloklamalı. İki yeni yardımcı (ikinci kaynak — `MakeupRequest`):
- `PendingInstructorConflictAsync(studioId, instructorProfileId, startAt, endAt, excludeRequestId?)`
  → `MakeupRequests.Any(Status==Pending && RequestedInstructorProfileId==X && overlap)`.
- `PendingStudentConflictAsync(studioId, studentProfileId, startAt, endAt, excludeRequestId?)`
  → `MakeupRequests.Any(Status==Pending && StudentProfileId==X && overlap)`.

**Çift yönlü uygulanır (kritik):**
1. **Talep oluştururken** (MakeupRequest create): hem gerçek seanslara (`HasConflictAsync` +
   `StudentConflictAsync`) **hem** diğer pending taleplere (yeni yardımcılar) bakılır → çakışan slota talep
   açılamaz.
2. **Gerçek seans oluştur/ taşıma / booking** yollarında da pending taleplere bakılır
   (`EnsureValidAsync`/`EnsureMovableAsync`/`BookingCreator`) → pending bir talebin bloğuna gerçek ders
   yerleştirilemez. (Aksi halde pending "bloklar" iddiası tek yönlü kalırdı.)

**Sadece `Status==Pending` bloklar.** `Approved` zaten gerçek seans olarak ayrıca taranır (çift sayım
önlemek için pending taraması yalnız Pending'e bakar). `Rejected`/`Withdrawn` **hiç bloklamaz**.

`overlap` tanımı mevcutla aynı: `StartAt < otherEnd && otherStart < EndAt`.

---

## 5. Makeup credit reserve / consume modeli

**Mevcut ledger:** `MakeupCreditTransaction`, bakiye = aktif hareketlerin işaretli toplamı; `Type ∈
{Granted+, Used−, Restored+, Revoked−, Expired−}`. Makeup booking → `Use(−1)` (mevcut `BookingCreator`).

İki yaklaşım:

### (a) Yeni ledger tipi `Reserved`/`ReservationReleased`
Pending → `Reserved(−1)`; approve → release+Use; reject → `ReservationReleased(+1)`. Bakiye otomatik düşer.
**Eksi:** `MakeupCreditTransactionType` string saklanıyor → yeni değer eklemek tek başına şema migration
gerektirmez ama ledger semantiğini ("gerçek verilen/kullanılan") kirletir; reject'te ters-kayıt churn'ü;
raporlar yeni tipleri ayıklamak zorunda.

### (b) Rezerve = aktif pending talep sayısı ile **örtük** (ÖNERİLEN — yeni ledger tipi YOK)
- **Kullanılabilir bakiye** = `ledgerBalance − activePendingRequestCount(student)`.
  (`activePendingRequestCount` = `MakeupRequests.Count(Status==Pending && StudentProfileId==X)`.)
- **Talep oluştururken:** `ledgerBalance − pendingCount > 0` değilse 409 (telafi hakkı yok/rezervde).
  → aynı kredi ikinci talebe gitmez; tek-kullanım garanti.
- **Approve:** mevcut `BookingCreator` makeup yolu çalışır → `Use(−1)` (gerçek tüketim). Pending kapanır
  (Status=Approved) → pendingCount düşer, ledger −1 → net tutarlı.
- **Reject/Withdraw:** ledger'a **hiç dokunulmaz** (rezerve örtük; pending kapanınca otomatik serbest).
- **Yarış koruması:** mevcut `IMakeupCreditLock.LockStudentAsync` ile öğrenci satırı kilitlenir → eşzamanlı iki
  talep aynı son krediyi rezerve edemez (BookingCreator'daki desenin aynısı).

### Karar
**(b).** Ledger "gerçek krediler" anlamını korur; **yeni transaction type GEREKMEZ**; reject'te ledger churn'ü
yok; migration yalnız tek yeni tabloyla sınırlı kalır. Soru #5'in cevabı: **mevcut ledger uygundur, yeni tip
gerekmez** — rezerve, pending-talep sayısıyla hesaplanır.

> **Açık soru (R1):** Kullanılabilir-bakiye = `ledgerBalance − pendingCount` tek noktada (örn.
> `MakeupCreditService.GetAvailableBalanceAsync`) hesaplanmalı; telafi gösteren her yer (grant ekranı, makeup
> booking) bunu mu kullanmalı yoksa yalnız talep-create yolu mu? (Öneri: tek nokta, her yer kullanır →
> tutarlı.)

---

## 6. Yetki / güvenlik modeli

| Aksiyon | Instructor | Owner/StudioAdmin | Student |
|---|---|---|---|
| Talep oluştur (kendi bağlamı) | ✅ (yalnız `RequestedInstructorProfileId == kendi profili`) | ✅ | ❌ |
| Talepleri listele | ✅ yalnız **kendi** talepleri | ✅ **tümü** | ✅ yalnız **kendi** (salt-okuma, programda) |
| Pending talebi geri çek (withdraw) | ✅ yalnız **kendi** pending'i | ✅ (herhangi) | ❌ |
| Approve | ❌ | ✅ | ❌ |
| Reject | ❌ | ✅ | ❌ |
| Edit-approve (tarih/saat/hoca değiştirerek onayla) | ❌ | ✅ | ❌ |
| Telafi kredisi ver/düzelt | ❌ (mevcut v1.1.1 kuralı) | ✅ | ❌ |

**Hard kurallar (backend zorunlu):**
- Instructor başka hoca adına talep açamaz → `RequestedInstructorProfileId` request gövdesinden DEĞİL,
  **token'daki instructor profilinden** alınır (manager ise gövdeden gelebilir).
- Instructor "kendi bağlamındaki öğrenci": **Açık soru (R2)** — tanım ne? (a) öğrencinin bu hocayla aktif
  sözleşmesi var; veya (b) öğrenci bu hocanın geçmiş/aktif bir seansında Booked olmuş. (Öneri: **(a) aktif
  sözleşme** — net ve sözleşme-merkezli modele uygun; yoksa 403.)
- Withdraw yalnız `Status==Pending` ve `RequestedByUserId==self` (manager hariç) iken.
- Approve/Reject yalnız `Status==Pending` iken (idempotent değil → ikinci karar 409).
- Tüm yazma uçları `IStudioWriteRequest` (transaction + tenant scope). Controller seviyesi role-gate +
  pipeline `MembershipGuard`.

---

## 7. Endpoint taslağı

**Instructor + Owner/StudioAdmin (talep):**
- `POST /api/studio/makeup-requests` — talep oluştur. Gövde: `{ studentProfileId, classTypeId, startAt,
  description?, requestedInstructorProfileId? (yalnız manager) }`. Instructor → kendi profili zorlanır.
  Conflict (gerçek + pending) + reserve (available>0) kontrol. → 201 / 409 / 403.
- `GET /api/studio/makeup-requests` — liste. Instructor → kendi; Owner/Admin → tümü; `?status=` filtresi.
- `GET /api/studio/makeup-requests/{id}` — tek talep (tenant + sahiplik/rol kontrolü).
- `POST /api/studio/makeup-requests/{id}/withdraw` — geri çek (Instructor kendi pending'i / manager herhangi).

**Owner/StudioAdmin (karar):**
- `POST /api/studio/makeup-requests/{id}/approve` — onayla. Gövde (opsiyonel edit-approve):
  `{ startAt?, requestedInstructorProfileId?, classTypeId? }` verilirse o değerlerle materyalize. Akış:
  conflict yeniden doğrula (final değerlerle) → Makeup `ClassSession` oluştur (`SessionKind=Makeup`,
  `IsIndividual=true` öneri) → `BookingCreator.CreateAsync(..., SessionKind=Makeup)` → `Use(−1)` →
  `Status=Approved`, `ResultingClassSessionId/BookingId` yaz. → 200 / 409.
- `POST /api/studio/makeup-requests/{id}/reject` — reddet. Gövde: `{ decisionNote? }`. → `Status=Rejected`
  (ledger'a dokunmaz). → 200.

**Schedule görünümü (union):**
- `GET /api/studio/class-sessions` (mevcut `GetClassSessions`) yanıtına **pending talepler** ayrı bir DTO
  alanı/işaretle eklenir: ya aynı listede `kind:"PendingMakeupRequest"` + `isPending:true` öğeleri, ya da
  yanıt zarfında `pendingMakeupRequests: [...]`. (Öneri: aynı zaman-eksenli listede `isPending` bayraklı öğe →
  takvim tek geçişte çizer.) Instructor → kendi pending'i + gerçek seansları; Owner → tümü.
- `GET /api/student/...` (öğrenci programı) → kendi pending taleplerini **salt-okuma** "Onay bekleyen telafi
  talebi" etiketiyle döndürür.

**Pending kayıtta KAPALI:** attendance, confirm, booking-ekle, reschedule — pending bir gerçek seans
olmadığından bu uçlar onu görmez/işlemez (doğal olarak; ek guard'a gerek yok çünkü ClassSession yok).

---

## 8. Frontend ekran taslağı

1. **Owner/StudioAdmin — "Telafi Talepleri" (yeni sayfa, `/dashboard/makeup-requests`):**
   - Sidebar'da yalnız manager'a görünür (mevcut `isManager` gate deseni).
   - Liste: talebi açan hoca · öğrenci · ders tipi · tarih/saat · açıklama · durum · **çakışma durumu** (gerçek
     veya pending çakışma uyarısı) · aksiyonlar: **Onayla / Reddet / Düzenleyerek onayla**.
   - Edit-approve: tarih/saat/hoca/ders-tipi düzenleme alanlı bir modal → approve gövdesine yollar.
   - Filtre: durum (Pending varsayılan).
2. **Instructor — talep oluşturma:** schedule veya öğrenci bağlamından "Telafi talebi oluştur". Yalnız
   **kullanılabilir telafi hakkı olan** (available>0) ve **kendi bağlamındaki** öğrenciler seçilebilir
   (Sprint 20 makeup-booking UI deseni). Kendi pending taleplerini listede görür + **"Geri çek"**.
3. **Schedule (tüm roller):** pending talep, gerçek derslerden **görsel olarak ayrı** ("Onay bekliyor" rozeti,
   soluk/dashed stil). Pending üzerinde yoklama/onay aksiyonu **görünmez**.
4. **Student programı:** kendi pending talebi salt-okuma "Onay bekleyen telafi talebi" etiketi; aksiyon yok.

---

## 9. Migration analizi

- **Gerekir mi?** **EVET** — `MakeupRequest` yeni tablo + `MakeupRequestStatus` enum string sütunu. (Mevcut
  enum'lara değer eklenmiyor; mevcut tablo/sütun değişmiyor.)
- **Hangi tablo/alan/status:** tek yeni tablo `MakeupRequests` (bölüm 3 alanları) + 3 index. **Mevcut entity'de
  alan/status değişikliği YOK.**
- **Rollback riski:** Düşük — additive. Down migration = `DROP TABLE "MakeupRequests"`. Kayıt yoksa risksiz;
  pilotta veri varsa down = veri kaybı (beklenen, talep verisi geçici/iş-akışı).
- **Mevcut veriye etki:** **YOK** — backfill yok, mevcut satırlar değişmez, FK eklenmiyor (plain referans).
- **Migration adı (öneri):** `Sprint27_MakeupRequest`.
- **Not:** Karar (b) sayesinde ledger şeması ve `MakeupCreditTransactionType` enum'u **değişmez** → migration
  yalnız tek tabloyla sınırlı.

---

## 10. Test planı

**Backend smoke — `sprint27-smoke.sh` (yeni):**
- Instructor kendi bağlamındaki (aktif sözleşmeli) + kredili öğrenci için talep → 201.
- Instructor başka hoca adına talep (`requestedInstructorProfileId` farklı) → 403 (token profili zorlanır).
- Kredisiz öğrenci için talep → 409 (reserve/available=0).
- Aynı krediyle 2. pending talep → 409 (rezerve tek-kullanım: `balance − pendingCount`).
- Pending talep **hoca saatini bloklar:** çakışan gerçek seans create → 409; **öğrenci saatini bloklar:**
  çakışan booking/seans → 409.
- `Rejected`/`Withdrawn` talep **bloklamaz:** reject sonra aynı slota gerçek ders → 200.
- Instructor approve dener → 403. Owner approve → 200 → gerçek Makeup seans+booking oluşur, **ledger `Use(−1)`**
  (bakiye 1 düşer), `ResultingClassSessionId/BookingId` dolu.
- Owner reject → 200, ledger değişmez (bakiye aynı), rezerve serbest (pendingCount düşer → tekrar talep 201).
- Owner **edit-approve** (saat değiştir) → yeni saatte materyalize; final conflict doğrulanır.
- Instructor kendi pending'ini withdraw → 200; başka hocanın pending'ini withdraw → 403.
- Pending kayıtta attendance/confirm → erişilemez (ClassSession yok).
- **Owner regresyon:** mevcut makeup-booking (Sprint 20) + grant/revoke (v1.1.1) **bozulmaz**.

**E2E (Playwright):**
- Instructor: "Telafi Talepleri" yönetim sayfasını **görmez** (sidebar manager-only); talep oluşturma akışı
  yalnız kredili/kendi öğrencileri listeler; kendi pending'i + "Geri çek".
- Owner: "Telafi Talepleri" sayfası açılır; Onayla/Reddet/Düzenleyerek-onayla görünür.
- Schedule: pending talep "Onay bekliyor" rozetiyle ayrışır; pending üzerinde yoklama/onay yok.
- Student: programında "Onay bekleyen telafi talebi" salt-okuma görünür.

**Regresyon kapısı:** tüm mevcut smoke suite (compose 29/29) + E2E (58/58) yeşil kalmalı; conflict yardımcıları
genişlediği için Sprint 18a/20/21 smoke'ları özellikle doğrula.

---

## 11. Riskler

- **Çift-kaynak conflict:** Çakışma artık ClassSessions + pending MakeupRequests'i birlikte taramalı. Bir yolu
  unutmak (örn. gerçek seans create'in pending'e bakmaması) "tek yönlü blok" hatası verir → bölüm 4'teki **çift
  yönlü** uygulama şart; smoke ikisini de test eder.
- **Schedule union performansı/gösterimi:** iki kaynağı tek takvimde birleştirmek; pending'in görsel ayrımı net
  olmazsa kullanıcı gerçek ders sanabilir → rozet + stil zorunlu.
- **Reserve doğruluğu (b):** `balance − pendingCount` her zaman row-lock altında hesaplanmalı; aksi halde
  eşzamanlı iki talep son krediyi iki kez rezerve eder. Mevcut `IMakeupCreditLock` reuse → test et.
- **Edit-approve tutarlılığı:** approve'da saat/hoca değişirse conflict **final değerlerle** yeniden
  doğrulanmalı (talep anındaki değerlerle değil) → yoksa çakışan ders materyalize olur.
- **"Kendi bağlamı" tanımı (R2):** çok gevşek olursa hoca herhangi öğrenciye talep açar (gizlilik); çok katı
  olursa meşru telafi engellenir.
- **Migration (pilot):** pilot canlı; yeni tablo additive olduğundan düşük risk, ama migration compose
  postgres'e sorunsuz uygulanmalı + `has-pending` temiz olmalı.

---

## 12. Açık sorular (onay bekleyen)

- **R1 — Kullanılabilir bakiye tek nokta mı?** `ledgerBalance − pendingCount` tek serviste hesaplanıp telafi
  gösteren her yer mi kullansın (öneri: evet), yoksa yalnız talep-create yolu mu?
- **R2 — "Instructor'ın kendi bağlamındaki öğrenci" tanımı?** (a) bu hocayla aktif sözleşme (öneri) / (b) bu
  hocanın bir seansında Booked olmuş / (c) ikisinden biri.
- **R3 — Approve materyalizasyonu bireysel mi?** Makeup seans `IsIndividual=true` (öneri — telafi tipik 1-1) mi,
  yoksa gruba eklenebilir mi?
- **R4 — Schedule union biçimi?** Aynı listede `isPending` bayraklı öğe (öneri) mi, yoksa ayrı
  `pendingMakeupRequests` dizisi mi?
- **R5 — Talep zaman penceresi?** Geçmiş saate talep engellenir (öneri: evet, `StartAt > studioNow`). Telafi
  expiry zorlaması **kapsam dışı** (aşağıda).
- **R6 — Reject/Withdraw audit?** `Status=Rejected/Withdrawn` satır olarak **saklanır** (öneri — audit) mi,
  soft-delete mi? (Öneri: saklanır; conflict'e dahil değil.)

---

## 13. Kapsam dışı (Sprint 27)

Gerçek bildirim kanalı (e-posta/push) · telafi **expiry** zorlaması · drag/drop grid · çoklu-currency · online
ödeme · telafi talebinden ücret/borç üretimi. (Mevcut TECH-DEBT ile uyumlu.)

---

## 14. Beklenen çıktı (onay sonrası uygulama)

Onaylanırsa uygulama sırası: `MakeupRequest` entity+config+**migration** (`Sprint27_MakeupRequest`) →
`MakeupRequestStatus` enum → conflict yardımcıları (çift yönlü) → `GetAvailableBalanceAsync` (b) →
feature handler'ları (create/list/get/withdraw/approve/reject) → controller `api/studio/makeup-requests` →
schedule union → frontend "Telafi Talepleri" sayfası + schedule rozeti + student görünümü →
`sprint27-smoke.sh` + E2E → full compose doğrulama → rapor. **Commit/tag yalnız ayrı onayla.**

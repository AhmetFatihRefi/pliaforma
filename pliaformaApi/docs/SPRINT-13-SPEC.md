# Sprint 13 — Abonelik Yaşam Döngüsü, Plan Kataloğu, Tahsilat Defteri ve Lisans Kontrolü

> Durum: **UYGULANDI ✅** (v2 spec birebir hayata geçirildi). Gerçek kart tahsilatı (gateway) bu sprintte YOK;
> o, Sprint 14'tür (webhook ile bu mantığa bağlanır). Doğrulama: `sprint13-smoke.sh` (31) + 14 smoke + 28 E2E +
> full compose. Detay: [sprints.md](sprints.md#sprint-13--tamamlanan-kapsam).

## Ana hedef
Platform sahibinin (şirket) **yönetilebilir plan kataloğu** + her stüdyonun **abonelik yaşam döngüsü**
(deneme → aktif → gecikme → iptal) + **abonelik tahsilat defteri** + **lisans kontrolü** (lisanssız stüdyo
yazamaz, okur) + stüdyonun **kendi aboneliğini görüp iptal edebilmesi** ve lisanssızken **neyin kilitli
olduğunu görmesi**. Tahsilat şimdilik platform-admin "ödeme kaydet" ile manuel; gateway (Sprint 14) aynı defteri
otomatik besler.

## Kararlar (kullanıcı onayı ile sabit)
1. **Lisans bitince/ödenmeyince → salt-okunur** (yazma 403; okuma + login açık; veri SİLİNMEZ).
2. **PastDue grace = 7 gün** (dönem bitiminden sonra 7 gün tolerans, sonra salt-okunur).
3. **Deneme = 14 gün**, stüdyo açılınca **otomatik** başlar (default plan ile).
4. **Login açık**; lisanssızken UI'da belirgin uyarı + kilitli yazma aksiyonları.
5. **Plan kataloğu dinamik** (admin ekle/düzenle/fiyat güncelle); intro/kampanya fiyatı desteklenir; fiyat
   **atama/tahsilat anında snapshot**'lanır (plan sonradan değişse mevcut abonelik/ödeme etkilenmez).
6. **Self-cancel:** stüdyo Owner kendi aboneliğini iptal eder; model **cancel-at-period-end** (aşağıda netleşti).
7. **Plan limitleri** (maxHoca/öğrenci): bu sprintte **sakla + göster**, **zorlama YOK**.
8. **Tahsilat ayrı defter:** abonelik ödemeleri `PlatformSubscriptionPayment` ledger'ında; **öğrenci `Payment`
   entity'siyle ASLA karıştırılmaz.**
9. **Fail-open görünür:** aboneliği olmayan stüdyo lisanslı sayılır ama platformda **sayılır/uyarılır** (sessiz değil).

---

## Bu sprintte YAPILACAK

### A) Domain — yeni entity'ler

**`SubscriptionPlan`** (platform kataloğu, tenant değil; yalnız PlatformAdmin yönetir):
`Id, Name, Tier?, MonthlyPrice (decimal), Currency, BillingPeriod (Monthly/Yearly), IntroMonthlyPrice?,
IntroPeriodCount? (ör. 3), IsCustomQuote (Kurumsal=true → "teklif"), **IsDefault (bool)**, IsActive,
DisplayOrder, Description?, FeaturesJson?, MaxInstructors?, MaxStudents? (sakla/göster), audit + soft-delete.`
- **Default plan kuralı:** Aynı anda **yalnız bir aktif default plan** olabilir (yeni default atanınca eskisi
  düşer — komut seviyesinde garanti). `register-studio` 14g trial'i **bu default plan** ile açar.

**`PlatformSubscriptionPayment`** (YENİ — abonelik tahsilat defteri/ledger):
`Id, StudioId, PlatformSubscriptionId, Amount (decimal), Currency, BillingPeriod, PeriodStart (DateOnly),
PeriodEnd (DateOnly), PaidAt (UTC), RecordedByPlatformAdminUserId?, Source (enum), ProviderRef?, Notes?,
CreatedAt, IsDeleted.`
- Enum **`PlatformSubscriptionPaymentSource`**: `Manual | Gateway | Adjustment`.
- **Öğrenci `Payment` entity'siyle KARIŞTIRILMAZ** (o stüdyo↔öğrenci; bu stüdyo↔platform). Student `Payment`
  kullanılmaz.
- Sprint 14 gateway/webhook geldiğinde **`Source=Gateway`** ile buraya kayıt atar (aynı kontrat).

### B) `PlatformSubscription` genişletme
- `SubscriptionPlanId?` referansı + **snapshot fiyat** (`MonthlyPrice` plandan atamada kopyalanır).
- `IntroPeriodsRemaining?` (ör. ilk 3 yenilemede intro fiyatı uygulanır, sonra standart).
- **Cancel-at-period-end alanları (YENİ — netleştirme):** `CancelAtPeriodEnd (bool)`, `CancelRequestedAt? (UTC)`,
  `CancelledAt? (UTC, ancak fiilen dönem bitince/efektif)`.
- Mevcut: `Status (Trialing/Active/PastDue/Suspended/Cancelled)`, `CurrentPeriodStart/End`, `TrialEndsAt`.

### C) Yaşam döngüsü mantığı
- **Otomatik trial:** `register-studio` → default plan ile `Status=Trialing`, `TrialEndsAt=+14g`,
  `CurrentPeriodEnd=TrialEndsAt`. (Default plan yoksa → aşağıdaki güvenli davranış.)
- **Ödeme kaydet (record-payment; admin, gateway sonra):** **aynı transaction içinde**
  (1) `PlatformSubscriptionPayment` satırı oluşturur (snapshot Amount/Currency/Period + Source=Manual +
  RecordedBy), (2) `Status=Active` yapar, dönemi `BillingPeriod` kadar ileri sarar, intro varsa intro fiyatı
  uygular + `IntroPeriodsRemaining--`. **Tahsilat ve period ilerletme atomiktir.**
- **İptal (cancel-at-period-end — NET MODEL):** Owner self-cancel →
  - `Status` **DEĞİŞMEZ** (Active/Trialing kalır), `CancelAtPeriodEnd=true`, `CancelRequestedAt=now`,
    `CurrentPeriodEnd` korunur.
  - **Dönem sonuna kadar lisanslı** (effective license `CancelAtPeriodEnd` olsa bile `today≤PeriodEnd` ise
    lisanslı sayar).
  - Dönem bitince **effectiveStatus = Cancelled/Unlicensed** döner (DTO'da hesaplanır). **Kalıcı job YOK** — DB
    `Status`'u otomatik değişmek zorunda değil; `CancelledAt` fiilen dönem bitiminde anlam kazanır.
  - PlatformAdmin'in **anında** suspend/cancel'ı bu sprintte **kapsam dışı** (gerekmiyor); yalnız
    cancel-at-period-end + mevcut admin-suspend (StudioStatus) yeterli.
- **PastDue:** dönem bitti + ödenmedi → 7 gün grace içinde hâlâ lisanslı; grace sonrası lisanssız.
- **Effective license (dinamik, kalıcı job yok):** lisanslı =
  `Trialing (today≤TrialEndsAt)` **veya** `Active/PastDue (today≤PeriodEnd)` **veya**
  `CancelAtPeriodEnd ama today≤PeriodEnd` **veya** `today≤PeriodEnd+7g (grace)`. Aksi → **lisanssız**.
- **effectiveStatus (DTO):** `Trialing/Active/PastDue/CancelPending(=CancelAtPeriodEnd, dönem sürüyor)/Expired/
  Unlicensed` — gösterim için (mevcut Payment Overdue / Package effectiveStatus deseniyle aynı yaklaşım).

### D) Lisans kapısı — `SubscriptionLicenseBehavior`
- `IStudioWriteRequest` komutlarında: stüdyo **lisanssızsa 403** ("Aboneliğiniz sona erdi; yazma kısıtlı").
- **İki BAĞIMSIZ kapı:** `StudioStatusBehavior` (admin-suspend) **ayrı**, `SubscriptionLicenseBehavior` **ayrı**;
  ikisi de write'ı keser. **Okuma + login etkilenmez.**
- **Platform uçları (`/api/platform/*`) license gate'e TAKILMAZ** (PlatformAdmin işlemleri; ayrıca
  IStudioWriteRequest değil → zaten geçmez). Bunu netçe koru.
- **Fail-open + GÖRÜNÜR (madde 4):** aboneliği OLMAYAN (legacy) stüdyo **lisanslı sayılır** (yanlış kilit yok),
  AMA:
  - Platform **overview/health** → `missingSubscriptionCount` alanı.
  - Platform **studio detail** → "abonelik kaydı yok — fail-open lisanslı" uyarısı.
  - Bu, **geçici/koruyucu** davranış olarak docs'a işlenir.
- **Pipeline sırası:** `Logging → Validation → StudioStatus → SubscriptionLicense → MembershipGuard →
  Transaction → Handler`.

### E) Platform API (`/api/platform/*`, PlatformAdmin)
- `GET/POST/PUT/DELETE /plans[/{id}]` — plan kataloğu CRUD (+ default plan tekilliği komutta).
- `PUT /studios/{id}/subscription/plan` — stüdyoya plan ata (snapshot).
- `POST /studios/{id}/subscription/record-payment` — tahsilat (ledger + Active + dönem ilerlet, atomik).
- `POST /studios/{id}/subscription/cancel` — admin tarafı cancel-at-period-end (Owner ile aynı davranış).
- `GET /studios/{id}/subscription/payments` — **abonelik tahsilat geçmişi** (ledger).
- Overview/detay: **`missingSubscriptionCount`** + **"bu ay manuel tahsilat"** (ledger'dan, Source=Manual,
  PaidAt bu ay) + lisans durumu.

### F) Stüdyo-tarafı API (YENİ yüzey, yalnız Owner)
- `GET /api/studio/subscription` — kendi abonelik + **effectiveStatus** + plan + dönem sonu + kalan gün +
  `cancelAtPeriodEnd` + **tahsilat geçmişi** (kendi ledger'ı).
- `POST /api/studio/subscription/cancel` — Owner cancel-at-period-end.
- StudioAdmin/Instructor/Student → 403. Owner başka stüdyo aboneliği → 404 (tenant).

### G) Migration `Sprint13_Subscriptions` + **backfill (kritik)**
- `SubscriptionPlans`, `PlatformSubscriptionPayments` tabloları + `PlatformSubscriptions` yeni alanlar.
- **Default plan seed:** migration/seed **en az bir IsDefault=true plan** oluşturur (deploy'da register-studio
  trial açabilsin). Katalog seed örneği (admin sonra düzenler):
  Başlangıç 1.490 · **Stüdyo 2.990 (intro 1.990×3ay, IsDefault)** · Profesyonel 4.990 · Kurumsal 7.990+ (custom).
- **Stüdyo backfill:** mevcut tüm aktif stüdyolara abonelik ver (ör. `Active`, `CurrentPeriodEnd=+1 yıl`) →
  deploy'da kimse kilitlenmez. (Aksi halde lisans kapısı tüm stüdyoları salt-okunur yapardı.)
- **Default plan yoksa register-studio davranışı (güvenli karar):** register **fail ETMEZ**; trial'siz/abonelik
  kaydı olmadan stüdyo açılır (fail-open ile lisanslı kalır) ve **kodda açık uyarı loglanır** ("default plan yok —
  trial başlatılamadı"). Seed/backfill bunu pratikte garanti eder; log operatöre sinyal verir.

### Frontend
- **H) Platform plan kataloğu UI** (`/platform/plans`): liste/ekle/düzenle/pasifleştir + intro alanları +
  **default işaretle** (tekil).
- **I) Stüdyo abonelik sayfası** (Owner, `/dashboard/subscription`): plan, **status + effectiveStatus**,
  **cancelAtPeriodEnd uyarısı** ("X tarihine kadar erişiminiz var, sonra kısıtlanacak"), dönem sonu/kalan gün,
  **"İptal et"**, ve **tahsilat geçmişi** (okunur tablo). Lisanssızsa kilitli özellik açıklaması.
- **J) Global lisanssız banner:** lisanssız stüdyoda dashboard üstünde belirgin uyarı + yazma butonları
  disabled/uyarılı.
- **K) Platform studio detail:** **abonelik tahsilat geçmişi** + **missing-subscription uyarısı** (yalnız
  PlatformAdmin tarafında görünür).

---

## Bu sprintte YAPILMAYACAK
Gerçek kart/online tahsilat & recurring & webhook (**Sprint 14**) · fatura/e-arşiv · plan **limit zorlaması** ·
PlatformAdmin'in **anında** suspend/cancel'ı (cancel-at-period-end yeterli) · gerçek SMS/e-posta · büyük
refactor · UI redesign · öğrenci-tarafı değişiklik.

## Önemli kurallar (taşınan standartlar)
1. Commit/push yapma. 2. Body'den StudioId'ye güvenme. 3. Tenant izolasyonu; lisans/ödeme cross-tenant
sızdırmasın. 4. **Lisans kapısı dikkatli** — fail-open + backfill ile yanlış kilit YOK + **görünür** (missing
count). 5. **Abonelik ödemesi ≠ öğrenci Payment** (ayrı ledger). 6. record-payment **atomik** (ledger + period
aynı tx). 7. Mevcut **13 smoke + 21 security + 25 E2E** bozulmasın. 8. Owner yalnız kendi aboneliğini iptal/görsün;
plan CRUD yalnız PlatformAdmin. 9. Migration backfill + default plan seed zorunlu. 10. Rapor + onay; dürüst raporlama.

## Test/Doğrulama (yeni `sprint13-smoke.sh` + güncellemeler)
**İş kuralı:**
- record-payment → **PlatformSubscriptionPayment oluşur** + period ilerler + **snapshot fiyat** kullanır.
- plan fiyatı değişince **eski subscription payment/snapshot etkilenmez**.
- Owner cancel → `CancelAtPeriodEnd=true`, status değişmez, **dönem sonuna kadar lisanslı**; dönem sonrası lisanssız.
- PastDue 7g grace; lisans bitince **yazma 403 + okuma 200**.
- intro fiyatı `IntroPeriodCount` sonrası standart.
- **yeni register-studio → 14g trial + default plan** ile subscription oluşur.
- **no-default-plan senaryosu** → register fail etmez + log/uyarı (kontrollü).

**Güvenlik/fail-open:**
- plan CRUD PlatformAdmin-only (diğer roller 403); Owner başka stüdyo aboneliği → 404.
- **missing subscription** → stüdyo write yapabilir (fail-open) **AMA** platform health `missingSubscriptionCount`
  gösterir.
- lisans kapısı cross-tenant sızdırmaz; platform uçları license gate'e takılmaz.

**Regresyon:** 13 smoke + 21 security + 25 E2E + same-origin + full compose.

## Rapor başlıkları (Sprint 13 bitince)
1. Genel Özet 2. Oluşturulan/Değiştirilen Dosyalar 3. Yeni Entity & Migration (+backfill+default plan)
4. Plan Kataloğu 5. **Tahsilat Defteri (PlatformSubscriptionPayment)** 6. Abonelik Yaşam Döngüsü 7. Cancel-at-
Period-End Raporu 8. Lisans Kontrolü + **Fail-open Görünürlük** 9. Self-Cancel & Stüdyo-Tarafı 10. Platform UI
11. Frontend Lisans/Kilit/Tahsilat-geçmişi Deneyimi 12. Yetki & Tenant 13. Test/Doğrulama 14. Bilinçli
Yapılmayanlar 15. Riskler/Borçlar 16. Sprint 14 (Gerçek Tahsilat/Gateway) Önerisi.

---
## Sonraki: Sprint 14 — Gerçek Tahsilat (Gateway)
iyzico/PayTR/Stripe: kartla ödeme + recurring + webhook → webhook **`PlatformSubscriptionPayment` (Source=Gateway)**
yazar + period ilerletir (bu sprintteki record-payment ile **aynı kontrat**). Şirket + merchant + fatura gerektirir;
bu sprintten bağımsız, sonra eklenir.

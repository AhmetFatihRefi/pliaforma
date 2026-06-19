# SPRINT 23 — Contract Amendment, Extra Lessons & Student Debt

## Amaç

Öğrencinin mevcut sözleşmesine **yeni bir sözleşme açmadan** ek ders / süre / ücret ekleyebilmek ve bunun
**borç/ödeme takibini** oluşturmak. Sözleşme; ders programının (Sprint 18a) ve Regular booking'in tek yetki
kaynağıydı; bu sprint ticari tarafı tamamlar:

- Öğrenci ek ders alırsa → ek ders hakkı + ek ücret.
- Sözleşme süresi uzarsa → bitiş tarihi ileri alma (programlama penceresi).
- Ek ücret doğarsa → öğrenci finans ekranında borç olarak görünür.
- Öğrenci ödemediyse → sözleşme-bazlı kalan borç hesaplanır.

Yetki: **Owner/StudioAdmin**. Instructor finans/amendment/borç göremez.

## Migration gerekçesi

Bu sprint mevcut şemayla yapılamaz; **additive** bir migration şarttır (`Sprint23_ContractAmendmentAndDebt`):

1. **`ContractAmendments` tablosu** — amendment geçmişi **immutable + sorgulanabilir** olmalı (kim/ne zaman/kaç
   ders/tarih/ücret/açıklama). Notes alanına JSON gömmek kırılgan → reddedildi.
2. **`Payments.StudentContractId` (nullable FK)** — ödeme sözleşmeye bağlanmadan sözleşme-bazlı borç güvenilir
   hesaplanamaz.

**Geriye dönük güvenlik:** Up yalnız `AddColumn` (nullable) + `CreateTable` + index + FK (SetNull). Mevcut kolon
**silme/rename yok**, **backfill yok**, mevcut ödemeler NULL kalır (etiketsiz). Down tabloyu + kolonu düşürür
(rollback). Compose **ve** standalone postgres'e uygulandı; `dotnet ef migrations has-pending-model-changes` →
**"No changes"**.

## ContractAmendment modeli

Append-only ledger (oluşturulduktan sonra **güncellenmez/silinmez**; düzeltme = ters işaretli yeni kayıt,
`Kind=Correction`). Alanlar:

| Alan | Tip | Açıklama |
|---|---|---|
| `StudioId`, `StudentContractId` | Guid | tenant + sözleşme (FK Restrict) |
| `Kind` | enum(string,16) | ExtraLessons / DateExtension / PriceAdjustment / Mixed / Correction / Other — raporlama yardımcısı |
| `AdditionalLessonCount` | int | ek ders deltası (negatif olabilir) |
| `PreviousTotalLessonCount` / `NewTotalLessonCount` | int | audit snapshot |
| `PreviousEndDate` / `NewEndDate` | DateOnly | audit snapshot (yalnız ileri) |
| `AdditionalPrice` | decimal(12,2) | ek ücret deltası (negatif = indirim/düzeltme) |
| `Currency` | string(8) | contract.Currency ile aynı |
| `Description` | string(1000) | "Ek paket / uzatma / özel anlaşma" |
| `CreatedByUserId`, `CreatedAt` | — | kim/ne zaman |

**Bilinçli asimetri (para vs hak):** Ders hakkı (`TotalLessonCount`) ve `EndDate` amendment uygulanırken
**contract'ta canlı güncellenir** (AvailableLessonCount ve program üretimi bunları okur); amendment satırı
Previous/New snapshot'ı audit için tutar. Para tarafında `contract.Price` **orijinal kalır**; ek ücretler
ledger deltalarıdır. Tek atomik komut (`AddContractAmendmentCommand : ICommand, IStudioWriteRequest` → tek
transaction: amendment insert + contract mutasyonu).

## Payments.StudentContractId gerekçesi

Borç **sözleşme-bazlı** hesaplanır; ödeme hangi sözleşmeye ait olduğu bilinmeden mahsup edilemez. Bu nedenle
ödeme opsiyonel olarak bir sözleşmeye etiketlenir (`StudentContractId`, nullable FK, **SetNull**). NULL → etiketsiz
ödeme; **hiçbir sözleşmenin borcundan düşülmez**, yalnız bilgi amaçlı (`untaggedPaid`) gösterilir. Ödeme bir
sözleşmeye bağlanıyorsa para birimi sözleşme ile aynı olmalı (yoksa 400).

## Debt hesaplama formülü

Stored balance **yoktur**; her sorguda hesaplanır.

```
BasePrice         = contract.Price                         (orijinal)
AdditionalCharges = Σ ContractAmendments.AdditionalPrice   (o sözleşme)
TotalCharges      = BasePrice + AdditionalCharges
Paid              = Σ Payments.Amount  WHERE StudentContractId = c.Id AND Status = Paid
Outstanding       = TotalCharges − Paid
```

- **Yalnız `Status == Paid`** ödemeler borçtan düşer (Pending/Overdue/Cancelled/Refunded düşmez).
- Öğrenci roll-up: `TotalCharges/TotalPaid/TotalOutstanding = Σ sözleşmeler`; ayrıca `UntaggedPaid` (etiketsiz
  Paid ödemeler, **roll-up'a katılmaz**).
- UI dökümü: sözleşme ücreti · ek ücretler · ödenen · kalan borç · etiketsiz ödemeler (ayrı satır).

## Amendment kuralları

- Yalnız **Active** sözleşmeye ek işlem; Cancelled/Expired → **409**.
- `EndDate` **yalnız ileri** alınabilir (geri çekme → 409). `NewEndDate` verilmezse değişmez.
- Sadece EndDate uzatma (AdditionalLessonCount = 0) **ders hakkını artırmaz** — yalnız programlama penceresi.
- `AdditionalLessonCount` negatif olabilir; ancak `NewTotalLessonCount` mevcut **planlı (Booked Regular)** ders
  sayısının altına inemez → **409**.
- `AdditionalPrice` negatif olabilir (indirim/düzeltme).
- `Currency` = contract.Currency olmalı (amendment ve ödeme) → yoksa **400**.
- Geçmiş **immutable**: amendment için update/delete endpoint **yoktur**.
- **Program üretimi amendment'ten ayrıdır**: amendment yalnız hak/tarih/ücret/geçmiş yazar; ek dersler sonra
  mevcut `GenerateContractSchedule` (veya grup değişikliği) ile üretilir. Finansal değişiklik ile program üretimi
  karışmaz.

## API (Owner/StudioAdmin)

| Verb | Route | Açıklama |
|---|---|---|
| POST | `api/studio/contracts/{id}/amendments` | ek işlem ekle |
| GET | `api/studio/contracts/{id}/amendments` | geçmiş (read-only) |
| GET | `api/studio/students/{studentId}/debt` | öğrenci borç özeti |
| POST | `api/studio/students/{studentId}/payments` | ödeme oluştur (+ opsiyonel `studentContractId`) |

## Kapsam dışı (bu sprint yapılmadı)

Online ödeme entegrasyonu · fatura/e-arşiv · muhasebe entegrasyonu · kampanya/indirim sistemi · taksit
otomasyonu · **student self-servis borç (Sprint 24'e kaldı)** · package/rate DB drop · drag/drop grid · closure
full-undo.

## Doğrulama sonuçları

- Backend build **0/0**; migration additive, compose **ve** standalone postgres'e uygulandı; `has-pending` →
  **No changes**.
- Frontend **tsc 0**, **next build 0**.
- **sprint23-smoke 24/24**: ilk borç (5000); +4 ders +2000 → Total 12 / borç 7000; sadece-tarih uzatma → hak
  artmaz; üretimde ek hak kullanılır (8 değil **12 ders**); negatif amendment booked altına inemez (409);
  ödeme→outstanding düşer; etiketsiz ödeme düşmez (untaggedPaid bilgi); amendment immutable (DELETE/PUT 404);
  currency mismatch 400; cancelled contract 409; Instructor amendment/debt 403.
- **Smoke suite 25/25** (dev + compose).
- **E2E 55/55** (15-contracts: borç özeti + "Ek işlem" + 9999 gizli korunur).
- **Full compose validation** yeşil: config dev/prod 0 · up --build · migration auto-apply · health · same-origin
  (401) · seed · compose smoke · compose E2E · down -v + restore.
- Audit: package/rate DROP yok · amendment immutable · borç hesaplanıyor (stored değil) · Instructor 403 ·
  9999 UI korunuyor.

## Bilinen notlar

- **Multi-currency roll-up — MVP varsayımı:** amendment ve ödeme her sözleşmede `contract.Currency` ile eşleşmeye
  zorlandığından tek sözleşme tutarlıdır. Öğrencinin farklı para birimli birden çok sözleşmesi olursa, borç
  özeti roll-up'ı stüdyo para birimi etiketiyle sayısal toplar (karma para birimi roll-up ileride ele alınabilir).
- **Student self-servis borç görünümü Sprint 24'e bırakıldı.** Bu sprintte yalnız Owner/StudioAdmin borç görür;
  mevcut `my-payments` (öğrenci salt-okunur ödeme listesi) bozulmadı.
- **Dev backend background task'ı exit 1**: smoke'un beklenen Instructor 403 testindeki `ForbiddenException`'ın
  pipeline'da loglanması kaynaklı (handled, 403 döndü). Fonksiyonel etki yok; asıl doğrulama compose'ta yapıldı.

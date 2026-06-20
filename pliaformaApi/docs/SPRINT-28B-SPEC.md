# SPRINT 28B — SPEC: Line Payments/Debt + Contract Line Editor

> **Durum:** Onaylandı. Kod uygulanıyor; **migration YOK** (28A alanları yeter — çıkarsa dur+raporla).
> **Faz:** 28A (line zemini + backfill + read-model, v1.3.0-pilot) bitti. **28B = sözleşme/ödeme/borç UI+backend
> line-bazlı.** Scheduling/booking/makeup line-based = **28C**; due-date reminder = **28D** (kapsam dışı).

## Onaylanan kararlar
1. **Header otomatik:** HeaderId verilirse doğrula+kullan; yoksa öğrencinin **en güncel aktif** header'ı; çok aktif
   header → UI seçtir (default en güncel); aktif header yoksa **yeni header** (dormant alanları **ilk line** değerleriyle).
2. **Line edit yalnız `DueDate` + `Notes`.** Diğer alanlar değişmez (yanlışsa yeni line). **Cancel** sınırlı: line'da
   **Paid payment** / **Booked booking (scheduled ders)** / **makeup ledger veya request** varsa → **409**; yoksa
   `Status=Cancelled`. Fiziksel silme YOK. (Cancel beklenenden karmaşıksa **dur+raporla**, 28B dışına alınabilir.)
3. **Debt uçları yerinde** line-bazlı: `students/{id}/debt` + `student/debt` URL'leri değişmez; veri line kaynaklı;
   **currency bazında** güvenli özet; cross-currency tek toplam YOK.
4. **CreateStudentContract kalır** (ilk sözleşme = contract+line). Sonraki alımlar **CreateStudentContractLine**.
5. **Currency line başına serbest** (aynı header'da TRY + USD line olabilir). Debt currency bazında ayrı. **Payment
   currency = line currency**; eşleşmezse **400**.

## Kritik ödeme/borç kuralı (legacy fallback)
- `Payment.StudentContractLineId` **dolu** → doğrudan o line'a say.
- `StudentContractLineId` **NULL** + `StudentContractId` **dolu** → **yalnız o header'da TAM 1 line varsa** legacy
  fallback olarak o line'a say. Header'da **2+ line** varsa **hiçbir line'a sayma** ("Diğer/atanmamış"). **Çift sayım
  ASLA olmaz** (çok-line header'da line'sız ödeme tüm line'lara dağılmaz).
- `StudentContractLineId` NULL + `StudentContractId` NULL → **"Diğer ödeme"** (borca sayılmaz).
- **`ContractAmendment.AdditionalPrice` artık debt'e KATILMAZ** (28A backfill'de `line.TotalPrice`'a yedirildi → çift
  sayım önlenir). Amendment UI gizlenir / "Yeni line ekle"ye yönlendirilir; audit/list kalır; drop YOK.

## Line `paid` formülü (read-model + debt ortak)
```
paid(L) = Σ Payment(Paid, lineId=L.Id)
        + ( header(L)'de aktif line sayısı == 1
            ? Σ Payment(Paid, lineId IS NULL, contractId=L.StudentContractId)
            : 0 )
remaining(L) = L.TotalPrice − paid(L)
```
(28A `ContractLineProjection` legacy-aware yapılır; `contract-lines` ucu + debt aynı formülü kullanır.)

## Backend değişiklikleri
- `CreatePaymentCommand` **+`StudentContractLineId`**: line öğrenciye ait + `Status!=Cancelled` + currency==line.Currency
  (400); `StudentContractId` line'dan türetilir. lineId yoksa eski contractId yolu tolere; ikisi de yoksa "Diğer ödeme".
- **`CreateStudentContractLineCommand`** (header çözüm + `TotalPrice=lessonCount×pricePerLesson`, override yok) +
  **`UpdateContractLineCommand`** (yalnız DueDate/Notes) + **`CancelContractLineCommand`** (409 guard'lar). Owner/StudioAdmin.
  Uçlar: `POST/PUT/POST students/{id}/contract-lines[...]` (Controller: ContractsController).
- **Line-bazlı debt servis** (mevcut `ContractDebtService` line sürümü): per-line total/paid/remaining (legacy-aware) +
  currency özet + unassigned (sayılmayan Paid ödemeler, currency bazında). `GetStudentDebt`/`GetMyDebt` line-bazlıya
  taşınır; **amendment hariç**.
- **`PaymentDto` +`StudentContractLineId` + `LineLabel`** ("ClassTypeName · start–end"); my-payments + staff gösterir.

## Frontend değişiklikleri
- Öğrenci detayı: **line kartları birincil** (ders tipi/hoca/tarih/ders sayısı/ders başı/toplam/ödenen/kalan/son ödeme/
  telafi/bireysel/durum; DueDate NULL → "Belirlenmedi") + **"Yeni line ekle"** + **line edit (DueDate/Notes)** + **cancel
  (güvenliyse)**. **Amendment UI gizlenir / "Yeni line ekle"ye yönlendirilir.**
- Ödeme UI: **line seçici** (header→line); line yoksa "Diğer ödeme". Borç özeti line + currency bazlı.
- my-payments / öğrenci finans: **line etiketi**.
- Student self "Sözleşmelerim" + my-debt: **line-bazlı** (28A `student/contract-lines` ucu), salt-okuma.

## Migration
**YOK.** Tüm alan/FK 28A'da geldi. Gerekirse **dur + raporla**.

## Test (`sprint28b-smoke.sh`)
2. line ekleme; aynı classType 2. alım yeni line (eski şişmez); Line A ödemesi yalnız A'yı etkiler, B etkilenmez;
line'sız ödeme "Diğer ödeme" (borca sayılmaz); **contractId-only legacy ödeme yalnız tek-line header'da fallback**;
**çok-line header'da null-line ödeme hiçbir line'a sayılmaz**; amendment debt'e katılmaz; header/öğrenci toplamı
line'lardan; currency bazında ayrı; cross-currency tek toplam yok; currency uyuşmazlık → 400; DueDate NULL→"Belirlenmedi";
DueDate/Notes edit; güvenli cancel (veya 409); Instructor erişemez; Student yazmaz, kendi line/debt okur; my-payments
etiketi. **Regresyon:** v1.3.0 read-model/backfill, v1.2.0 makeup-request, v1.1.2 role-hardening yeşil.

## Kapsam dışı
scheduling/booking line resolver · makeup ledger/request line enforcement · individual hard rule · level schedule
filter · due-date reminder · throttle · migration · ClassType kolon drop · online payment · invoice/e-arşiv.

## Doğrulama
backend build 0/0 · frontend tsc 0 · next build 0 · migration yok · has-pending No changes · sprint28b-smoke yeşil ·
smoke suite yeşil · E2E yeşil · full compose yeşil. Commit/tag ayrı onayla (öneri: `v1.4.0-pilot`).

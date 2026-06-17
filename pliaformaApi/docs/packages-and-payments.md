# Öğrenci Paketleri ve Ödeme Takibi (Sprint 7)

Bu **stüdyo-içi manuel** paket/ödeme takip sistemidir. **Online ödeme entegrasyonu DEĞİLDİR** (banka/iyzico/
PayTR/otomatik tahsilat/fatura yok).

## PackagePlan nedir?
Stüdyonun tanımladığı **paket şablonu** (ör. "4 Derslik Reformer Paketi"): ad, ders sayısı (CreditCount),
fiyat, opsiyonel geçerlilik süresi (ValidityDays). Öğrenciye paket satarken temel alınır. Tenant-scoped;
ad stüdyo içinde benzersizdir. Bu sürümde class type kısıtı yoktur (tüm gruplarda geçerli varsayılır).

## StudentPackage nedir?
Bir öğrenciye **satılan/atanan** paket. Plandan **kopyalanır** (plan sonradan değişse satılan paket
etkilenmez). **StudentProfileId üzerinden** çalışır (misafir öğrenciye de satılabilir; pasif öğrenciye yeni
paket tanımlanamaz). `CurrentCreditBalance` kalan ders hakkıdır; `InitialCreditCount`'tan başlar, negatif
olamaz. Status: `Active | UsedUp | Expired | Cancelled`. Paket **silinmez**, iptal (Cancelled) edilebilir;
iptal paket tüketilemez.

## Payment ile Package farkı nedir?
- **Package** = ders hakkı (kaç ders kullanılabilir). Hak **kullanımı yoklama/manuel** ledger ile düşer.
- **Payment** = para hareketi kaydı (ne kadar, ne zaman, ödendi mi). **Ayrıdırlar:** ödeme yapılması
  **otomatik ders hakkı düşürmez** ve ders kullanımı ödemeyi etkilemez. Bir Payment opsiyonel olarak bir
  pakete bağlanabilir (StudentPackageId) ama bağlanmak zorunda değildir (paket dışı ödeme de olabilir).

## Paket hakkı neden ledger ile takip edilir?
Tek tamsayı `CurrentCreditBalance` "ne zaman, neden, hangi yoklamadan düştü" sorusunu yanıtlayamaz. Bu yüzden
her kullanım/düzeltme ayrı bir **`StudentPackageTransaction`** kaydıdır (Granted/Used/Restored/Adjusted/
Revoked + Reason + SourceAttendanceId/SourceClassBookingId). `CurrentCreditBalance` hızlı okuma için
**denormalize** tutulur ve ledger ile **senkron** kalır (her hareket bakiyeyi aynı transaction'da günceller).
Hareketler **soft delete edilmez**; düzeltme ters hareketle (Adjusted/Restored) yapılır.

## Attendance paket hakkını nasıl düşürür?
Yoklama işaretlenirken (Owner/StudioAdmin) `consumePackageCredit=true` verilirse öğrencinin paketinden
**1 hak** düşülür. **Kullanılabilir paket** = `Status=Active` **ve** `CurrentCreditBalance>0` **ve**
(`ValidUntil` null **ya da** bugün/ileri). `studentPackageId` verilirse o paket kullanılabilir olmalıdır
(değilse: aktif değilse veya **süresi dolmuşsa** → 409); verilmezse **otomatik seçim**: kullanılabilir
paketler arasından **en yakın bitiş tarihli** (süresizler en sonda) — **süresi geçmiş paketler atlanır**.
Düşüm **atomik koşullu UPDATE** ile yapılır (race-safe; bakiye negatife düşmez, 0'a inince Status=UsedUp).
Düşüm + yoklama **aynı transaction'dadır**: kullanılabilir paket yoksa/bakiye yetmezse/seçilen paket
süresi dolmuşsa **ConflictException (409)** → **tüm transaction rollback** olur, yani **yoklama da
kaydedilmez** — staff hatayı görür ve paket seçer/oluşturur (bilinçli karar). Paket hakkı düşürme
**otomatik/zorunlu değildir**; staff yoklama sırasında seçer. Instructor paket hakkı düşüremez (403).

## ValidUntil nasıl yorumlanır? effectiveStatus neden var?
- **ValidUntil**, paketin son geçerlilik tarihidir (DateOnly). `null` → süresiz. Tüketim sırasında
  **bugünden (stüdyo-yerel) önceyse paket kullanılamaz** (yukarıdaki kural). Bugün veya ileri tarih ise
  kullanılabilir.
- **Background expiry job ŞU AN YOK.** Bu yüzden bir paketin DB `Status`'u, ValidUntil geçtiğinde
  otomatik olarak `Expired`'a **çevrilmez** — DB'de hâlâ `Active` görünebilir.
- Bu karışıklığı önlemek için DTO/listelerde **`effectiveStatus`** alanı vardır: DB `status` = `Active`
  ama `ValidUntil` geçmişse `effectiveStatus = Expired` döner (diğer durumlarda DB ile aynı). **Yönetimde
  ayrım korunur:** gerçek `status` (DB) iptal/düzeltme kararları için, `effectiveStatus` ise gösterim
  içindir. UI paketleri `effectiveStatus` ile rozetler (süresi dolmuş → "Süresi doldu").
- **Auto package selection neden expired atlar?** Tüketim kuralı süresi geçmiş paketi reddettiğinden,
  otomatik seçim de bunları baştan eler → kullanıcı geçerli bir paket seçmiş/oluşturmuş gibi davranır,
  "Active görünen ama aslında süresi geçmiş" paketten yanlışlıkla düşüm olmaz. Tam expiry (DB Status'u
  Expired yapan job + bakiye dışlaması) ileriki bir sprinttedir; **tüketim güvenliği bu sürümde sağlanır.**

## Aynı Attendance için ikinci kez düşüm neden engellenir?
Bir yoklama satırı yeniden işaretlenebilir (upsert). Çift düşümü engellemek için: (1) consumer, o yoklama için
zaten bir `Used` hareketi varsa hiçbir şey yapmaz; (2) DB seviyesinde **filtered unique index**
`(SourceAttendanceId) WHERE Type='Used'` aynı yoklamadan iki Used'ı reddeder. Böylece "Present + paket düş"
işlemi tekrar kaydedilse bile hak yalnızca bir kez düşer.

> **Status değişirse?** Bir yoklama paket hakkı düşürdükten sonra durumu değişse bile düşülen hak **otomatik
> geri verilmez** (güvenli karar — defter bütünlüğü). Gerekirse yönetici **manuel restore/adjust** yapar.

## Instructor neden finansal detayları göremez?
Paket fiyatları ve ödeme tutarları finansal/ticari bilgidir. Bu yüzden **PackagePlan, StudentPackage ve
Payment yönetim uçları yalnızca Owner/StudioAdmin'e açıktır** (controller `[Authorize(Roles="Owner,
StudioAdmin")]`). Instructor bu uçlara erişemez (403); yoklama ekranında öğrenci paket bakiyesi de
Instructor'a gösterilmez (GetSessionAttendance `Packages` alanını yalnızca Owner/StudioAdmin için doldurur).

## Student kendi paket/ödeme bilgisini nasıl görür?
`GET /api/student/packages` (kalan hak + geçerlilik) ve `GET /api/student/payments` (durum + ödeme günü) —
yalnızca **kendi** kayıtları. Başkasının paket/ödeme verisini göremez; yönetim uçlarına erişemez (403).

## Overdue nasıl hesaplanır?
`Overdue` durumu **DİNAMİK** türetilir, kalıcı tutulmaz: depodaki Status `Pending` **ve** `PaidAt` null **ve**
`DueDate` stüdyo-yerel **bugünden** önceyse → listede/DTO'da **Overdue** olarak gösterilir. Otomatik bir job
yoktur; "ödendi" işaretlenince Status=Paid olur ve Overdue düşer. Durum filtresi (`?status=Overdue`) de aynı
dinamik koşulla SQL'de süzülür.

## Online ödeme neden bu sprintte yok?
Kapsam **stüdyo-içi takip**tir: stüdyo elden/havaleyle aldığı ödemeyi manuel kaydeder. Online gateway,
otomatik tahsilat, kredi kartı saklama, fatura/e-arşiv ve muhasebe entegrasyonu **bilinçli olarak hariç**
(güvenlik/uyumluluk yükü + 3. parti bağımlılığı). Manuel kayıt, küçük stüdyolar için yeterli ve sade.

## Bildirimlerde kullanımı (Sprint 9)
Ödemeler **hatırlatma** üretir: `PaymentDueSoon` (Pending + bugün/yarın vadeli) ve `PaymentOverdue` (effective
Overdue). Ödenmiş ödeme için bildirim yoktur; öğrenci User'lı olmalıdır; payment başına bir kez (dedup).
Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

## Dashboard/raporlarda kullanımı (Sprint 8)
Studio dashboard + ödeme raporu, ödemeleri **DueDate** ay aralığına göre toplar ve **dinamik Overdue** ile
sınıflandırır; paket sayımlarında **effectiveStatus** (süresi geçmiş Active → Expired) dikkate alınır. Finansal
tutarlar yalnızca Owner/StudioAdmin'e gösterilir (Instructor'a gizli). Detay: [dashboards-and-reports.md](dashboards-and-reports.md).

## Sprint sonrası ödeme gateway/bildirim nasıl bağlanabilir?
- **Gateway:** `Payment` zaten durum makinesine sahip (Pending→Paid/Cancelled/Refunded). Bir online ödeme
  servisi, başarı callback'inde `mark-paid` akışını tetikler; `Method` ve bir `ProviderRef` alanı eklenir.
  Ledger/paket tarafı değişmez (ödeme hâlâ hak düşürmez).
- **Bildirim:** vadesi yaklaşan/geçen ödemeler için bir worker, dinamik Overdue sorgusunu kullanarak
  hatırlatma gönderebilir (Sprint: Bildirim).
- **Paket bitişi:** ileride bir expiry job, `ValidUntil` geçmiş Active paketleri `Expired` yapar (şu an YOK).

## Yetki & tenant özeti
Tüm entity'ler `StudioId == token.StudioId` ile yüklenir → başka stüdyo **404** (smoke ile kanıtlı: C, A'nın
planını/paketini/ödemesini göremez/değiştiremez). `StudentPackage`/`Payment` için `ClassBooking`/
`ClassSession`/`StudentProfile` aynı stüdyoya ait olmalıdır. Yönetim Owner/StudioAdmin; öğrenci yalnızca kendi.

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `GET/POST/PUT/DELETE /api/studio/package-plans[/{id}]` | paket planı yönetimi |
| `GET/POST /api/studio/students/{id}/packages` | öğrenci paketleri / paket sat |
| `GET /api/studio/student-packages/{id}` | paket + hareket geçmişi |
| `POST /api/studio/student-packages/{id}/cancel` · `/adjust` | iptal / manuel düzeltme |
| `GET /api/studio/payments?studentProfileId&status` | ödeme listesi (Overdue dinamik) |
| `GET/POST /api/studio/students/{id}/payments` | öğrenci ödemeleri / oluştur |
| `PUT /api/studio/payments/{id}` · `/mark-paid` · `/cancel` | güncelle / ödendi / iptal |
| `GET /api/student/packages` · `/api/student/payments` | öğrenci kendi paket/ödemesi |
| (Yoklama) `PUT .../attendance` `items[].consumePackageCredit` + `studentPackageId?` | paket hakkı düşürme |

Manuel doğrulama: `scripts/sprint7-smoke.sh` (plan CRUD, paket sat/bakiye, attendance consume + idempotency,
belirtilen paketten düşüm, bakiye 0 → 409, iptal paket → 409, manuel adjust, ödeme create/mark-paid/cancel,
Overdue dinamik, student self, instructor/student 403, tenant 404). Not: attendance "başlamış seans" ister →
smoke seansı ileri açıp psql ile geçmişe çeker.

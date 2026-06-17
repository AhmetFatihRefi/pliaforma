# Bildirimler ve Hatırlatmalar (Sprint 9)

Kritik olaylar için **tenant-scoped, idempotent, in-app/log** bildirim altyapısı. Bu sürümde **gerçek SMS/
WhatsApp/e-posta/push GÖNDERİLMEZ**; kanal soyutlaması kuruldu, ilk (ve tek aktif) kanal **InApp**'tir (Log
yan kanalı). Bildirimler **booking/payment iş kurallarını değiştirmez**.

## Notification nedir?
Bir kullanıcıya yönelik tekil bildirim kaydı (`Notification`): Type, Channel, Status, Title, Body, ilişkili
kayıtlar (session/booking/payment), zaman damgaları (ScheduledAt/SentAt/ReadAt) ve **DeduplicationKey**.
**Tenant-scoped** (StudioId) ve **kullanıcı-scoped** (RecipientUserId) — kullanıcı yalnızca **kendi** stüdyo
bağlamındaki **kendi** bildirimlerini görür.

## In-app notification ile SMS/WhatsApp farkı nedir?
- **InApp:** bildirim bir DB kaydıdır; uygulamada (bell + bildirimler sayfası) görünür. "Gönderim" = kaydın
  oluşması + loglanması. Dış servis çağrılmaz.
- **SMS/WhatsApp/Email:** dış servise gerçek mesaj iletimi gerektirir. Bu sürümde enum olarak vardır ama
  **uygulanmadı (no-op)** — `INotificationChannelSender` bu kanallar için false döner (Status=Failed),
  hiçbir dış servis çağrılmaz.

## Bu sprintte neden gerçek SMS/WhatsApp yok?
Gerçek gönderim 3. parti sağlayıcı (Twilio/Netgsm/WhatsApp Business vb.), kimlik/credential yönetimi, maliyet,
teslim-durumu webhooks ve onay/throttling gerektirir. Önce **sağlam, idempotent, tenant-güvenli üretim +
in-app teslim** kuruldu; gerçek kanallar sonraki sprintte sağlayıcı entegrasyonuyla eklenecek (altyapı hazır).

## DeduplicationKey neden var?
Worker periyodik çalışır ve manuel trigger da vardır; **aynı olay için ikinci bildirim üretilmemelidir**
(idempotency). Her bildirimin stüdyo içinde **benzersiz** bir `DeduplicationKey`'i olur (örn.
`class-reminder:{bookingId}`, `payment-due-soon:{paymentId}`, `payment-overdue:{paymentId}`). Üretici, oluşturmadan
önce mevcut anahtarları sorgular ve atlar; ayrıca DB'de **filtered unique index** `(StudioId, DeduplicationKey)
WHERE IsDeleted=false` bütünlüğü garanti eder.

## Ders hatırlatma nasıl çalışır?
`ClassReminder`: dersten **~1 gün önce** (StartAt, stüdyo-yerel şimdiden itibaren ~25 saatlik pencere içinde
ve gelecekte). Koşullar: booking **Booked**, session **Scheduled**, `StudentProfile.UserId` dolu. DeduplicationKey
`class-reminder:{bookingId}` → booking başına **tek** hatırlatma. **İptal booking / iptal session için
üretilmez.** Reschedule: bookingId bazlı tek hatırlatma — saat değişip henüz hatırlatma gitmediyse yeni saatle
gider; gittiyse tekrar gönderilmez (gelişmiş reschedule bildirimi ayrı sprintte).

## PaymentDueSoon / PaymentOverdue nasıl çalışır?
- **PaymentDueSoon:** Payment `Pending` + `PaidAt` null + `DueDate` bugün/yarın (stüdyo-yerel) + öğrenci UserId
  dolu. DeduplicationKey `payment-due-soon:{paymentId}`.
- **PaymentOverdue:** **effective Overdue** (Pending + PaidAt null + DueDate geçmiş) + öğrenci UserId dolu.
  DeduplicationKey `payment-overdue:{paymentId}`. Payment başına bir kez (gelişmiş tekrar hatırlatma yok).
- **Ödenmiş (Paid) ödeme için bildirim üretilmez.**

## Guest student neden in-app notification alamaz?
In-app bildirim bir **User**'a (RecipientUserId) iletilir. Misafir öğrencinin (`StudentProfile.UserId == null`)
giriş yapabileceği bir hesabı yoktur; uygulamada görüntüleyemez. Bu yüzden misafir bookingleri/ödemeleri için
**in-app bildirim üretilmez** (üretici UserId null ise atlar). Misafir User'a dönüştürülünce sonraki olaylar
için bildirim alır.

## Worker idempotency nasıl sağlanır?
Worker ve manuel trigger **aynı `ReminderScanner`**'ı kullanır. Scanner: adayları toplar → mevcut
DeduplicationKey'leri tek sorguda çeker → yalnızca **yeni** olanları oluşturur. Tekrar çalıştırmada hepsi zaten
var → 0 yeni üretim. DB filtered unique index son güvenlik ağıdır. Smoke: ikinci trigger → `total=0`.

## Worker hata alırsa ne olur?
Worker (`ReminderBackgroundService`) her tur bir DI scope açar, **aktif stüdyoları** (`Status == Active`)
dolaşır ve her biri için scanner çalıştırır. **Askıya alınmış (Suspended/Cancelled) stüdyo için reminder
ÜRETİLMEZ** (Sprint 10); manuel trigger de yazma isteği olduğundan askıda 403 olur. Bir stüdyo hata verirse
**loglanır ve diğer stüdyolara devam edilir**; bir tur tamamen
patlasa bile worker bir sonraki interval'de tekrar dener — **uygulama çökmez**. Interval
`Notifications:ReminderIntervalSeconds`'ten gelir. `Notifications:WorkerEnabled=false` ise worker kapalıdır
(Dev'de test determinizmi için kapalı; üretimde açık). Bildirim **teslimi** başarısız olsa bile (kanal hatası)
iş akışı bozulmaz; Notification `Status=Failed` + `FailureReason` ile kalır, booking/payment etkilenmez.

## Gelecekte SMS/WhatsApp/e-posta nasıl eklenecek?
`INotificationChannelSender` soyutlaması hazırdır. İleride her kanal için ayrı bir sender (örn.
`SmsNotificationSender`) eklenip kanal türüne göre seçilir (sender registry / strategy). Üretim akışı değişmeden,
bir bildirim ilgili kanala kuyruğa alınır; gerçek gönderim + teslim-durumu (webhook) + retry eklenir. Kullanıcı
bazlı tercih (preferences) ve throttling da bu aşamada gelir (bu sprintte YOK).

## Yetki & tenant özeti
| Endpoint | Erişim |
|---|---|
| `GET /api/notifications` · `/unread-count` | her kullanıcı — **yalnızca kendi** (aktif stüdyo) |
| `POST /api/notifications/{id}/read` · `/read-all` | kendi bildirimi (başkasınınki → 404) |
| `POST /api/studio/notifications/run-reminders` | Owner/StudioAdmin — yalnızca **aktif stüdyo** |

Kullanıcı yalnızca `RecipientUserId == self && StudioId == current` kayıtları görür/okur; başka kullanıcının
bildirimini okuma → **404**. Manuel trigger yalnızca current studio'yu tarar (cross-tenant yok). Worker
tenant'ları `StudioId` ile ayrı işler. Body'den gelen StudioId'ye güvenilmez.

Manuel doğrulama: `scripts/sprint9-smoke.sh` (üretim + idempotency, guest/iptal-booking/iptal-session hariç,
due-soon/overdue/paid, unread sayımı + okuma/read-all, rol kapıları 403, başka kullanıcı/tenant 404). Not:
hatırlatma stüdyo-yerel zamana (Europe/Istanbul) bağlı olduğundan smoke tarihleri o TZ ile hesaplanır; Dev'de
worker kapalı, üretim manuel trigger ile.
